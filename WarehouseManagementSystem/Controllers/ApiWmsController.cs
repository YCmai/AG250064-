using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Models;
using WarehouseManagementSystem.Models.Rcs;
using WarehouseManagementSystem.Services;
using WarehouseManagementSystem.Services.Rcs;

namespace WarehouseManagementSystem.Controllers;

/// <summary>
/// WMS 交互统一控制器。
/// 这里集中提供示例查看、插库、发送、插库后立即发送等入口，
/// 业务层和联调时都优先看这个控制器即可。
/// </summary>
[ApiController]
[Route("api/wms")]
public class ApiWmsController : ControllerBase
{
    private readonly IRcsWmsService _rcsWmsService;
    private readonly ILogger<ApiWmsController> _logger;

    public ApiWmsController(IRcsWmsService rcsWmsService, ILogger<ApiWmsController> logger)
    {
        _rcsWmsService = rcsWmsService;
        _logger = logger;
    }

    /// <summary>
    /// 获取 WMS 对接说明和标准示例 JSON。
    /// 用于联调时直接查看当前系统约定的请求结构和调用方式。
    /// </summary>
    [HttpGet("examples")]
    public ActionResult<ApiResponse<object>> GetExamples()
    {
        var data = new
        {
            mode = new
            {
                description = "当前系统同时支持“只插库”和“插库后立即发送”两种模式。安全信号在首次发送后，如未返回安全，会由后台服务继续重试。",
                insertOnlyMethods = new[]
                {
                    "InsertMaterialArrivalAsync",
                    "InsertSafetySignalAsync",
                    "InsertJobFeedbackAsync"
                },
                insertAndSendMethods = new[]
                {
                    "InsertAndSendMaterialArrivalAsync",
                    "InsertAndSendSafetySignalAsync",
                    "InsertAndSendJobFeedbackAsync"
                },
                backgroundRetry = "仅安全信号有后台自动重试，其它两类不会在插库后自动扫描发送。"
            },
            materialArrival = new
            {
                insertAndSendUrl = "/api/wms/material-arrival/insert-and-send",
                insertOnlyUrl = "/api/wms/material-arrival/insert",
                sendByIdUrl = "/api/wms/material-arrival/{id}/send",
                body = new
                {
                    orderNumber = "20260402000001",
                    palletNumber = "PLT000000000000001",
                    barcodes = new[] { "123456789012", "123456789013" }
                },
                wmsRequestJson = new
                {
                    orderNumber = "20260402000001",
                    palletNumber = "PLT000000000000001",
                    items = new[]
                    {
                        new { barcode = "123456789012" },
                        new { barcode = "123456789013" }
                    }
                }
            },
            safetySignal = new
            {
                insertAndSendUrl = "/api/wms/safety-signal/insert-and-send",
                insertOnlyUrl = "/api/wms/safety-signal/insert",
                sendByIdUrl = "/api/wms/safety-signal/{id}/send",
                body = new
                {
                    taskNumber = "AGV_TASK_202604020001",
                    requestDate = "2026-04-02T14:30:00",
                    room = "WEIGH01"
                },
                wmsRequestJson = new
                {
                    taskNumber = "AGV_TASK_202604020001",
                    requestDate = "20260402143000",
                    room = "WEIGH01"
                }
            },
            jobFeedback = new
            {
                insertAndSendUrl = "/api/wms/job-feedback/insert-and-send",
                insertOnlyUrl = "/api/wms/job-feedback/insert",
                sendByIdUrl = "/api/wms/job-feedback/{id}/send",
                body = new
                {
                    taskNumber = "AGV_TASK_202604020001",
                    status = "1"
                },
                wmsRequestJson = new
                {
                    taskNumber = "AGV_TASK_202604020001",
                    status = "1"
                }
            }
        };

        return Ok(ApiResponseHelper.Success<object>(data, "获取 WMS 示例成功"));
    }

