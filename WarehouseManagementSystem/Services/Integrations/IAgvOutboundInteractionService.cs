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
    /// 入队“PDA 绑定完成回传”。
    /// </summary>
    Task<(bool Success, string ErrorMessage)> NotifyPdaBindingCompletedAsync(
        RCS_PdaTaskBinding binding,
        CancellationToken cancellationToken = default);

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
    private const string JobCompletedEndpointKey = "AgvUpstream:JobCompletedEndpoint";
    private const string PdaBindingCompletedEndpointKey = "AgvUpstream:PdaBindingCompletedEndpoint";

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

    public async Task<(bool Success, string ErrorMessage)> NotifyPdaBindingCompletedAsync(
        RCS_PdaTaskBinding binding,
        CancellationToken cancellationToken = default)
    {
        if (binding == null)
        {
            return (false, "PDA绑定记录不能为空");
        }

        var orderNumber = binding.OrderNumber?.Trim();
        var palletNumber = binding.PalletNumber?.Trim();
        var barcode = binding.Barcode?.Trim();
        var requestCode = binding.RequestCode?.Trim();

        if (string.IsNullOrWhiteSpace(orderNumber) ||
            string.IsNullOrWhiteSpace(palletNumber) ||
            string.IsNullOrWhiteSpace(barcode) ||
            string.IsNullOrWhiteSpace(requestCode))
        {
            return (false, "PDA绑定回传字段不完整");
        }

        var payload = new
        {
            orderNumber,
            palletNumber,
            items = new[]
            {
                new
                {
                    barcode
                }
            }
        };

        var businessKey = $"pda-binding:{orderNumber}:{palletNumber}:{barcode}:{requestCode}";

        try
        {
            var exists = await _queueRepository.ExistsByBusinessKeyAsync(businessKey, cancellationToken);
            if (exists)
            {
                _logger.LogInformation(
                    "PDA绑定完成回传入队跳过：已存在相同业务键。RequestCode={RequestCode}, BusinessKey={BusinessKey}",
                    requestCode,
                    businessKey);
                return (true, string.Empty);
            }

            await EnqueueAsync(
                (int)AgvOutboundEventType.PdaBindingCompleted,
                businessKey,
                requestCode,
                payload,
                cancellationToken);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDA绑定完成回传入队失败，RequestCode={RequestCode}", requestCode);
            return (false, ex.Message);
        }
    }

    public async Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var maxRetryCount = _configuration.GetValue<int?>("AgvUpstream:MaxRetryCount") ?? 10;
        var pendingTasks = await LoadPendingOutboundTasksAsync(batchSize, maxRetryCount, cancellationToken);

        foreach (var pendingTask in pendingTasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DispatchOutboundTaskAsync(pendingTask, maxRetryCount, cancellationToken);
        }

        return pendingTasks.Count;
    }

    /// <summary>
    /// Why: 后台线程本质上只是“按批次取待发送任务”，把取数动作单独提出来后，
    /// 现场后续如果想改批次大小、查询条件或切换到 ECS 表，优先只需要看这里。
    /// </summary>
    private Task<List<RCS_AgvOutboundQueue>> LoadPendingOutboundTasksAsync(
        int batchSize,
        int maxRetryCount,
        CancellationToken cancellationToken)
    {
        return _queueRepository.GetPendingAsync(batchSize, maxRetryCount, DateTime.Now, cancellationToken);
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
    /// Why: 出站线程的主干逻辑应该让人一眼看懂，就是“按表中类型分发接口，再按结果回写状态”。
    /// 因此这里不再把分发、发送、回写混在一起，后续维护者重点只需要看这个方法。
    /// </summary>
    private async Task DispatchOutboundTaskAsync(
        RCS_AgvOutboundQueue item,
        int maxRetryCount,
        CancellationToken cancellationToken)
    {
        if (item.RetryCount >= maxRetryCount)
        {
            await UpdateOutboundTaskResultAsync(
                item,
                new OutboundDispatchResult(false, item.LastError, true),
                maxRetryCount,
                cancellationToken);
            return;
        }

        var dispatchTarget = ResolveDispatchTarget(item.EventType);
        if (!dispatchTarget.IsValid)
        {
            await UpdateOutboundTaskResultAsync(
                item,
                new OutboundDispatchResult(false, dispatchTarget.ErrorMessage),
                maxRetryCount,
                cancellationToken);
            return;
        }

        var dispatchResult = await SendOutboundRequestAsync(dispatchTarget.Endpoint, item.RequestBody, cancellationToken);
        await UpdateOutboundTaskResultAsync(item, dispatchResult, maxRetryCount, cancellationToken);
    }

    /// <summary>
    /// Why: 这里统一根据发送结果回写表状态。
    /// 成功、失败待重试、失败终态三种分支都在这一处，后续查历史或改重试策略也更集中。
    /// </summary>
    private async Task UpdateOutboundTaskResultAsync(
        RCS_AgvOutboundQueue item,
        OutboundDispatchResult dispatchResult,
        int maxRetryCount,
        CancellationToken cancellationToken)
    {
        if (dispatchResult.Success)
        {
            await _queueRepository.MarkSuccessAsync(item.ID, DateTime.Now, cancellationToken);
            return;
        }

        if (dispatchResult.MarkAsAbandoned || item.RetryCount >= maxRetryCount)
        {
            await _queueRepository.MarkAbandonedAsync(
                item.ID,
                item.RetryCount,
                dispatchResult.ErrorMessage,
                DateTime.Now,
                cancellationToken);
            return;
        }

        var nextRetryCount = item.RetryCount + 1;
        if (nextRetryCount >= maxRetryCount)
        {
            await _queueRepository.MarkAbandonedAsync(
                item.ID,
                nextRetryCount,
                dispatchResult.ErrorMessage,
                DateTime.Now,
                cancellationToken);
            return;
        }

        var nextRetrySeconds = Math.Min(300, Math.Max(5, nextRetryCount * 10));
        await _queueRepository.MarkFailedAsync(
            item.ID,
            nextRetryCount,
            dispatchResult.ErrorMessage,
            DateTime.Now.AddSeconds(nextRetrySeconds),
            cancellationToken);
    }

    /// <summary>
    /// Why: 表里只有事件类型，没有复杂流程。
    /// 所以这里就直接做“类型 -> 接口地址”的简单分发，保持和现场理解一致。
    /// </summary>
    private DispatchTarget ResolveDispatchTarget(int eventType)
    {
        var endpointKey = eventType switch
        {
            (int)AgvOutboundEventType.MaterialArrived => MaterialArrivedEndpointKey,
            (int)AgvOutboundEventType.JobCompleted => JobCompletedEndpointKey,
            (int)AgvOutboundEventType.PdaBindingCompleted => PdaBindingCompletedEndpointKey,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(endpointKey))
        {
            return DispatchTarget.Fail($"不支持的事件类型，EventType={eventType}");
        }

        var endpoint = (_configuration[endpointKey] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return DispatchTarget.Fail($"接口未配置，EventType={eventType}");
        }

        return DispatchTarget.Ok(endpoint);
    }

    /// <summary>
    /// 发送 HTTP 请求并依据 flag（0成功，-1失败）解析上位机回包。
    /// </summary>
    private async Task<OutboundDispatchResult> SendOutboundRequestAsync(
        string endpoint,
        string requestBody,
        CancellationToken cancellationToken)
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
                return new OutboundDispatchResult(false, $"HttpStatus={(int)response.StatusCode}, Body={responseText}");
            }

            AgvUpstreamAckResponse? ack;
            try
            {
                ack = JsonSerializer.Deserialize<AgvUpstreamAckResponse>(responseText, _jsonOptions);
            }
            catch
            {
                return new OutboundDispatchResult(false, $"响应JSON解析失败, Body={responseText}");
            }

            if (ack?.Flag == 0)
            {
                return new OutboundDispatchResult(true, string.Empty);
            }

            return new OutboundDispatchResult(false, $"Flag={ack?.Flag}, ErrorMsg={ack?.ErrorMsg}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new OutboundDispatchResult(false, "请求超时");
        }
        catch (Exception ex)
        {
            return new OutboundDispatchResult(false, ex.Message);
        }
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

    private sealed record DispatchTarget(bool IsValid, string Endpoint, string ErrorMessage)
    {
        public static DispatchTarget Ok(string endpoint) => new(true, endpoint, string.Empty);

        public static DispatchTarget Fail(string errorMessage) => new(false, string.Empty, errorMessage);
    }

    private sealed record OutboundDispatchResult(bool Success, string ErrorMessage, bool MarkAsAbandoned = false);
}

/// <summary>
/// 出站事件类型。
/// </summary>
public enum AgvOutboundEventType
{
    /// <summary>物料达到生产线信息。</summary>
    MaterialArrived = 1,
    /// <summary>作业完成反馈。</summary>
    JobCompleted = 3,
    /// <summary>PDA 绑定完成回传。</summary>
    PdaBindingCompleted = 4
}
