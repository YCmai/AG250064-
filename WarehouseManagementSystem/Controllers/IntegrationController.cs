using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Services;

namespace WarehouseManagementSystem.Controllers
{
    /// <summary>
    /// 对外系统集成统一入口模板。
    /// 推荐用于 MES、ERP、WCS、RCS、第三方平台等系统的 WebAPI 对接。
    /// </summary>
    [ApiController]
    [Route("api/integrations")]
    public class IntegrationController : ControllerBase
    {
        private readonly ILogger<IntegrationController> _logger;

        public IntegrationController(ILogger<IntegrationController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 健康检查。
        /// GET /api/integrations/ping
        /// </summary>
        [HttpGet("ping")]
        [AllowAnonymous]
        public IActionResult Ping()
        {
            return Ok(ApiResponseHelper.Success(new
            {
                status = "ok",
                serverTime = DateTime.Now
            }, "集成接口可用"));
        }

        /// <summary>
        /// 外部系统统一数据接收入口。
        /// 推荐方法名：ReceiveExternalSystemData
        /// POST /api/integrations/receive/{systemCode}
        /// </summary>
        [HttpPost("receive/{systemCode}")]
        [AllowAnonymous]
        public IActionResult ReceiveExternalSystemData(string systemCode, [FromBody] JsonElement payload)
        {
            _logger.LogInformation(
                "收到外部系统通用数据，SystemCode={SystemCode}, Payload={Payload}",
                systemCode,
                payload.ToString());

            return Ok(ApiResponseHelper.Success(new IntegrationAckResponse
            {
                SystemCode = systemCode,
                Action = "receive",
                BusinessCode = string.Empty,
                Accepted = true,
                ReceivedAt = DateTime.Now,
                TraceId = HttpContext.TraceIdentifier
            }, "外部系统数据已接收"));
        }

        /// <summary>
        /// 外部系统订单/单据同步入口模板。
        /// POST /api/integrations/{systemCode}/orders/sync
        /// </summary>
        [HttpPost("{systemCode}/orders/sync")]
        [AllowAnonymous]
        public IActionResult SyncOrder(string systemCode, [FromBody] IntegrationOrderSyncRequest request)
        {
            _logger.LogInformation(
                "收到订单同步请求，SystemCode={SystemCode}, OrderCode={OrderCode}, OrderType={OrderType}",
                systemCode,
                request.OrderCode,
                request.OrderType);

            return Ok(ApiResponseHelper.Success(new IntegrationAckResponse
            {
                SystemCode = systemCode,
                Action = "order-sync",
                BusinessCode = request.OrderCode,
                Accepted = true,
                ReceivedAt = DateTime.Now,
                TraceId = HttpContext.TraceIdentifier
            }, "订单同步请求已接收"));
        }

        /// <summary>
        /// 向外部系统推送任务/通知的模板入口。
        /// POST /api/integrations/{systemCode}/tasks/push
        /// </summary>
        [HttpPost("{systemCode}/tasks/push")]
        [Authorize]
        public IActionResult PushTaskToExternalSystem(string systemCode, [FromBody] IntegrationTaskPushRequest request)
        {
            _logger.LogInformation(
                "准备推送任务到外部系统，SystemCode={SystemCode}, TaskCode={TaskCode}, TaskType={TaskType}",
                systemCode,
                request.TaskCode,
                request.TaskType);

            return Ok(ApiResponseHelper.Success(new IntegrationDispatchResponse
            {
                SystemCode = systemCode,
                Action = "task-push",
                BusinessCode = request.TaskCode,
                DispatchStatus = "queued",
                DispatchTime = DateTime.Now,
                TraceId = HttpContext.TraceIdentifier
            }, "任务推送请求已受理"));
        }

        /// <summary>
        /// 外部系统回调统一入口模板。
        /// POST /api/integrations/callback/{systemCode}
        /// </summary>
        [HttpPost("callback/{systemCode}")]
        [AllowAnonymous]
        public IActionResult ReceiveCallback(string systemCode, [FromBody] IntegrationCallbackRequest request)
        {
            _logger.LogInformation(
                "收到外部系统回调，SystemCode={SystemCode}, BusinessCode={BusinessCode}, Status={Status}",
                systemCode,
                request.BusinessCode,
                request.Status);

            return Ok(ApiResponseHelper.Success(new IntegrationAckResponse
            {
                SystemCode = systemCode,
                Action = "callback",
                BusinessCode = request.BusinessCode,
                Accepted = true,
                ReceivedAt = DateTime.Now,
                TraceId = HttpContext.TraceIdentifier
            }, "回调数据已接收"));
        }

        /// <summary>
        /// 查询集成业务状态模板。
        /// GET /api/integrations/{systemCode}/status/{businessCode}
        /// </summary>
        [HttpGet("{systemCode}/status/{businessCode}")]
        [Authorize]
        public IActionResult QueryIntegrationStatus(string systemCode, string businessCode)
        {
            _logger.LogInformation(
                "查询集成状态，SystemCode={SystemCode}, BusinessCode={BusinessCode}",
                systemCode,
                businessCode);

            return Ok(ApiResponseHelper.Success(new IntegrationStatusResponse
            {
                SystemCode = systemCode,
                BusinessCode = businessCode,
                Status = "pending",
                Message = "模板接口，待接入实际业务状态",
                UpdatedAt = DateTime.Now,
                TraceId = HttpContext.TraceIdentifier
            }, "状态查询成功"));
        }
    }

    public class IntegrationOrderSyncRequest
    {
        public string OrderCode { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public string SourceSystem { get; set; } = string.Empty;
        public JsonElement Payload { get; set; }
    }

    public class IntegrationTaskPushRequest
    {
        public string TaskCode { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
        public string TargetSystem { get; set; } = string.Empty;
        public JsonElement Payload { get; set; }
    }

    public class IntegrationCallbackRequest
    {
        public string BusinessCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public JsonElement Payload { get; set; }
    }

    public class IntegrationAckResponse
    {
        public string SystemCode { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string BusinessCode { get; set; } = string.Empty;
        public bool Accepted { get; set; }
        public DateTime ReceivedAt { get; set; }
        public string TraceId { get; set; } = string.Empty;
    }

    public class IntegrationDispatchResponse
    {
        public string SystemCode { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string BusinessCode { get; set; } = string.Empty;
        public string DispatchStatus { get; set; } = string.Empty;
        public DateTime DispatchTime { get; set; }
        public string TraceId { get; set; } = string.Empty;
    }

    public class IntegrationStatusResponse
    {
        public string SystemCode { get; set; } = string.Empty;
        public string BusinessCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public string TraceId { get; set; } = string.Empty;
    }
}
