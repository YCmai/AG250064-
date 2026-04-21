using Dapper;
using WarehouseManagementSystem.Models.DTOs.Integrations;
using WarehouseManagementSystem.Db;

namespace WarehouseManagementSystem.Services.Integrations;

/// <summary>
/// AGV 对外集成服务接口，负责外部数据落库与任务收件箱处理。
/// </summary>
public interface IAgvIntegrationService
{
    /// <summary>
    /// 保存工单数据到 <c>RCS_WorkOrder</c>。
    /// </summary>
    /// <param name="request">工单请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>持久化结果。</returns>
    Task<AgvPersistResult> SaveWorkOrderAsync(AgvWorkOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将 AGV 指令写入收件箱主子表（<c>RCS_AgvCommandInbox</c> / <c>RCS_AgvCommandInboxItems</c>）。
    /// </summary>
    /// <param name="request">AGV 指令请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>入箱结果。</returns>
    Task<AgvPersistResult> EnqueueAgvCommandAsync(AgvCommandRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理待消费的 AGV 指令收件箱记录，并转换到 <c>RCS_UserTasks</c>。
    /// </summary>
    /// <param name="batchSize">单批处理数量上限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次扫描并处理的收件箱记录数量。</returns>
    Task<int> ProcessPendingCommandInboxAsync(int batchSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// AGV 数据持久化状态。
/// </summary>
public enum AgvPersistStatus
{
    /// <summary>写入成功。</summary>
    Success = 0,
    /// <summary>重复数据（按幂等规则处理）。</summary>
    Duplicate = 1,
    /// <summary>存在冲突（例如同 key 但明细不一致）。</summary>
    Conflict = 2,
    /// <summary>处理失败。</summary>
    Failed = 3
}

/// <summary>
/// AGV 数据持久化结果。
/// </summary>
public sealed class AgvPersistResult
{
    /// <summary>状态码。</summary>
    public AgvPersistStatus Status { get; init; }
    /// <summary>错误信息。</summary>
    public string ErrorMsg { get; init; } = string.Empty;

    public static AgvPersistResult Success() => new() { Status = AgvPersistStatus.Success };
    public static AgvPersistResult Duplicate(string errorMsg = "") => new() { Status = AgvPersistStatus.Duplicate, ErrorMsg = errorMsg };
    public static AgvPersistResult Conflict(string errorMsg) => new() { Status = AgvPersistStatus.Conflict, ErrorMsg = errorMsg };
    public static AgvPersistResult Failed(string errorMsg) => new() { Status = AgvPersistStatus.Failed, ErrorMsg = errorMsg };
}

public class AgvIntegrationService : IAgvIntegrationService
{
    private readonly IDatabaseService _db;
    private readonly ILogger<AgvIntegrationService> _logger;

    public AgvIntegrationService(IDatabaseService db, ILogger<AgvIntegrationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<AgvPersistResult> SaveWorkOrderAsync(AgvWorkOrderRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _db.CreateConnection();
            connection.Open();

            var normalizedOrderNumber = request.OrderNumber!.Trim();
            var normalizedMaterialNumber = request.MaterialNumber!.Trim();
            var normalizedMaterialName = request.MaterialName!.Trim();
            var normalizedMsgType = request.MsgType!.Trim();

            const string duplicateSql = @"
            SELECT COUNT(1)
            FROM RCS_WorkOrder
            WHERE OrderNumber = @OrderNumber
              AND MaterialNumber = @MaterialNumber
              AND MsgType = @MsgType;";

            var duplicateCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                duplicateSql,
                new
                {
                    OrderNumber = normalizedOrderNumber,
                    MaterialNumber = normalizedMaterialNumber,
                    MsgType = normalizedMsgType
                },
                cancellationToken: cancellationToken));

            if (duplicateCount > 0)
            {
                return AgvPersistResult.Duplicate("工单已存在，禁止重复下发");
            }

            const string insertSql = @"
            INSERT INTO RCS_WorkOrder
            (
                OrderNumber,
                MaterialNumber,
                MaterialName,
                MsgType,
                CreateTime,
                ProcessStatus,
                Remarks
            )
            VALUES
            (
                @OrderNumber,
                @MaterialNumber,
                @MaterialName,
                @MsgType,
                @CreateTime,
                @ProcessStatus,
                @Remarks
            );";

            await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new
                {
                    OrderNumber = normalizedOrderNumber,
                    MaterialNumber = normalizedMaterialNumber,
                    MaterialName = normalizedMaterialName,
                    MsgType = normalizedMsgType,
                    CreateTime = DateTime.Now,
                    ProcessStatus = 0,
                    Remarks = string.Empty
                },
                cancellationToken: cancellationToken));

            return AgvPersistResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存工单失败，OrderNumber={OrderNumber}", request.OrderNumber);
            return AgvPersistResult.Failed("工单入库失败");
        }
    }

