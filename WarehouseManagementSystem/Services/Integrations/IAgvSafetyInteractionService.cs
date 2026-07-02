using System.Text;
using System.Text.Json;
using Dapper;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models;

namespace WarehouseManagementSystem.Services.Integrations;

/// <summary>
/// AGV 安全交互服务。
/// </summary>
public interface IAgvSafetyInteractionService
{
    /// <summary>
    /// 处理一次安全交互握手，并返回当前是否允许 NDC 继续执行。
    /// </summary>
    /// <param name="taskNumber">任务号。</param>
    /// <param name="requestDate">业务请求时间。</param>
    /// <param name="room">安全交互区域。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>当前安全握手判定结果。</returns>
    Task<AgvSafetyCheckResult> CheckSafetyAsync(
        string taskNumber,
        DateTime requestDate,
        string room,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 安全交互仓储接口。
/// </summary>
public interface IAgvSafetyHandshakeRepository
{
    /// <summary>
    /// 按任务号和房间查询当前安全交互状态。
    /// </summary>
    Task<RCS_AgvSafetyHandshake?> GetByTaskNumberAndRoomAsync(
        string taskNumber,
        string room,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存安全交互状态。
    /// </summary>
    Task SaveAsync(RCS_AgvSafetyHandshake entity, CancellationToken cancellationToken = default);
}

/// <summary>
/// AGV 安全交互状态仓储实现。
/// </summary>
public sealed class AgvSafetyHandshakeRepository : IAgvSafetyHandshakeRepository
{
    private readonly IDatabaseService _db;

    public AgvSafetyHandshakeRepository(IDatabaseService db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<RCS_AgvSafetyHandshake?> GetByTaskNumberAndRoomAsync(
        string taskNumber,
        string room,
        CancellationToken cancellationToken = default)
    {
        using var connection = _db.CreateConnection();
        connection.Open();

        return await connection.QueryFirstOrDefaultAsync<RCS_AgvSafetyHandshake>(new CommandDefinition(
            @"
SELECT TOP 1 *
FROM RCS_AgvSafetyHandshake
WHERE TaskNumber = @TaskNumber
  AND Room = @Room
ORDER BY ID DESC;",
            new
            {
                TaskNumber = taskNumber,
                Room = room
            },
            cancellationToken: cancellationToken));
    }

    public async Task SaveAsync(RCS_AgvSafetyHandshake entity, CancellationToken cancellationToken = default)
    {
        using var connection = _db.CreateConnection();
        connection.Open();

        if (entity.ID > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"
UPDATE RCS_AgvSafetyHandshake
SET RequestDate = @RequestDate,
    LastRequestTime = @LastRequestTime,
    LastResponseTime = @LastResponseTime,
    SafeFlag = @SafeFlag,
    ProcessStatus = @ProcessStatus,
    RetryCount = @RetryCount,
    ErrorMessage = @ErrorMessage,
    ResponseBody = @ResponseBody,
    UpdateTime = @UpdateTime
WHERE ID = @ID;",
                entity,
                cancellationToken: cancellationToken));
            return;
        }

        var sql = @"
INSERT INTO RCS_AgvSafetyHandshake
(
    TaskNumber,
    Room,
    RequestDate,
    LastRequestTime,
    LastResponseTime,
    SafeFlag,
    ProcessStatus,
    RetryCount,
    ErrorMessage,
    ResponseBody,
    CreateTime,
    UpdateTime
)
VALUES
(
    @TaskNumber,
    @Room,
    @RequestDate,
    @LastRequestTime,
    @LastResponseTime,
    @SafeFlag,
    @ProcessStatus,
    @RetryCount,
    @ErrorMessage,
    @ResponseBody,
    @CreateTime,
    @UpdateTime
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

        entity.ID = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            entity,
            cancellationToken: cancellationToken));
    }
}

/// <summary>
/// AGV 安全交互服务实现。
/// Why: 安全交互不是“发出去就算完成”的普通异步上报，而是要在当前 NDC 步骤里实时判定是否放行。
/// 因此这里采用“直联接口 + 单表保存最近状态”的方式，避免把实时握手和异步出站队列混在一起。
/// </summary>
public sealed class AgvSafetyInteractionService : IAgvSafetyInteractionService
{
    private const string SafetySignalEndpointKey = "AgvUpstream:SafetySignalEndpoint";
    private const int SafeStatus = 1;
    private const int UnsafeStatus = 2;
    private const int FailedStatus = 3;

    private readonly IAgvSafetyHandshakeRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgvSafetyInteractionService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AgvSafetyInteractionService(
        IAgvSafetyHandshakeRepository repository,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AgvSafetyInteractionService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgvSafetyCheckResult> CheckSafetyAsync(
        string taskNumber,
        DateTime requestDate,
        string room,
        CancellationToken cancellationToken = default)
    {
        var normalizedTaskNumber = taskNumber?.Trim() ?? string.Empty;
        var normalizedRoom = room?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedTaskNumber) || string.IsNullOrWhiteSpace(normalizedRoom))
        {
            return AgvSafetyCheckResult.Wait("安全交互参数不完整");
        }

        var now = DateTime.Now;
        var retryIntervalSeconds = _configuration.GetValue<int?>("AgvUpstream:SafetyRetryIntervalSeconds") ?? 30;
        var state = await _repository.GetByTaskNumberAndRoomAsync(normalizedTaskNumber, normalizedRoom, cancellationToken)
            ?? CreateInitialState(normalizedTaskNumber, normalizedRoom, requestDate, now);

        if (state.ProcessStatus == SafeStatus && string.Equals(state.SafeFlag, "Y", StringComparison.OrdinalIgnoreCase))
        {
            return AgvSafetyCheckResult.Pass();
        }

        if (state.LastRequestTime.HasValue)
        {
            var nextRetryTime = state.LastRequestTime.Value.AddSeconds(retryIntervalSeconds);
            if (nextRetryTime > now)
            {
                return AgvSafetyCheckResult.Wait(
                    $"安全交互等待中，{nextRetryTime:HH:mm:ss} 后再重试",
                    nextRetryTime);
            }
        }

        return await SendSafetyRequestAsync(state, cancellationToken);
    }

