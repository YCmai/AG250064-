using System.Data;
using System.Net.Http.Headers;
using System.Text;
using Dapper;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models.Rcs;

namespace WarehouseManagementSystem.Services.Rcs;

/// <summary>
/// WMS 交互主服务。
/// 业务层只需要依赖这一个接口，不需要再分别关心 DTO、HTTP、日志回写和重试细节。
/// </summary>
public interface IRcsWmsService
{
    /// <summary>
    /// 只插入“物料到达生产线信息”到业务表和请求日志表，不立即发送到 WMS。
    /// </summary>
    /// <param name="request">物料到达生产线请求数据。</param>
    /// <returns>新插入业务记录的主键 ID。</returns>
    Task<int> InsertMaterialArrivalAsync(RcsWmsMaterialArrivalCreateRequest request);

    /// <summary>
    /// 只插入“安全信号”到业务表和请求日志表，不立即发送到 WMS。
    /// </summary>
    /// <param name="request">安全信号请求数据。</param>
    /// <returns>新插入业务记录的主键 ID。</returns>
    Task<int> InsertSafetySignalAsync(RcsWmsSafetySignalCreateRequest request);

    /// <summary>
    /// 只插入“作业完成反馈”到业务表和请求日志表，不立即发送到 WMS。
    /// </summary>
    /// <param name="request">作业完成反馈请求数据。</param>
    /// <returns>新插入业务记录的主键 ID。</returns>
    Task<int> InsertJobFeedbackAsync(RcsWmsJobFeedbackCreateRequest request);