    public async Task<AgvPersistResult> EnqueueAgvCommandAsync(AgvCommandRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.TaskNumber))
            {
                return AgvPersistResult.Failed("taskNumber 不能为空");
            }

            using var connection = _db.CreateConnection();
            connection.Open();

            var tableCheck = await CheckAgvInboxTablesAsync(connection, cancellationToken);
            if (!tableCheck.IsValid)
            {
                return AgvPersistResult.Failed(tableCheck.ErrorMessage);
            }

            using var transaction = connection.BeginTransaction();

            var normalizedTaskNumber = request.TaskNumber.Trim();
            var now = DateTime.Now;
            var rawJson = System.Text.Json.JsonSerializer.Serialize(request, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            const string existingInboxSql = @"
            SELECT TOP 1 ID
            FROM RCS_AgvCommandInbox
            WHERE TaskNumber = @TaskNumber
            ORDER BY ID DESC;";

            var existingInboxId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                existingInboxSql,
                new { TaskNumber = normalizedTaskNumber },
                transaction,
                cancellationToken: cancellationToken));

            if (existingInboxId.HasValue)
            {
                var existingItemCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT COUNT(1) FROM RCS_AgvCommandInboxItems WHERE InboxId = @InboxId;",
                    new { InboxId = existingInboxId.Value },
                    transaction,
                    cancellationToken: cancellationToken));

                if (existingItemCount == request.Items.Count)
                {
                    transaction.Commit();
                    return AgvPersistResult.Duplicate("taskNumber 已存在，禁止重复下发");
                }

                transaction.Rollback();
                return AgvPersistResult.Conflict("taskNumber 已存在且明细数量不一致，拒绝写入");
            }

            const string insertInboxSql = @"
            INSERT INTO RCS_AgvCommandInbox
            (
                TaskNumber,
                Priority,
                RawJson,
                ProcessStatus,
                ErrorMsg,
                CreateTime,
                UpdateTime
            )
            VALUES
            (
                @TaskNumber,
                @Priority,
                @RawJson,
                @ProcessStatus,
                @ErrorMsg,
                @CreateTime,
                @UpdateTime
            );
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var inboxId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                insertInboxSql,
                new
                {
                    TaskNumber = normalizedTaskNumber,
                    Priority = request.Priority!.Value,
                    RawJson = rawJson,
                    ProcessStatus = 0,
                    ErrorMsg = string.Empty,
                    CreateTime = now,
                    UpdateTime = now
                },
                transaction,
                cancellationToken: cancellationToken));

            const string insertInboxItemSql = @"
            INSERT INTO RCS_AgvCommandInboxItems
            (
                InboxId,
                Seq,
                PalletNumber,
                BinNumber,
                FromStation,
                ToStation,
                TaskType,
                CreateTime
            )
            VALUES
            (
                @InboxId,
                @Seq,
                @PalletNumber,
                @BinNumber,
                @FromStation,
                @ToStation,
                @TaskType,
                @CreateTime
            );";

            foreach (var item in request.Items.OrderBy(x => x.Seq))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    insertInboxItemSql,
                    new
                    {
                        InboxId = inboxId,
                        Seq = item.Seq,
                        PalletNumber = string.IsNullOrWhiteSpace(item.PalletNumber) ? null : item.PalletNumber.Trim(),
                        BinNumber = string.IsNullOrWhiteSpace(item.BinNumber) ? null : item.BinNumber.Trim(),
                        FromStation = string.IsNullOrWhiteSpace(item.FromStation) ? null : item.FromStation.Trim(),
                        ToStation = item.ToStation!.Trim(),
                        TaskType = item.TaskType!.Value,
                        CreateTime = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            transaction.Commit();
            return AgvPersistResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "写入AGV指令Inbox失败，TaskNumber={TaskNumber}", request.TaskNumber);
            return AgvPersistResult.Failed("AGV指令入库失败");
        }
    }

    public async Task<int> ProcessPendingCommandInboxAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        using var connection = _db.CreateConnection();
        connection.Open();

        var tableCheck = await CheckAgvInboxTablesAsync(connection, cancellationToken);
        if (!tableCheck.IsValid)
        {
            _logger.LogWarning("跳过AGV指令收件箱处理：{Reason}", tableCheck.ErrorMessage);
            return 0;
        }

        var inboxList = (await connection.QueryAsync<InboxHeader>(new CommandDefinition(
            @"SELECT TOP (@TopN) ID, TaskNumber, Priority
              FROM RCS_AgvCommandInbox WITH (READPAST)
              WHERE ProcessStatus = 0
              ORDER BY ID ASC;",
            new { TopN = batchSize },
            cancellationToken: cancellationToken))).ToList();

        foreach (var inbox in inboxList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessSingleInboxAsync(connection, inbox, cancellationToken);
        }

        return inboxList.Count;
    }

    private async Task ProcessSingleInboxAsync(System.Data.IDbConnection connection, InboxHeader inbox, CancellationToken cancellationToken)
    {
        System.Data.IDbTransaction? transaction = null;
        try
        {
            connection.Open();
            transaction = connection.BeginTransaction();

            var items = (await connection.QueryAsync<InboxItem>(new CommandDefinition(
                @"SELECT ID, InboxId, Seq, PalletNumber, BinNumber, FromStation, ToStation, TaskType
                  FROM RCS_AgvCommandInboxItems
                  WHERE InboxId = @InboxId
                  ORDER BY Seq ASC;",
                new { InboxId = inbox.ID },
                transaction,
                cancellationToken: cancellationToken))).ToList();

            if (items.Count == 0)
            {
                await MarkInboxFailedAsync(connection, inbox.ID, "未找到items明细", transaction, cancellationToken);
                transaction.Commit();
                return;
            }

            var existingCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                @"SELECT COUNT(1)
                  FROM RCS_UserTasks
                  WHERE taskGroupNo = @TaskGroupNo
                    AND IsCancelled = 0;",
                new { TaskGroupNo = inbox.TaskNumber },
                transaction,
                cancellationToken: cancellationToken));

            if (existingCount == items.Count)
            {
                await MarkInboxSuccessAsync(connection, inbox.ID, transaction, cancellationToken);
                transaction.Commit();
                return;
            }

            if (existingCount > 0)
            {
                await MarkInboxFailedAsync(connection, inbox.ID, "RCS_UserTasks中已存在同组任务且数量不一致", transaction, cancellationToken);
                transaction.Commit();
                return;
            }

            const string insertTaskSql = @"
