using Dapper;
using WarehouseManagementSystem.Models.DTOs.Integrations;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Services.Tasks;

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
    /// 将 AGV 指令写入收件箱主子表（<c>RCS_AgvCommandInbox</c> / <c>RCS_AgvCommandInboxItems</c>），
    /// 并在同一主流程内直接拆分生成 <c>RCS_UserTasks</c>。
    /// </summary>
    /// <param name="request">AGV 指令请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>处理结果。</returns>
    Task<AgvPersistResult> ReceiveAndCreateUserTasksAsync(AgvCommandRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 返回 AGV 指令任务下发主链路描述。
    /// </summary>
    /// <returns>统一链路说明。</returns>
    string DescribeAgvCommandDispatchFlow();
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
    private readonly IUserTaskCreationService _userTaskCreationService;
    private readonly ILogger<AgvIntegrationService> _logger;

    public AgvIntegrationService(
        IDatabaseService db,
        IUserTaskCreationService userTaskCreationService,
        ILogger<AgvIntegrationService> logger)
    {
        _db = db;
        _userTaskCreationService = userTaskCreationService;
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

    public async Task<AgvPersistResult> ReceiveAndCreateUserTasksAsync(AgvCommandRequest request, CancellationToken cancellationToken = default)
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
                RequestCode,
                ProcessStatus,
                TaskStatus,
                ErrorMsg,
                UserTaskId,
                CreateTime,
                UpdateTime,
                ProcessTime
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
                @RequestCode,
                @ProcessStatus,
                @TaskStatus,
                @ErrorMsg,
                @UserTaskId,
                @CreateTime,
                @UpdateTime,
                @ProcessTime
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
                        RequestCode = (string?)null,
                        ProcessStatus = 0,
                        TaskStatus = (int?)null,
                        ErrorMsg = string.Empty,
                        UserTaskId = (int?)null,
                        CreateTime = now,
                        UpdateTime = now,
                        ProcessTime = (DateTime?)null
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            var inboxHeader = new InboxHeader
            {
                ID = inboxId,
                TaskNumber = normalizedTaskNumber,
                Priority = request.Priority!.Value
            };

            var inboxItems = request.Items
                .OrderBy(x => x.Seq)
                .Select(item => new InboxItem
                {
                    InboxId = inboxId,
                    Seq = item.Seq,
                    PalletNumber = string.IsNullOrWhiteSpace(item.PalletNumber) ? null : item.PalletNumber.Trim(),
                    BinNumber = string.IsNullOrWhiteSpace(item.BinNumber) ? null : item.BinNumber.Trim(),
                    FromStation = string.IsNullOrWhiteSpace(item.FromStation) ? null : item.FromStation.Trim(),
                    ToStation = item.ToStation!.Trim(),
                    TaskType = item.TaskType!.Value,
                    ProcessStatus = 0
                })
                .ToList();

            var failedItemMessages = new List<string>();

            foreach (var item in inboxItems)
            {
                var requestCode = $"{normalizedTaskNumber}_{item.Seq:D3}";

                try
                {
                    var unifiedCreateRequest = BuildUnifiedMesTaskCreationRequest(inboxHeader, item, requestCode);
                    var createResult = await _userTaskCreationService.CreateTaskAsync(
                        unifiedCreateRequest,
                        connection,
                        transaction,
                        cancellationToken);

                    if (!createResult.Success)
                    {
                        failedItemMessages.Add($"seq={item.Seq}: {createResult.Message}");
                        await MarkInboxItemFailedByInboxAsync(
                            connection,
                            inboxId,
                            item.Seq,
                            requestCode,
                            createResult.Message,
                            transaction,
                            cancellationToken);
                        continue;
                    }

                    await MarkInboxItemSuccessByInboxAsync(
                        connection,
                        inboxId,
                        item.Seq,
                        requestCode,
                        createResult.TaskId,
                        createResult.TaskStatus,
                        transaction,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理AGV指令明细失败，TaskNumber={TaskNumber}, Seq={Seq}", normalizedTaskNumber, item.Seq);
                    var itemErrorMessage = $"创建任务异常: {ex.Message}";
                    failedItemMessages.Add($"seq={item.Seq}: {itemErrorMessage}");

                    await MarkInboxItemFailedByInboxAsync(
                        connection,
                        inboxId,
                        item.Seq,
                        requestCode,
                        itemErrorMessage,
                        transaction,
                        cancellationToken);
                }
            }

            if (failedItemMessages.Count > 0)
            {
                var headerErrorMessage = failedItemMessages.Count == inboxItems.Count
                    ? $"全部明细处理失败：{string.Join(" | ", failedItemMessages)}"
                    : $"部分明细处理失败：{string.Join(" | ", failedItemMessages)}";

                await MarkInboxFailedAsync(connection, inboxId, headerErrorMessage, transaction, cancellationToken);
                transaction.Commit();
                return AgvPersistResult.Failed(headerErrorMessage);
            }

            await MarkInboxSuccessAsync(connection, inboxId, transaction, cancellationToken);
            transaction.Commit();
            return AgvPersistResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "接收并创建AGV任务失败，TaskNumber={TaskNumber}", request.TaskNumber);
            return AgvPersistResult.Failed("AGV指令处理失败");
        }
    }

    /// <summary>
    /// 统一返回 AGV 指令从接口接收到最终创建 RCS_UserTasks 的链路。
    /// </summary>
    public string DescribeAgvCommandDispatchFlow()
    {
        return "ReceiveAgvCommand -> ReceiveAndCreateUserTasksAsync -> BuildMesTaskDraftByTaskTypeAsync -> IUserTaskCreationService.CreateTaskAsync";
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

    private static Task MarkInboxItemSuccessAsync(
        System.Data.IDbConnection connection,
        int id,
        string requestCode,
        int? userTaskId,
        int? taskStatus,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE RCS_AgvCommandInboxItems
              SET RequestCode = @RequestCode,
                  ProcessStatus = 1,
                  TaskStatus = @TaskStatus,
                  ErrorMsg = '',
                  UserTaskId = @UserTaskId,
                  ProcessTime = @ProcessTime,
                  UpdateTime = @UpdateTime
              WHERE ID = @ID;",
            new
            {
                ID = id,
                RequestCode = requestCode,
                UserTaskId = userTaskId,
                TaskStatus = taskStatus,
                ProcessTime = DateTime.Now,
                UpdateTime = DateTime.Now
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static Task MarkInboxItemFailedAsync(
        System.Data.IDbConnection connection,
        int id,
        string requestCode,
        string errorMsg,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE RCS_AgvCommandInboxItems
              SET RequestCode = @RequestCode,
                  ProcessStatus = 2,
                  TaskStatus = NULL,
                  ErrorMsg = @ErrorMsg,
                  UpdateTime = @UpdateTime
              WHERE ID = @ID;",
            new
            {
                ID = id,
                RequestCode = requestCode,
                ErrorMsg = errorMsg,
                UpdateTime = DateTime.Now
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static Task MarkInboxItemSuccessByInboxAsync(
        System.Data.IDbConnection connection,
        int inboxId,
        int seq,
        string requestCode,
        int? userTaskId,
        int? taskStatus,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE RCS_AgvCommandInboxItems
              SET RequestCode = @RequestCode,
                  ProcessStatus = 1,
                  TaskStatus = @TaskStatus,
                  ErrorMsg = '',
                  UserTaskId = @UserTaskId,
                  ProcessTime = @ProcessTime,
                  UpdateTime = @UpdateTime
              WHERE InboxId = @InboxId AND Seq = @Seq;",
            new
            {
                InboxId = inboxId,
                Seq = seq,
                RequestCode = requestCode,
                UserTaskId = userTaskId,
                TaskStatus = taskStatus,
                ProcessTime = DateTime.Now,
                UpdateTime = DateTime.Now
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static Task MarkInboxItemFailedByInboxAsync(
        System.Data.IDbConnection connection,
        int inboxId,
        int seq,
        string requestCode,
        string errorMsg,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE RCS_AgvCommandInboxItems
              SET RequestCode = @RequestCode,
                  ProcessStatus = 2,
                  TaskStatus = NULL,
                  ErrorMsg = @ErrorMsg,
                  UpdateTime = @UpdateTime
              WHERE InboxId = @InboxId AND Seq = @Seq;",
            new
            {
                InboxId = inboxId,
                Seq = seq,
                RequestCode = requestCode,
                ErrorMsg = errorMsg,
                UpdateTime = DateTime.Now
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// 统一构造 MES 收件箱明细对应的任务创建请求。
    /// Why: 收件箱消费者只负责“逐条消费和回写状态”，而不同任务类型的起点/终点解析规则统一交给
    /// <c>UserTaskCreationService.BuildExternalTaskDraftAsync</c> 处理；这样后续现场主要调整“如何插入 RCS_UserTasks”时，
    /// 只需要聚焦统一任务创建服务，不需要回到收件箱消费循环里逐段排查。
    /// </summary>
    private static UserTaskCreationRequest BuildUnifiedMesTaskCreationRequest(InboxHeader inbox, InboxItem item, string requestCode)
    {
        return new UserTaskCreationRequest
        {
            SourceType = UserTaskSourceType.Mes,
            RequestCode = requestCode,
            TaskGroupNo = inbox.TaskNumber,
            Priority = inbox.Priority,
            SourcePosition = item.FromStation,
            TargetPosition = item.ToStation,
            PalletNumber = item.PalletNumber,
            BinNumber = item.BinNumber,
            ExternalTaskType = item.TaskType,
            Remarks = "MES inbox task",
            ValidateLocationExistence = false,
            ValidateReachableTarget = false,
            LockLocations = false
        };
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
        public string? RequestCode { get; set; }
        public int ProcessStatus { get; set; }
        public int? TaskStatus { get; set; }
        public string? ErrorMsg { get; set; }
        public int? UserTaskId { get; set; }
    }

    private sealed class TableExistsResult
    {
        public int InboxExists { get; set; }
        public int ItemsExists { get; set; }
    }
}