    /// <summary>
    /// 根据物料到达生产线业务记录 ID 组装 JSON 并发送到 WMS，同时回写请求日志。
    /// </summary>
    /// <param name="materialArrivalId">物料到达生产线业务表主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>统一的发送结果。</returns>
    Task<RcsWmsDispatchResult> SendMaterialArrivalAsync(int materialArrivalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据安全信号业务记录 ID 组装 JSON 并发送到 WMS，同时回写请求日志。
    /// 如果 WMS 返回不安全，结果会带回下次重试时间。
    /// </summary>
    /// <param name="safetySignalId">安全信号业务表主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>统一的发送结果。</returns>
    Task<RcsWmsDispatchResult> SendSafetySignalAsync(int safetySignalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据作业完成反馈业务记录 ID 组装 JSON 并发送到 WMS，同时回写请求日志。
    /// </summary>
    /// <param name="jobFeedbackId">作业完成反馈业务表主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>统一的发送结果。</returns>
    Task<RcsWmsDispatchResult> SendJobFeedbackAsync(int jobFeedbackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 先插入“物料到达生产线信息”，再立即发送到 WMS。
    /// 这是业务层最常用的快捷入口。
    /// </summary>
    /// <param name="request">物料到达生产线请求数据。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>插入后的主键 ID 和发送结果。</returns>
    Task<(int Id, RcsWmsDispatchResult Result)> InsertAndSendMaterialArrivalAsync(RcsWmsMaterialArrivalCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 先插入“安全信号”，再立即发送到 WMS。
    /// 如果 WMS 返回不安全，后台服务会继续按配置重试。
    /// </summary>
    /// <param name="request">安全信号请求数据。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>插入后的主键 ID 和发送结果。</returns>
    Task<(int Id, RcsWmsDispatchResult Result)> InsertAndSendSafetySignalAsync(RcsWmsSafetySignalCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 先插入“作业完成反馈”，再立即发送到 WMS。
    /// </summary>
    /// <param name="request">作业完成反馈请求数据。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>插入后的主键 ID 和发送结果。</returns>
    Task<(int Id, RcsWmsDispatchResult Result)> InsertAndSendJobFeedbackAsync(RcsWmsJobFeedbackCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 扫描并处理所有到期的安全信号重试任务。
    /// 这个方法主要给后台 HostedService 调用，业务层通常不需要手动调用。
    /// </summary>
    /// <param name="batchSize">本次最多处理多少条记录。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ProcessDueSafetySignalsAsync(int batchSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// WMS 交互实现。
/// 内部统一处理：
/// 1. 业务表插入
/// 2. 请求日志插入与回写
/// 3. HTTP 请求发送
/// 4. 安全信号自动重试所需的查询与状态流转
/// </summary>
public sealed class RcsWmsService : IRcsWmsService
{
    private readonly IDatabaseService _db;
    private readonly HttpClient _httpClient;
    private readonly IOptions<RcsWmsOptions> _options;
    private readonly ILogger<RcsWmsService> _logger;

    public RcsWmsService(
        IDatabaseService db,
        HttpClient httpClient,
        IOptions<RcsWmsOptions> options,
        ILogger<RcsWmsService> logger)
    {
        _db = db;
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 只插入“物料到达生产线信息”，不立即发送。
    /// </summary>
    public async Task<int> InsertMaterialArrivalAsync(RcsWmsMaterialArrivalCreateRequest request)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var now = DateTime.Now;
            var requestLogId = await InsertRequestLogAsync(
                connection,
                transaction,
                RcsWmsBusinessType.MaterialArrival,
                $"{request.OrderNumber}_{request.PalletNumber}",
                null,
                request.OrderNumber,
                request.RequestJson,
                now);

            const string insertSql = @"
INSERT INTO [RCS_WmsMaterialArrival]
(
    [RequestLogId],
    [OrderNumber],
    [PalletNumber],
    [CreateTime]
)
VALUES
(
    @RequestLogId,
    @OrderNumber,
    @PalletNumber,
    @CreateTime
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var id = await connection.ExecuteScalarAsync<int>(
                insertSql,
                new
                {
                    RequestLogId = requestLogId,
                    OrderNumber = request.OrderNumber.Trim(),
                    PalletNumber = request.PalletNumber.Trim(),
                    CreateTime = now
                },
                transaction);

            if (request.Barcodes.Any(x => !string.IsNullOrWhiteSpace(x)))
            {
                const string insertItemSql = @"
INSERT INTO [RCS_WmsMaterialArrivalItems]
(
    [MaterialArrivalId],
    [Barcode]
)
VALUES
(
    @MaterialArrivalId,
    @Barcode
);";

                var items = request.Barcodes
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => new
                    {
                        MaterialArrivalId = id,
                        Barcode = x.Trim()
                    })
                    .ToList();

                await connection.ExecuteAsync(insertItemSql, items, transaction);
            }

            transaction.Commit();
            return id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 只插入“安全信号”，不立即发送。
    /// </summary>
    public async Task<int> InsertSafetySignalAsync(RcsWmsSafetySignalCreateRequest request)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var now = DateTime.Now;
            var requestLogId = await InsertRequestLogAsync(
                connection,
                transaction,
                RcsWmsBusinessType.SafetySignal,
                request.TaskNumber,
                request.TaskNumber,
                null,
                request.RequestJson,
                now);

            const string insertSql = @"
INSERT INTO [RCS_WmsSafetySignal]
(
    [RequestLogId],
    [TaskNumber],
    [RequestDate],
    [Room],
    [SafeFlag],
    [CreateTime]
)
VALUES
(
    @RequestLogId,
    @TaskNumber,
    @RequestDate,
    @Room,
    @SafeFlag,
    @CreateTime
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var id = await connection.ExecuteScalarAsync<int>(
                insertSql,
                new
                {
                    RequestLogId = requestLogId,
                    TaskNumber = request.TaskNumber.Trim(),
                    RequestDate = request.RequestDate,
                    Room = request.Room.Trim(),
                    SafeFlag = (string?)null,
                    CreateTime = now
                },
                transaction);

            transaction.Commit();
            return id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 只插入“作业完成反馈”，不立即发送。
    /// </summary>
    public async Task<int> InsertJobFeedbackAsync(RcsWmsJobFeedbackCreateRequest request)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var now = DateTime.Now;
            var requestLogId = await InsertRequestLogAsync(
                connection,
                transaction,
                RcsWmsBusinessType.JobFeedback,
                request.TaskNumber,
                request.TaskNumber,
                null,
                request.RequestJson,
                now);

            const string insertSql = @"
INSERT INTO [RCS_WmsJobFeedback]
(
    [RequestLogId],
    [TaskNumber],
    [Status],
    [CreateTime]
)
VALUES
(
    @RequestLogId,
    @TaskNumber,
    @Status,
    @CreateTime
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var id = await connection.ExecuteScalarAsync<int>(
                insertSql,
                new
                {
                    RequestLogId = requestLogId,
                    TaskNumber = request.TaskNumber.Trim(),
                    Status = request.Status.Trim(),
                    CreateTime = now
                },
                transaction);

            transaction.Commit();
            return id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<(int Id, RcsWmsDispatchResult Result)> InsertAndSendMaterialArrivalAsync(RcsWmsMaterialArrivalCreateRequest request, CancellationToken cancellationToken = default)
    {
        var id = await InsertMaterialArrivalAsync(request);
        var result = await SendMaterialArrivalAsync(id, cancellationToken);
        return (id, result);
    }

    public async Task<(int Id, RcsWmsDispatchResult Result)> InsertAndSendSafetySignalAsync(RcsWmsSafetySignalCreateRequest request, CancellationToken cancellationToken = default)
    {
        var id = await InsertSafetySignalAsync(request);
        var result = await SendSafetySignalAsync(id, cancellationToken);
        return (id, result);
    }

    public async Task<(int Id, RcsWmsDispatchResult Result)> InsertAndSendJobFeedbackAsync(RcsWmsJobFeedbackCreateRequest request, CancellationToken cancellationToken = default)
    {
        var id = await InsertJobFeedbackAsync(request);
        var result = await SendJobFeedbackAsync(id, cancellationToken);
        return (id, result);
    }

    /// <summary>
    /// 发送“物料到达生产线信息”。
    /// </summary>
    public async Task<RcsWmsDispatchResult> SendMaterialArrivalAsync(int materialArrivalId, CancellationToken cancellationToken = default)
    {
        var arrival = await GetMaterialArrivalByIdAsync(materialArrivalId);
        if (arrival == null)
        {
            return NotFound($"未找到物料到达记录，ID={materialArrivalId}");
        }

        var items = await GetMaterialArrivalItemsAsync(arrival.ID);
        var request = new RcsWmsMaterialArrivalRequestDto
        {
            OrderNumber = arrival.OrderNumber,
            PalletNumber = arrival.PalletNumber,
            Items = items.Select(x => new RcsWmsMaterialArrivalItemDto { Barcode = x.Barcode }).ToList()
        };

        return await SendAndUpdateAsync(
            arrival.RequestLogId,
            request,
            _options.Value.MaterialArrivalEndpoint,
            RcsWmsBusinessType.MaterialArrival,
            onSuccess: null,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 发送“安全信号”。
    /// 只有 flag=0 且 safeFlag=Y 才算真正成功。
    /// </summary>
    public async Task<RcsWmsDispatchResult> SendSafetySignalAsync(int safetySignalId, CancellationToken cancellationToken = default)
    {
        var signal = await GetSafetySignalByIdAsync(safetySignalId);
        if (signal == null)
        {
            return NotFound($"未找到安全信号记录，ID={safetySignalId}");
        }

        var request = new RcsWmsSafetySignalRequestDto
        {
            TaskNumber = signal.TaskNumber,
            RequestDate = signal.RequestDate.ToString("yyyyMMddHHmmss"),
            Room = signal.Room
        };

        return await SendAndUpdateAsync(
            signal.RequestLogId,
            request,
            _options.Value.SafetySignalEndpoint,
            RcsWmsBusinessType.SafetySignal,
            async result =>
            {
                var safeFlag = string.IsNullOrWhiteSpace(result.SafeFlag) ? null : result.SafeFlag.Trim().ToUpperInvariant();
                await UpdateSafetySignalResultAsync(signal.ID, safeFlag);
            },
            cancellationToken);
    }

    /// <summary>
    /// 发送“作业完成反馈”。
    /// </summary>
    public async Task<RcsWmsDispatchResult> SendJobFeedbackAsync(int jobFeedbackId, CancellationToken cancellationToken = default)
    {
        var feedback = await GetJobFeedbackByIdAsync(jobFeedbackId);
        if (feedback == null)
        {
            return NotFound($"未找到作业完成反馈记录，ID={jobFeedbackId}");
        }

        var request = new RcsWmsJobFeedbackRequestDto
        {
            TaskNumber = feedback.TaskNumber,
            Status = feedback.Status
        };

        return await SendAndUpdateAsync(
            feedback.RequestLogId,
            request,
            _options.Value.JobFeedbackEndpoint,
            RcsWmsBusinessType.JobFeedback,
            onSuccess: null,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 处理所有到期的安全信号重试。
    /// 后台服务只需要调这个方法。
    /// </summary>
    public async Task ProcessDueSafetySignalsAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var logs = await GetDueSafetySignalRequestLogsAsync(DateTime.Now, batchSize);

        foreach (var log in logs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var signal = await GetSafetySignalByRequestLogIdAsync(log.ID);
            if (signal == null)
            {
                _logger.LogWarning("安全信号重试记录缺少业务数据，RequestLogId={RequestLogId}", log.ID);
                await UpdateRequestLogResultAsync(log.ID, RcsWmsRequestStatus.Failed, errorMsg: "未找到对应的安全信号业务记录");
                continue;
            }

            var result = await SendSafetySignalAsync(signal.ID, cancellationToken);
            if (result.Success)
            {
                _logger.LogInformation("安全信号收到安全响应，停止重试。TaskNumber={TaskNumber}", signal.TaskNumber);
            }
        }
    }

    private async Task<RcsWmsDispatchResult> SendAndUpdateAsync<TRequest>(
        int requestLogId,
        TRequest request,
        string endpoint,
        RcsWmsBusinessType businessType,
        Func<RcsWmsDispatchResult, Task>? onSuccess,
        CancellationToken cancellationToken)
    {
        var requestJson = JsonConvert.SerializeObject(request);
        await MarkRequestPreparingAsync(requestLogId, requestJson, null);

        try
        {
            var (requestUrl, responseJson, response) = await PostAsync(endpoint, request, cancellationToken);
            var result = BuildDispatchResult(businessType, response, responseJson);

            if (result.Success && onSuccess != null)
            {
                await onSuccess(result);
            }

            await UpdateRequestLogResultAsync(
                requestLogId,
                result.Success ? RcsWmsRequestStatus.Success : GetPendingStatus(businessType, result),
                responseJson,
                result.ErrorMsg,
                requestUrl: requestUrl,
                nextRetryTime: result.NextRetryTime);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "调用 WMS 接口失败，BusinessType={BusinessType}, RequestLogId={RequestLogId}", businessType, requestLogId);
            var nextRetryTime = businessType == RcsWmsBusinessType.SafetySignal
                ? DateTime.Now.AddSeconds(GetSafetyRetryIntervalSeconds())
                : (DateTime?)null;

            await UpdateRequestLogResultAsync(
                requestLogId,
                businessType == RcsWmsBusinessType.SafetySignal ? RcsWmsRequestStatus.PendingRetry : RcsWmsRequestStatus.Failed,
                errorMsg: ex.Message,
                nextRetryTime: nextRetryTime);

            return new RcsWmsDispatchResult
            {
                Success = false,
                Flag = "-1",
                ErrorMsg = ex.Message,
                NextRetryTime = nextRetryTime
            };
        }
    }

    private async Task<(string RequestUrl, string ResponseJson, RcsWmsResponseDto Response)> PostAsync<TRequest>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var requestUrl = BuildRequestUrl(endpoint);
        var requestJson = JsonConvert.SerializeObject(request);

        using var message = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        message.Headers.Accept.Clear();
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _logger.LogInformation("开始调用 WMS 接口: {RequestUrl}", requestUrl);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(responseJson))
        {
            responseJson = JsonConvert.SerializeObject(new RcsWmsResponseDto
            {
                Flag = "-1",
                ErrorMsg = $"WMS 接口返回空响应，HTTP {response.StatusCode}"
            });
        }

        RcsWmsResponseDto? responseDto;
        try
        {
            responseDto = responseJson.Contains("\"safeFlag\"", StringComparison.OrdinalIgnoreCase)
                ? JsonConvert.DeserializeObject<RcsWmsSafetySignalResponseDto>(responseJson)
                : JsonConvert.DeserializeObject<RcsWmsResponseDto>(responseJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析 WMS 响应失败，地址: {RequestUrl}，原始响应: {ResponseJson}", requestUrl, responseJson);
            responseDto = new RcsWmsResponseDto
            {
                Flag = "-1",
                ErrorMsg = "WMS 返回内容不是合法 JSON"
            };
        }

        responseDto ??= new RcsWmsResponseDto
        {
            Flag = "-1",
            ErrorMsg = $"WMS 接口返回空对象，HTTP {response.StatusCode}"
        };

        if (!response.IsSuccessStatusCode && responseDto.IsSuccess)
        {
            responseDto.Flag = "-1";
            responseDto.ErrorMsg = $"WMS HTTP 状态码异常: {(int)response.StatusCode}";
        }

        return (requestUrl, responseJson, responseDto);
    }

    private RcsWmsDispatchResult BuildDispatchResult(
        RcsWmsBusinessType businessType,
        RcsWmsResponseDto response,
        string responseJson)
    {
        if (businessType != RcsWmsBusinessType.SafetySignal)
        {
            return new RcsWmsDispatchResult
            {
                Success = response.IsSuccess,
                Flag = response.Flag,
                ErrorMsg = response.ErrorMsg,
                ResponseJson = responseJson
            };
        }

        var safetyResponse = response as RcsWmsSafetySignalResponseDto;
        var safeFlag = safetyResponse?.SafeFlag?.Trim().ToUpperInvariant();

        if (!response.IsSuccess)
        {
            return new RcsWmsDispatchResult
            {
                Success = false,
                Flag = response.Flag,
                ErrorMsg = response.ErrorMsg,
                ResponseJson = responseJson,
                SafeFlag = safeFlag,
                NextRetryTime = DateTime.Now.AddSeconds(GetSafetyRetryIntervalSeconds())
            };
        }

        if (string.Equals(safeFlag, "Y", StringComparison.OrdinalIgnoreCase))
        {
            return new RcsWmsDispatchResult
            {
                Success = true,
                Flag = response.Flag,
                ErrorMsg = response.ErrorMsg,
                ResponseJson = responseJson,
                SafeFlag = safeFlag
            };
        }

        return new RcsWmsDispatchResult
        {
            Success = false,
            Flag = response.Flag,
            ErrorMsg = string.IsNullOrWhiteSpace(response.ErrorMsg) ? "WMS 返回未安全，等待 30 秒后重试" : response.ErrorMsg,
            ResponseJson = responseJson,
            SafeFlag = safeFlag,
            NextRetryTime = DateTime.Now.AddSeconds(GetSafetyRetryIntervalSeconds())
        };
    }

    private static RcsWmsRequestStatus GetPendingStatus(RcsWmsBusinessType businessType, RcsWmsDispatchResult result)
    {
        return businessType == RcsWmsBusinessType.SafetySignal && result.NextRetryTime.HasValue
            ? RcsWmsRequestStatus.PendingRetry
            : RcsWmsRequestStatus.Failed;
    }

    private int GetSafetyRetryIntervalSeconds()
    {
        return Math.Max(1, _options.Value.SafetyRetryIntervalSeconds);
    }

    private string BuildRequestUrl(string endpoint)
    {
        var options = _options.Value;

        if (!options.Enabled)
        {
            throw new InvalidOperationException("RcsWmsOutbound:Enabled 当前为 false。");
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException("RcsWmsOutbound:BaseUrl 未配置。");
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("WMS 接口地址未配置。");
        }

        return $"{options.BaseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";
    }

    private static RcsWmsDispatchResult NotFound(string message) => new()
    {
        Success = false,
        Flag = "-1",
        ErrorMsg = message
    };

    private async Task MarkRequestPreparingAsync(int requestLogId, string? requestJson, string? requestUrl)
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
UPDATE [RCS_WmsRequestLog]
SET
    [RequestJson] = COALESCE(@RequestJson, [RequestJson]),
    [RequestUrl] = COALESCE(@RequestUrl, [RequestUrl]),
    [RetryCount] = [RetryCount] + 1,
    [LastRequestTime] = @LastRequestTime,
    [UpdateTime] = @UpdateTime
WHERE [ID] = @ID";

        await connection.ExecuteAsync(sql, new
        {
            ID = requestLogId,
            RequestJson = requestJson,
            RequestUrl = requestUrl,
            LastRequestTime = DateTime.Now,
            UpdateTime = DateTime.Now
        });
    }

    private async Task UpdateRequestLogResultAsync(
        int requestLogId,
        RcsWmsRequestStatus requestStatus,
        string? responseJson = null,
        string? errorMsg = null,
        string? requestUrl = null,
        int? retryCount = null,
        DateTime? nextRetryTime = null)
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
UPDATE [RCS_WmsRequestLog]
SET
    [RequestStatus] = @RequestStatus,
    [RequestUrl] = COALESCE(@RequestUrl, [RequestUrl]),
    [ResponseJson] = @ResponseJson,
    [ErrorMsg] = @ErrorMsg,
    [RetryCount] = COALESCE(@RetryCount, [RetryCount]),
    [LastResponseTime] = @LastResponseTime,
    [NextRetryTime] = @NextRetryTime,
    [UpdateTime] = @UpdateTime
WHERE [ID] = @ID";

        await connection.ExecuteAsync(sql, new
        {
            ID = requestLogId,
            RequestStatus = (int)requestStatus,
            RequestUrl = requestUrl,
            ResponseJson = responseJson,
            ErrorMsg = errorMsg,
            RetryCount = retryCount,
            LastResponseTime = DateTime.Now,
            NextRetryTime = nextRetryTime,
            UpdateTime = DateTime.Now
        });
    }

    private async Task<int> InsertRequestLogAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        RcsWmsBusinessType businessType,
        string businessKey,
        string? taskNumber,
        string? orderNumber,
        string? requestJson,
        DateTime now)
    {
        const string insertSql = @"
INSERT INTO [RCS_WmsRequestLog]
(
    [BusinessType],
    [BusinessKey],
    [TaskNumber],
    [OrderNumber],
    [RequestUrl],
    [RequestJson],
    [ResponseJson],
    [RequestStatus],
    [RetryCount],
    [LastRequestTime],
    [LastResponseTime],
    [NextRetryTime],
    [ErrorMsg],
    [CreateTime],
    [UpdateTime]
)
VALUES
(
    @BusinessType,
    @BusinessKey,
    @TaskNumber,
    @OrderNumber,
    @RequestUrl,
    @RequestJson,
    @ResponseJson,
    @RequestStatus,
    @RetryCount,
    @LastRequestTime,
    @LastResponseTime,
    @NextRetryTime,
    @ErrorMsg,
    @CreateTime,
    @UpdateTime
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

        return await connection.ExecuteScalarAsync<int>(insertSql, new
        {
            BusinessType = (int)businessType,
            BusinessKey = businessKey,
            TaskNumber = taskNumber,
            OrderNumber = orderNumber,
            RequestUrl = (string?)null,
            RequestJson = requestJson,
            ResponseJson = (string?)null,
            RequestStatus = (int)RcsWmsRequestStatus.Pending,
            RetryCount = 0,
            LastRequestTime = now,
            LastResponseTime = (DateTime?)null,
            NextRetryTime = (DateTime?)null,
            ErrorMsg = (string?)null,
            CreateTime = now,
            UpdateTime = now
        }, transaction);
    }

    private async Task<List<RCS_WmsRequestLog>> GetDueSafetySignalRequestLogsAsync(DateTime now, int take)
    {
        using var connection = _db.CreateConnection();
        var items = await connection.QueryAsync<RCS_WmsRequestLog>(
            @"
SELECT TOP (@Take) *
FROM [RCS_WmsRequestLog]
WHERE [BusinessType] = @BusinessType
  AND [RequestStatus] IN @Statuses
  AND ([NextRetryTime] IS NULL OR [NextRetryTime] <= @Now)
ORDER BY COALESCE([NextRetryTime], [CreateTime]), [ID]",
            new
            {
                Take = take,
                BusinessType = (int)RcsWmsBusinessType.SafetySignal,
                Statuses = new[] { (int)RcsWmsRequestStatus.Pending, (int)RcsWmsRequestStatus.PendingRetry },
                Now = now
            });
        return items.ToList();
    }

    private async Task<RCS_WmsMaterialArrival?> GetMaterialArrivalByIdAsync(int id)
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<RCS_WmsMaterialArrival>(
            "SELECT * FROM [RCS_WmsMaterialArrival] WHERE [ID] = @ID",
            new { ID = id });
    }

    private async Task<List<RCS_WmsMaterialArrivalItem>> GetMaterialArrivalItemsAsync(int materialArrivalId)
    {
        using var connection = _db.CreateConnection();
        var items = await connection.QueryAsync<RCS_WmsMaterialArrivalItem>(
            "SELECT * FROM [RCS_WmsMaterialArrivalItems] WHERE [MaterialArrivalId] = @MaterialArrivalId ORDER BY [ID]",
            new { MaterialArrivalId = materialArrivalId });
        return items.ToList();
    }

    private async Task<RCS_WmsSafetySignal?> GetSafetySignalByIdAsync(int id)
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<RCS_WmsSafetySignal>(
            "SELECT * FROM [RCS_WmsSafetySignal] WHERE [ID] = @ID",
            new { ID = id });
    }

    private async Task<RCS_WmsSafetySignal?> GetSafetySignalByRequestLogIdAsync(int requestLogId)
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<RCS_WmsSafetySignal>(
            "SELECT TOP 1 * FROM [RCS_WmsSafetySignal] WHERE [RequestLogId] = @RequestLogId ORDER BY [ID] DESC",
            new { RequestLogId = requestLogId });
    }

    private async Task UpdateSafetySignalResultAsync(int id, string? safeFlag)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE [RCS_WmsSafetySignal] SET [SafeFlag] = @SafeFlag WHERE [ID] = @ID",
            new { ID = id, SafeFlag = safeFlag });
    }

    private async Task<RCS_WmsJobFeedback?> GetJobFeedbackByIdAsync(int id)
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<RCS_WmsJobFeedback>(
            "SELECT * FROM [RCS_WmsJobFeedback] WHERE [ID] = @ID",
            new { ID = id });
    }
}
