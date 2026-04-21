using System.Text;
using System.Text.Json;
using WarehouseManagementSystem.Models;
using WarehouseManagementSystem.Models.Ndc;

namespace WarehouseManagementSystem.Services.Integrations;

/// <summary>
/// AGV 主动上报服务。
/// 负责把上报数据写入统一出站表，并由后台线程异步发送给上位机。
/// </summary>
public interface IAgvOutboundInteractionService
{
    /// <summary>
    /// 入队“物料达到生产线信息”。
    /// </summary>
    Task NotifyMaterialArrivedAsync(NdcUserTask userTask, CancellationToken cancellationToken = default);

    /// <summary>
    /// 入队“作业完成反馈”。
    /// </summary>
    Task NotifyJobCompletedAsync(string taskNumber, int status, CancellationToken cancellationToken = default);

    /// <summary>
    /// 入队“安全信号”。
    /// </summary>
    Task NotifySafetySignalAsync(string taskNumber, DateTime requestDate, string room, CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理统一出站表中的待发送记录。
    /// </summary>
    Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// AGV 主动上报服务实现。
/// </summary>
public sealed class AgvOutboundInteractionService : IAgvOutboundInteractionService
{
    private const string MaterialArrivedEndpointKey = "AgvUpstream:MaterialArrivedEndpoint";
    private const string SafetySignalEndpointKey = "AgvUpstream:SafetySignalEndpoint";
    private const string JobCompletedEndpointKey = "AgvUpstream:JobCompletedEndpoint";

    private readonly IAgvOutboundQueueRepository _queueRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgvOutboundInteractionService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AgvOutboundInteractionService(
        IAgvOutboundQueueRepository queueRepository,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AgvOutboundInteractionService> logger)
    {
        _queueRepository = queueRepository ?? throw new ArgumentNullException(nameof(queueRepository));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyMaterialArrivedAsync(NdcUserTask userTask, CancellationToken cancellationToken = default)
    {
        if (userTask == null)
        {
            _logger.LogWarning("跳过物料到达入队：任务对象为空");
            return;
        }

        var orderNumber = ResolveOrderNumber(userTask);
        var palletNumber = userTask.palletNo?.Trim();
        var barcodes = ParseBarcodes(userTask.binNumber);

        if (string.IsNullOrWhiteSpace(orderNumber) || string.IsNullOrWhiteSpace(palletNumber) || barcodes.Count == 0)
        {
            _logger.LogWarning(
                "跳过物料到达入队：字段不完整，OrderNumber={OrderNumber}, PalletNumber={PalletNumber}, BarcodeCount={BarcodeCount}",
                orderNumber,
                palletNumber,
                barcodes.Count);
            return;
        }

        var payload = new
        {
            orderNumber,
            palletNumber,
            items = barcodes.Select(x => new { barcode = x }).ToList()
        };

        var businessKey = $"material:{orderNumber}:{palletNumber}:{string.Join(",", barcodes)}";
        await EnqueueAsync((int)AgvOutboundEventType.MaterialArrived, businessKey, orderNumber, payload, cancellationToken);
    }

    public async Task NotifyJobCompletedAsync(string taskNumber, int status, CancellationToken cancellationToken = default)
    {
        var normalizedTaskNumber = taskNumber?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTaskNumber))
        {
            _logger.LogWarning("跳过作业完成入队：taskNumber为空");
            return;
        }

        if (status != 1 && status != 2)
        {
            _logger.LogWarning("跳过作业完成入队：status非法，TaskNumber={TaskNumber}, Status={Status}", normalizedTaskNumber, status);
            return;
        }

        var payload = new
        {
            taskNumber = normalizedTaskNumber,
            status
        };

        var businessKey = $"job:{normalizedTaskNumber}:{status}";
        await EnqueueAsync((int)AgvOutboundEventType.JobCompleted, businessKey, normalizedTaskNumber, payload, cancellationToken);
    }

    public async Task NotifySafetySignalAsync(string taskNumber, DateTime requestDate, string room, CancellationToken cancellationToken = default)
    {
        var normalizedTaskNumber = taskNumber?.Trim();
        var normalizedRoom = room?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedTaskNumber) || string.IsNullOrWhiteSpace(normalizedRoom))
        {
            _logger.LogWarning("跳过安全信号入队：taskNumber或room为空");
            return;
        }