    private async Task<AgvSafetyCheckResult> SendSafetyRequestAsync(
        RCS_AgvSafetyHandshake state,
        CancellationToken cancellationToken)
    {
        var endpoint = (_configuration[SafetySignalEndpointKey] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            state.ProcessStatus = FailedStatus;
            state.ErrorMessage = "未配置安全交互接口地址";
            state.UpdateTime = DateTime.Now;
            await _repository.SaveAsync(state, cancellationToken);
            return AgvSafetyCheckResult.Wait(state.ErrorMessage);
        }

        var requestTime = DateTime.Now;
        state.LastRequestTime = requestTime;
        state.RetryCount += 1;
        state.ErrorMessage = string.Empty;
        state.UpdateTime = requestTime;

        var payload = new
        {
            taskNumber = state.TaskNumber,
            requestDate = state.RequestDate.ToString("yyyyMMddHHmmss"),
            room = state.Room
        };

        try
        {
            var timeoutSeconds = _configuration.GetValue<int?>("AgvUpstream:TimeoutSeconds") ?? 10;
            using var httpClient = _httpClientFactory.CreateClient(nameof(AgvSafetyInteractionService));
            httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            using var content = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await httpClient.PostAsync(endpoint, content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            state.LastResponseTime = DateTime.Now;
            state.ResponseBody = Truncate(responseText, 4000);
            state.UpdateTime = DateTime.Now;

            if (!response.IsSuccessStatusCode)
            {
                state.ProcessStatus = FailedStatus;
                state.ErrorMessage = $"HttpStatus={(int)response.StatusCode}";
                await _repository.SaveAsync(state, cancellationToken);
                return AgvSafetyCheckResult.Wait(state.ErrorMessage);
            }

            SafetySignalResponse? ack;
            try
            {
                ack = JsonSerializer.Deserialize<SafetySignalResponse>(responseText, _jsonOptions);
            }
            catch
            {
                state.ProcessStatus = FailedStatus;
                state.ErrorMessage = "安全交互响应 JSON 解析失败";
                await _repository.SaveAsync(state, cancellationToken);
                return AgvSafetyCheckResult.Wait(state.ErrorMessage);
            }

            if (ack == null)
            {
                state.ProcessStatus = FailedStatus;
                state.ErrorMessage = "安全交互响应为空";
                await _repository.SaveAsync(state, cancellationToken);
                return AgvSafetyCheckResult.Wait(state.ErrorMessage);
            }

            state.SafeFlag = (ack.SafeFlag ?? string.Empty).Trim().ToUpperInvariant();
            state.ErrorMessage = Truncate(ack.ErrorMsg ?? string.Empty, 512);

            if (ack.Flag == 0 && state.SafeFlag == "Y")
            {
                state.ProcessStatus = SafeStatus;
                await _repository.SaveAsync(state, cancellationToken);
                return AgvSafetyCheckResult.Pass();
            }

            if (ack.Flag == 0 && state.SafeFlag == "N")
            {
                state.ProcessStatus = UnsafeStatus;
                if (string.IsNullOrWhiteSpace(state.ErrorMessage))
                {
                    state.ErrorMessage = "MES 返回当前区域暂不安全";
                }

                await _repository.SaveAsync(state, cancellationToken);
                return AgvSafetyCheckResult.Wait(state.ErrorMessage, requestTime.AddSeconds(
                    _configuration.GetValue<int?>("AgvUpstream:SafetyRetryIntervalSeconds") ?? 30));
            }

            state.ProcessStatus = FailedStatus;
            if (string.IsNullOrWhiteSpace(state.ErrorMessage))
            {
                state.ErrorMessage = $"安全交互返回失败，Flag={ack.Flag}";
            }

            await _repository.SaveAsync(state, cancellationToken);
            return AgvSafetyCheckResult.Wait(state.ErrorMessage);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            state.ProcessStatus = FailedStatus;
            state.ErrorMessage = "安全交互请求超时";
            state.UpdateTime = DateTime.Now;
            await _repository.SaveAsync(state, cancellationToken);
            return AgvSafetyCheckResult.Wait(state.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "安全交互直联请求异常，TaskNumber={TaskNumber}, Room={Room}", state.TaskNumber, state.Room);
            state.ProcessStatus = FailedStatus;
            state.ErrorMessage = Truncate(ex.Message, 512);
            state.UpdateTime = DateTime.Now;
            await _repository.SaveAsync(state, cancellationToken);
            return AgvSafetyCheckResult.Wait(state.ErrorMessage);
        }
    }

    private static RCS_AgvSafetyHandshake CreateInitialState(
        string taskNumber,
        string room,
        DateTime requestDate,
        DateTime now)
    {
        return new RCS_AgvSafetyHandshake
        {
            TaskNumber = taskNumber,
            Room = room,
            RequestDate = requestDate,
            LastRequestTime = null,
            LastResponseTime = null,
            SafeFlag = string.Empty,
            ProcessStatus = 0,
            RetryCount = 0,
            ErrorMessage = string.Empty,
            ResponseBody = string.Empty,
            CreateTime = now,
            UpdateTime = now
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private sealed class SafetySignalResponse
    {
        public int Flag { get; set; }
        public string SafeFlag { get; set; } = string.Empty;
        public string ErrorMsg { get; set; } = string.Empty;
    }
}

/// <summary>
/// 安全交互判定结果。
/// </summary>
public sealed record AgvSafetyCheckResult(bool IsSafeToContinue, string Message, DateTime? NextRetryTime)
{
    public static AgvSafetyCheckResult Pass()
    {
        return new AgvSafetyCheckResult(true, string.Empty, null);
    }

    public static AgvSafetyCheckResult Wait(string message, DateTime? nextRetryTime = null)
    {
        return new AgvSafetyCheckResult(false, message, nextRetryTime);
    }
}