INSERT INTO RCS_UserTasks
(
    taskStatus,
    executed,
    creatTime,
    requestCode,
    taskGroupNo,
    taskType,
    priority,
    robotCode,
    sourcePosition,
    targetPosition,
    palletNo,
    binNumber,
    taskCode,
    IsCancelled,
    remarks
)
VALUES
(
    @taskStatus,
    @executed,
    @creatTime,
    @requestCode,
    @taskGroupNo,
    @taskType,
    @priority,
    @robotCode,
    @sourcePosition,
    @targetPosition,
    @palletNo,
    @binNumber,
    @taskCode,
    @IsCancelled,
    @remarks
);";

            var now = DateTime.Now;
            foreach (var item in items)
            {
                var requestCode = $"{inbox.TaskNumber}_{item.Seq:D3}";
                await connection.ExecuteAsync(new CommandDefinition(
                    insertTaskSql,
                    new
                    {
                        taskStatus = 0,
                        executed = false,
                        creatTime = now,
                        requestCode,
                        taskGroupNo = inbox.TaskNumber,
                        taskType = item.TaskType,
                        priority = inbox.Priority,
                        robotCode = "0",
                        sourcePosition = item.FromStation,
                        targetPosition = item.ToStation,
                        palletNo = item.PalletNumber,
                        binNumber = item.BinNumber,
                        taskCode = requestCode,
                        IsCancelled = false,
                        remarks = "MES->AGV(Inbox)"
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            await MarkInboxSuccessAsync(connection, inbox.ID, transaction, cancellationToken);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction?.Rollback();
            _logger.LogError(ex, "消费AGV指令收件箱失败，InboxId={InboxId}", inbox.ID);
            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE RCS_AgvCommandInbox
                  SET ProcessStatus = 2,
                      ErrorMsg = @ErrorMsg,
                      UpdateTime = @UpdateTime
                  WHERE ID = @ID;",
                new { ID = inbox.ID, ErrorMsg = $"处理异常: {ex.Message}", UpdateTime = DateTime.Now },
                cancellationToken: cancellationToken));
        }
        finally
        {
            if (connection.State == System.Data.ConnectionState.Open)
            {
                connection.Close();
            }
        }
    }

    private static Task MarkInboxSuccessAsync(System.Data.IDbConnection connection, int id, System.Data.IDbTransaction transaction, CancellationToken cancellationToken)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE RCS_AgvCommandInbox
              SET ProcessStatus = 1,
                  ErrorMsg = '',
                  ProcessTime = @ProcessTime,
                  UpdateTime = @UpdateTime
              WHERE ID = @ID;",
            new { ID = id, ProcessTime = DateTime.Now, UpdateTime = DateTime.Now },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static Task MarkInboxFailedAsync(System.Data.IDbConnection connection, int id, string errorMsg, System.Data.IDbTransaction transaction, CancellationToken cancellationToken)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE RCS_AgvCommandInbox
              SET ProcessStatus = 2,
                  ErrorMsg = @ErrorMsg,
                  UpdateTime = @UpdateTime
              WHERE ID = @ID;",
            new { ID = id, ErrorMsg = errorMsg, UpdateTime = DateTime.Now },
            transaction,
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// 检查 AGV 指令收件箱主/子表是否已创建。
    /// </summary>
    private async Task<(bool IsValid, string ErrorMessage)> CheckAgvInboxTablesAsync(System.Data.IDbConnection connection, CancellationToken cancellationToken)
    {
        var result = await connection.QuerySingleAsync<TableExistsResult>(new CommandDefinition(
            @"SELECT
                CASE WHEN OBJECT_ID(N'dbo.RCS_AgvCommandInbox', N'U') IS NULL THEN 0 ELSE 1 END AS InboxExists,
                CASE WHEN OBJECT_ID(N'dbo.RCS_AgvCommandInboxItems', N'U') IS NULL THEN 0 ELSE 1 END AS ItemsExists;",
            cancellationToken: cancellationToken));

        if (result.InboxExists == 1 && result.ItemsExists == 1)
        {
            return (true, string.Empty);
        }

        var missing = new List<string>();
        if (result.InboxExists == 0)
        {
            missing.Add("RCS_AgvCommandInbox");
        }

        if (result.ItemsExists == 0)
        {
            missing.Add("RCS_AgvCommandInboxItems");
        }

        var message = $"缺少数据表：{string.Join(", ", missing)}。请先执行脚本 Db/Sql/20260420_Create_AgvCommandInbox.sql";
        _logger.LogError("AGV收件箱表检查失败：{Message}", message);
        return (false, message);
    }

    private sealed class InboxHeader
    {
        public int ID { get; set; }
        public string TaskNumber { get; set; } = string.Empty;
        public int Priority { get; set; }
    }

    private sealed class InboxItem
    {
        public int ID { get; set; }
        public int InboxId { get; set; }
        public int Seq { get; set; }
        public string? PalletNumber { get; set; }
        public string? BinNumber { get; set; }
        public string? FromStation { get; set; }
        public string ToStation { get; set; } = string.Empty;
        public int TaskType { get; set; }
    }

    private sealed class TableExistsResult
    {
        public int InboxExists { get; set; }
        public int ItemsExists { get; set; }
    }
}