        var payload = new
        {
            taskNumber = normalizedTaskNumber,
            requestDate = requestDate.ToString("yyyy-MM-dd HH:mm:ss"),
            room = normalizedRoom
        };

        var businessKey = $"safe:{normalizedTaskNumber}:{requestDate:yyyyMMddHHmmss}:{normalizedRoom}";
        await EnqueueAsync((int)AgvOutboundEventType.SafetySignal, businessKey, normalizedTaskNumber, payload, cancellationToken);
    }

    public async Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var maxRetryCount = _configuration.GetValue<int?>("AgvUpstream:MaxRetryCount") ?? 10;
        var tasks = await _queueRepository.GetPendingAsync(batchSize, maxRetryCount, DateTime.Now, cancellationToken);

        foreach (var item in tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessSingleAsync(item, maxRetryCount, cancellationToken);
        }

        return tasks.Count;
    }

    /// <summary>
    /// 统一入队入口：先按业务键幂等检查，再写入统一出站表。
    /// </summary>
    private async Task EnqueueAsync(int eventType, string businessKey, string taskNumber, object payload, CancellationToken cancellationToken)
    {
        try
        {
            var exists = await _queueRepository.ExistsByBusinessKeyAsync(businessKey, cancellationToken);
            if (exists)
            {
                _logger.LogInformation(
                    "AGV主动上报入队跳过：已存在相同业务键。EventType={EventType}, TaskNumber={TaskNumber}, BusinessKey={BusinessKey}",
                    eventType,
                    taskNumber,
                    businessKey);
                return;
            }

            var now = DateTime.Now;
            var entity = new RCS_AgvOutboundQueue
            {
                EventType = eventType,
                TaskNumber = taskNumber,
                BusinessKey = businessKey,
                RequestBody = JsonSerializer.Serialize(payload, _jsonOptions),
                ProcessStatus = 0,
                RetryCount = 0,
                LastError = string.Empty,
                NextRetryTime = null,
                CreateTime = now,
                ProcessTime = null,
                UpdateTime = now
            };

            await _queueRepository.InsertAsync(entity, cancellationToken);
            _logger.LogInformation(
                "AGV主动上报入队成功。EventType={EventType}, TaskNumber={TaskNumber}, BusinessKey={BusinessKey}, CreateTime={CreateTime:yyyy-MM-dd HH:mm:ss}",
                eventType,
                taskNumber,
                businessKey,
                now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AGV主动上报入队失败，EventType={EventType}, BusinessKey={BusinessKey}", eventType, businessKey);
        }
    }

    /// <summary>
    /// 处理单条出站记录：发送成功标记完成，发送失败回写错误并计算下次重试时间。
    /// </summary>
    private async Task ProcessSingleAsync(RCS_AgvOutboundQueue item, int maxRetryCount, CancellationToken cancellationToken)
    {
        if (item.RetryCount >= maxRetryCount)
        {
            await _queueRepository.MarkAbandonedAsync(
                item.ID,
                item.RetryCount,
                item.LastError,
                DateTime.Now,
                cancellationToken);
            return;
        }

        var endpoint = GetEndpointByEventType(item.EventType);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            await MarkFailedWithRetryPolicyAsync(
                item,
                maxRetryCount,
                $"接口未配置，EventType={item.EventType}",
                cancellationToken);
            return;
        }

        var sendResult = await PostWithAckAsync(endpoint, item.RequestBody, cancellationToken);
        if (sendResult.Success)
        {
            await _queueRepository.MarkSuccessAsync(item.ID, DateTime.Now, cancellationToken);
            return;
        }

        await MarkFailedWithRetryPolicyAsync(item, maxRetryCount, sendResult.ErrorMsg, cancellationToken);
    }

    /// <summary>
    /// 失败回写策略：未达上限标记为“失败待重试(2)”，达到上限标记为“失败终态(3)”。
    /// </summary>
    private async Task MarkFailedWithRetryPolicyAsync(
        RCS_AgvOutboundQueue item,
        int maxRetryCount,
        string errorMsg,
        CancellationToken cancellationToken)
    {
        var nextRetryCount = item.RetryCount + 1;
        if (nextRetryCount >= maxRetryCount)
        {
            await _queueRepository.MarkAbandonedAsync(
                item.ID,
                nextRetryCount,
                errorMsg,
                DateTime.Now,
                cancellationToken);
            return;
        }

        var nextRetrySeconds = Math.Min(300, Math.Max(5, nextRetryCount * 10));
        await _queueRepository.MarkFailedAsync(
            item.ID,
            nextRetryCount,
            errorMsg,
            DateTime.Now.AddSeconds(nextRetrySeconds),
            cancellationToken);
    }

    /// <summary>
    /// 发送 HTTP 请求并依据 flag（0成功，-1失败）解析上位机回包。
    /// </summary>
    private async Task<(bool Success, string ErrorMsg)> PostWithAckAsync(string endpoint, string requestBody, CancellationToken cancellationToken)
    {
        try
        {
            var timeoutSeconds = _configuration.GetValue<int?>("AgvUpstream:TimeoutSeconds") ?? 10;
            using var httpClient = _httpClientFactory.CreateClient(nameof(AgvOutboundInteractionService));
            httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(endpoint, content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return (false, $"HttpStatus={(int)response.StatusCode}, Body={responseText}");
            }

            AgvUpstreamAckResponse? ack;
            try
            {
                ack = JsonSerializer.Deserialize<AgvUpstreamAckResponse>(responseText, _jsonOptions);
            }
            catch
            {
                return (false, $"响应JSON解析失败, Body={responseText}");
            }

            if (ack?.Flag == 0)
            {
                return (true, string.Empty);
            }

            return (false, $"Flag={ack?.Flag}, ErrorMsg={ack?.ErrorMsg}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, "请求超时");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// 根据事件类型读取对应的上位机配置地址。
    /// </summary>
    private string GetEndpointByEventType(int eventType)
    {
        var key = eventType switch
        {
            (int)AgvOutboundEventType.MaterialArrived => MaterialArrivedEndpointKey,
            (int)AgvOutboundEventType.SafetySignal => SafetySignalEndpointKey,
            (int)AgvOutboundEventType.JobCompleted => JobCompletedEndpointKey,
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(key) ? string.Empty : (_configuration[key] ?? string.Empty).Trim();
    }

    /// <summary>
    /// 解析物料到达上报里的订单号：优先 taskGroupNo，回退 requestCode。
    /// </summary>
    private static string ResolveOrderNumber(NdcUserTask userTask)
    {
        if (!string.IsNullOrWhiteSpace(userTask.taskGroupNo))
        {
            return userTask.taskGroupNo.Trim();
        }

        return userTask.requestCode?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// 把 binNumber 按逗号/分号/竖线拆分为条码集合，并去重。
    /// </summary>
    private static List<string> ParseBarcodes(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<string>();
        }

        return raw
            .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed class AgvUpstreamAckResponse
    {
        public int Flag { get; set; }
        public string ErrorMsg { get; set; } = string.Empty;
    }
}

/// <summary>
/// 出站事件类型。
/// </summary>
public enum AgvOutboundEventType
{
    /// <summary>物料达到生产线信息。</summary>
    MaterialArrived = 1,
    /// <summary>安全信号。</summary>
    SafetySignal = 2,
    /// <summary>作业完成反馈。</summary>
    JobCompleted = 3
}
