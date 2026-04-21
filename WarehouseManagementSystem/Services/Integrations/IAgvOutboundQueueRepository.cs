using Dapper;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models;

namespace WarehouseManagementSystem.Services.Integrations;

/// <summary>
/// AGV 主动上报出站队列仓储接口。
/// 统一封装 RCS_AgvOutboundQueue 的数据访问逻辑。
/// </summary>
public interface IAgvOutboundQueueRepository
{
    /// <summary>
    /// 检查同一个业务幂等键是否已存在任意记录。
    /// </summary>
    Task<bool> ExistsByBusinessKeyAsync(string businessKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增一条出站队列记录。
    /// </summary>
    Task InsertAsync(RCS_AgvOutboundQueue entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询可处理的待发送队列。
    /// </summary>
    Task<List<RCS_AgvOutboundQueue>> GetPendingAsync(int batchSize, int maxRetryCount, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记队列记录为发送成功。
    /// </summary>
    Task MarkSuccessAsync(int id, DateTime processTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记队列记录为发送失败并计划重试。
    /// </summary>
    Task MarkFailedAsync(int id, int retryCount, string errorMsg, DateTime nextRetryTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记队列记录为失败终态（超重试上限后不再重试）。
    /// </summary>
    Task MarkAbandonedAsync(int id, int retryCount, string errorMsg, DateTime processTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按任务号和事件类型查询最新一条出站记录。
    /// </summary>
    Task<RCS_AgvOutboundQueue?> GetLatestByTaskNumberAndEventTypeAsync(string taskNumber, int eventType, CancellationToken cancellationToken = default);
}

/// <summary>
/// AGV 主动上报出站队列仓储实现。
/// </summary>
public sealed class AgvOutboundQueueRepository : IAgvOutboundQueueRepository
{
    private readonly IDatabaseService _db;

    public AgvOutboundQueueRepository(IDatabaseService db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<bool> ExistsByBusinessKeyAsync(string businessKey, CancellationToken cancellationToken = default)
    {
        using var connection = _db.CreateConnection();
        connection.Open();

        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"
SELECT COUNT(1)
FROM RCS_AgvOutboundQueue
WHERE BusinessKey = @BusinessKey;",
            new { BusinessKey = businessKey },
            cancellationToken: cancellationToken));

        return count > 0;
    }

    public Task InsertAsync(RCS_AgvOutboundQueue entity, CancellationToken cancellationToken = default)
    {
        using var connection = _db.CreateConnection();
        connection.Open();

        return connection.ExecuteAsync(new CommandDefinition(
            @"
INSERT INTO RCS_AgvOutboundQueue
(
    EventType,
    TaskNumber,
    BusinessKey,
    RequestBody,
    ProcessStatus,
    RetryCount,
    LastError,
    NextRetryTime,
    CreateTime,
    ProcessTime,
    UpdateTime
)
VALUES
(
    @EventType,
    @TaskNumber,
    @BusinessKey,
    @RequestBody,
    @ProcessStatus,
    @RetryCount,
    @LastError,
    @NextRetryTime,
    @CreateTime,
    @ProcessTime,
    @UpdateTime
);",
            entity,
            cancellationToken: cancellationToken));
    }

    public async Task<List<RCS_AgvOutboundQueue>> GetPendingAsync(int batchSize, int maxRetryCount, DateTime now, CancellationToken cancellationToken = default)
    {
        using var connection = _db.CreateConnection();
        connection.Open();

        var items = await connection.QueryAsync<RCS_AgvOutboundQueue>(new CommandDefinition(
            @"
        SELECT TOP (@TopN) *
        FROM RCS_AgvOutboundQueue WITH (READPAST)
        WHERE ProcessStatus IN (0, 2)
          AND (NextRetryTime IS NULL OR NextRetryTime <= @Now)
        ORDER BY ID ASC;",
            new
            {
                TopN = batchSize,
                MaxRetryCount = maxRetryCount,
                Now = now
            },
            cancellationToken: cancellationToken));

        return items.ToList();
    }

    public Task MarkSuccessAsync(int id, DateTime processTime, CancellationToken cancellationToken = default)
    {
        using var connection = _db.CreateConnection();
        connection.Open();

        return connection.ExecuteAsync(new CommandDefinition(
            @"
        UPDATE RCS_AgvOutboundQueue
        SET ProcessStatus = 1,
            LastError = '',
            ProcessTime = @ProcessTime,
            UpdateTime = @UpdateTime
        WHERE ID = @ID;",
            new
            {
                ID = id,
                ProcessTime = processTime,
                UpdateTime = DateTime.Now
            },
            cancellationToken: cancellationToken));
    }

    public Task MarkFailedAsync(int id, int retryCount, string errorMsg, DateTime nextRetryTime, CancellationToken cancellationToken = default)
    {
        using var connection = _db.CreateConnection();
        connection.Open();

        return connection.ExecuteAsync(new CommandDefinition(
            @"
UPDATE RCS_AgvOutboundQueue
SET ProcessStatus = 2,
    RetryCount = @RetryCount,
    LastError = @LastError,
    NextRetryTime = @NextRetryTime,
    UpdateTime = @UpdateTime
WHERE ID = @ID;",
            new
            {
                ID = id,
                RetryCount = retryCount,
                LastError = Truncate(errorMsg, 1024),
                NextRetryTime = nextRetryTime,
                UpdateTime = DateTime.Now
            },
            cancellationToken: cancellationToken));
    }

    public Task MarkAbandonedAsync(int id, int retryCount, string errorMsg, DateTime processTime, CancellationToken cancellationToken = default)
    {
        using var connection = _db.CreateConnection();
        connection.Open();

        return connection.ExecuteAsync(new CommandDefinition(
            @"
        UPDATE RCS_AgvOutboundQueue
        SET ProcessStatus = 3,
            RetryCount = @RetryCount,
            LastError = @LastError,
            ProcessTime = @ProcessTime,
            UpdateTime = @UpdateTime
        WHERE ID = @ID;",
            new
            {
                ID = id,
                RetryCount = retryCount,
                LastError = Truncate(errorMsg, 1024),
                ProcessTime = processTime,
                UpdateTime = DateTime.Now
            },
            cancellationToken: cancellationToken));
    }

    public async Task<RCS_AgvOutboundQueue?> GetLatestByTaskNumberAndEventTypeAsync(string taskNumber, int eventType, CancellationToken cancellationToken = default)
    {
        using var connection = _db.CreateConnection();
        connection.Open();

        return await connection.QueryFirstOrDefaultAsync<RCS_AgvOutboundQueue>(new CommandDefinition(
            @"
            SELECT TOP 1 *
            FROM RCS_AgvOutboundQueue
            WHERE TaskNumber = @TaskNumber
              AND EventType = @EventType
            ORDER BY ID DESC;",
            new
            {
                TaskNumber = taskNumber,
                EventType = eventType
            },
            cancellationToken: cancellationToken));
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
