using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Models;
using WarehouseManagementSystem.Services.Tasks;

namespace WarehouseManagementSystem.Controllers;

/// <summary>
/// PDA 扫码绑定控制器。
/// </summary>
[ApiController]
[Route("api/pda-bindings")]
public class ApiPdaBindingController : ControllerBase
{
    private readonly IPdaBindingService _pdaBindingService;
    private readonly ILogger<ApiPdaBindingController> _logger;

    public ApiPdaBindingController(
        IPdaBindingService pdaBindingService,
        ILogger<ApiPdaBindingController> logger)
    {
        _pdaBindingService = pdaBindingService;
        _logger = logger;
    }

    /// <summary>
    /// 获取 PDA 可选工单下拉列表。
    /// </summary>
    [HttpGet("work-orders")]
    public async Task<ActionResult<ApiResponse<List<PdaWorkOrderOption>>>> GetWorkOrderOptions(CancellationToken cancellationToken)
    {
        try
        {
            var items = await _pdaBindingService.GetWorkOrderOptionsAsync(cancellationToken);
            return Ok(ApiResponseHelper.Success(items, "获取工单列表成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 PDA 工单列表失败");
            return StatusCode(500, ApiResponseHelper.Failure<List<PdaWorkOrderOption>>("获取工单列表失败"));
        }
    }

    /// <summary>
    /// 提交 PDA 扫码绑定。
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<PdaBindingResponse>>> CreateBinding(
        [FromBody] CreatePdaBindingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _pdaBindingService.BindAsync(new PdaBindingRequest
            {
                OrderNumber = request.OrderNumber,
                PalletNumber = request.PalletNumber,
                Barcode = request.Barcode
            }, cancellationToken);

            if (!result.Success)
            {
                return BadRequest(ApiResponseHelper.Failure<PdaBindingResponse>(result.Message));
            }

            return Ok(ApiResponseHelper.Success(new PdaBindingResponse
            {
                BindingId = result.BindingId,
                TaskId = result.TaskId,
                RequestCode = result.RequestCode
            }, "绑定成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDA 绑定提交失败");
            return StatusCode(500, ApiResponseHelper.Failure<PdaBindingResponse>("PDA 绑定提交失败"));
        }
    }
}

/// <summary>
/// PDA 绑定创建请求。
/// </summary>
public sealed class CreatePdaBindingRequest
{
    /// <summary>工单号。</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>托盘号。</summary>
    public string PalletNumber { get; set; } = string.Empty;

    /// <summary>SSCC码。</summary>
    public string Barcode { get; set; } = string.Empty;
}

/// <summary>
/// PDA 绑定响应。
/// </summary>
public sealed class PdaBindingResponse
{
    /// <summary>绑定记录 ID。</summary>
    public int BindingId { get; set; }

    /// <summary>任务 ID。</summary>
    public int TaskId { get; set; }

    /// <summary>任务请求号。</summary>
    public string RequestCode { get; set; } = string.Empty;
}