    /// <summary>
    /// 只插入物料到达生产线记录，不立即发送到 WMS。
    /// </summary>
    [HttpPost("material-arrival/insert")]
    public async Task<ActionResult<ApiResponse<object>>> InsertMaterialArrival([FromBody] RcsWmsMaterialArrivalCreateRequest request)
    {
        try
        {
            var id = await _rcsWmsService.InsertMaterialArrivalAsync(request);
            return Ok(ApiResponseHelper.Success<object>(new { id }, "物料到达生产线记录插入成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "插入物料到达生产线记录失败");
            return StatusCode(500, ApiResponseHelper.Failure<object>($"插入物料到达生产线记录失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 插入物料到达生产线记录后，立即发送到 WMS。
    /// </summary>
    [HttpPost("material-arrival/insert-and-send")]
    public async Task<ActionResult<ApiResponse<object>>> InsertAndSendMaterialArrival([FromBody] RcsWmsMaterialArrivalCreateRequest request)
    {
        try
        {
            var result = await _rcsWmsService.InsertAndSendMaterialArrivalAsync(request);
            return Ok(ApiResponseHelper.Success<object>(new { id = result.Id, dispatch = result.Result }, "物料到达生产线发送完成"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "插入并发送物料到达生产线失败");
            return StatusCode(500, ApiResponseHelper.Failure<object>($"插入并发送物料到达生产线失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 根据业务记录 ID 发送物料到达生产线信息到 WMS。
    /// </summary>
    [HttpPost("material-arrival/{id:int}/send")]
    public async Task<ActionResult<ApiResponse<RcsWmsDispatchResult>>> SendMaterialArrival(int id)
    {
        try
        {
            var result = await _rcsWmsService.SendMaterialArrivalAsync(id);
            return Ok(ApiResponseHelper.Success(result, "物料到达生产线发送完成"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送物料到达生产线失败, ID={Id}", id);
            return StatusCode(500, ApiResponseHelper.Failure<RcsWmsDispatchResult>($"发送物料到达生产线失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 只插入安全信号记录，不立即发送到 WMS。
    /// </summary>
    [HttpPost("safety-signal/insert")]
    public async Task<ActionResult<ApiResponse<object>>> InsertSafetySignal([FromBody] RcsWmsSafetySignalCreateRequest request)
    {
        try
        {
            var id = await _rcsWmsService.InsertSafetySignalAsync(request);
            return Ok(ApiResponseHelper.Success<object>(new { id }, "安全信号记录插入成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "插入安全信号记录失败");
            return StatusCode(500, ApiResponseHelper.Failure<object>($"插入安全信号记录失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 插入安全信号记录后，立即发送到 WMS。
    /// 如果首发未返回安全，后台服务会继续按配置重试。
    /// </summary>
    [HttpPost("safety-signal/insert-and-send")]
    public async Task<ActionResult<ApiResponse<object>>> InsertAndSendSafetySignal([FromBody] RcsWmsSafetySignalCreateRequest request)
    {
        try
        {
            var result = await _rcsWmsService.InsertAndSendSafetySignalAsync(request);
            return Ok(ApiResponseHelper.Success<object>(new { id = result.Id, dispatch = result.Result }, "安全信号发送完成"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "插入并发送安全信号失败");
            return StatusCode(500, ApiResponseHelper.Failure<object>($"插入并发送安全信号失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 根据业务记录 ID 发送安全信号到 WMS。
    /// </summary>
    [HttpPost("safety-signal/{id:int}/send")]
    public async Task<ActionResult<ApiResponse<RcsWmsDispatchResult>>> SendSafetySignal(int id)
    {
        try
        {
            var result = await _rcsWmsService.SendSafetySignalAsync(id);
            return Ok(ApiResponseHelper.Success(result, "安全信号发送完成"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送安全信号失败, ID={Id}", id);
            return StatusCode(500, ApiResponseHelper.Failure<RcsWmsDispatchResult>($"发送安全信号失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 只插入作业完成反馈记录，不立即发送到 WMS。
    /// </summary>
    [HttpPost("job-feedback/insert")]
    public async Task<ActionResult<ApiResponse<object>>> InsertJobFeedback([FromBody] RcsWmsJobFeedbackCreateRequest request)
    {
        try
        {
            var id = await _rcsWmsService.InsertJobFeedbackAsync(request);
            return Ok(ApiResponseHelper.Success<object>(new { id }, "作业完成反馈记录插入成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "插入作业完成反馈记录失败");
            return StatusCode(500, ApiResponseHelper.Failure<object>($"插入作业完成反馈记录失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 插入作业完成反馈记录后，立即发送到 WMS。
    /// </summary>
    [HttpPost("job-feedback/insert-and-send")]
    public async Task<ActionResult<ApiResponse<object>>> InsertAndSendJobFeedback([FromBody] RcsWmsJobFeedbackCreateRequest request)
    {
        try
        {
            var result = await _rcsWmsService.InsertAndSendJobFeedbackAsync(request);
            return Ok(ApiResponseHelper.Success<object>(new { id = result.Id, dispatch = result.Result }, "作业完成反馈发送完成"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "插入并发送作业完成反馈失败");
            return StatusCode(500, ApiResponseHelper.Failure<object>($"插入并发送作业完成反馈失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 根据业务记录 ID 发送作业完成反馈到 WMS。
    /// </summary>
    [HttpPost("job-feedback/{id:int}/send")]
    public async Task<ActionResult<ApiResponse<RcsWmsDispatchResult>>> SendJobFeedback(int id)
    {
        try
        {
            var result = await _rcsWmsService.SendJobFeedbackAsync(id);
            return Ok(ApiResponseHelper.Success(result, "作业完成反馈发送完成"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送作业完成反馈失败, ID={Id}", id);
            return StatusCode(500, ApiResponseHelper.Failure<RcsWmsDispatchResult>($"发送作业完成反馈失败: {ex.Message}"));
        }
    }
}
