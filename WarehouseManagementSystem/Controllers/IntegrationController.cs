using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Models.DTOs.Integrations;
using WarehouseManagementSystem.Services.Integrations;
using WarehouseManagementSystem.Services.Tasks;

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
        private readonly IAgvIntegrationService _agvIntegrationService;
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        public IntegrationController(ILogger<IntegrationController> logger, IAgvIntegrationService agvIntegrationService)
        {
            _logger = logger;
            _agvIntegrationService = agvIntegrationService;
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

        /// <summary>
        /// MES 下发工单信息给 AGV。
        /// POST /api/ApiTask/workorder
        /// </summary>
        [HttpPost("/api/ApiTask/workorder")]
        [HttpPost("agv/work-orders")]
        [AllowAnonymous]
        public async Task<IActionResult> ReceiveAgvWorkOrder([FromBody] AgvWorkOrderRequest request, CancellationToken cancellationToken)
        {
            var errors = ValidateAgvWorkOrderRequest(request);
            if (errors.Count > 0)
            {
                var errorMessage = string.Join("; ", errors);
                _logger.LogWarning("AGV工单信息校验失败，Error={Error}, Payload={Payload}", errorMessage, request);
                return Ok(AgvIntegrationResponse.Fail(errorMessage));
            }

            _logger.LogInformation(
                "收到MES下发AGV工单信息，OrderNumber={OrderNumber}, MaterialNumber={MaterialNumber}, MsgType={MsgType}, ReceiveTime={ReceiveTime}",
                request.OrderNumber,
                request.MaterialNumber,
                request.MsgType,
                DateTime.Now.ToString(DateTimeFormat));

            var persistResult = await _agvIntegrationService.SaveWorkOrderAsync(request, cancellationToken);
            return persistResult.Status switch
            {
                AgvPersistStatus.Success => Ok(AgvIntegrationResponse.Success()),
                AgvPersistStatus.Duplicate => Ok(AgvIntegrationResponse.Fail(
                    string.IsNullOrWhiteSpace(persistResult.ErrorMsg) ? "工单已存在，禁止重复下发" : persistResult.ErrorMsg)),
                AgvPersistStatus.Conflict => Ok(AgvIntegrationResponse.Fail(persistResult.ErrorMsg)),
                _ => Ok(AgvIntegrationResponse.Fail(persistResult.ErrorMsg))
            };
        }

        /// <summary>
        /// MES 下发 AGV 指令。
        /// POST /api/ApiTask/agvcommand
        /// </summary>
        [HttpPost("/api/ApiTask/agvcommand")]
        [HttpPost("agv/commands")]
        [AllowAnonymous]
        public async Task<IActionResult> ReceiveAgvCommand([FromBody] AgvCommandRequest request, CancellationToken cancellationToken)
        {
            var errors = ValidateAgvCommandRequest(request);
            if (errors.Count > 0)
            {
                var errorMessage = string.Join("; ", errors);
                _logger.LogWarning("AGV指令校验失败，Error={Error}, Payload={Payload}", errorMessage, request);
                return Ok(AgvIntegrationResponse.Fail(errorMessage));
            }

            _logger.LogInformation(
                "收到MES下发AGV指令，TaskNumber={TaskNumber}, Priority={Priority}, ItemCount={ItemCount}, Items={Items}, Payload={Payload}, ReceiveTime={ReceiveTime}",
                request.TaskNumber,
                request.Priority,
                request.Items.Count,
                string.Join(" | ", request.Items.Select(x =>
                    $"seq={x.Seq},taskType={x.TaskType},from={x.FromStation},to={x.ToStation},pallet={x.PalletNumber},bin={x.BinNumber}")),
                JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }),
                DateTime.Now.ToString(DateTimeFormat));

            var persistResult = await _agvIntegrationService.EnqueueAgvCommandAsync(request, cancellationToken);
            return persistResult.Status switch
            {
                AgvPersistStatus.Success => Ok(AgvIntegrationResponse.Success()),
                AgvPersistStatus.Duplicate => Ok(AgvIntegrationResponse.Fail(
                    string.IsNullOrWhiteSpace(persistResult.ErrorMsg) ? "taskNumber 已存在，禁止重复下发" : persistResult.ErrorMsg)),
                AgvPersistStatus.Conflict => Ok(AgvIntegrationResponse.Fail(persistResult.ErrorMsg)),
                _ => Ok(AgvIntegrationResponse.Fail(persistResult.ErrorMsg))
            };
        }

        /// <summary>
        /// 校验 MES 下发 AGV 工单请求的字段完整性与业务规则。
        /// </summary>
        /// <param name="request">工单请求体。</param>
        /// <returns>错误信息列表；为空表示校验通过。</returns>
        private static List<string> ValidateAgvWorkOrderRequest(AgvWorkOrderRequest? request)
        {
            var errors = new List<string>();
            if (request is null)
            {
                errors.Add("请求体不能为空");
                return errors;
            }

            ValidateRequired(request.OrderNumber, "orderNumber", errors);
            ValidateRequired(request.MaterialNumber, "materialNumber", errors);
            ValidateRequired(request.MaterialName, "materialName", errors);
            ValidateRequired(request.MsgType, "msgType", errors);

                if (!string.IsNullOrWhiteSpace(request.MsgType) && request.MsgType is not ("1" or "2"))
                {
                    errors.Add("msgType 仅支持 1(生效) 或 2(失效)");
                }

            return errors;
        }

        /// <summary>
        /// 校验 MES 下发 AGV 指令请求（含 items 明细）的字段与业务规则。
        /// </summary>
        /// <param name="request">AGV 指令请求体。</param>
        /// <returns>错误信息列表；为空表示校验通过。</returns>
        private static List<string> ValidateAgvCommandRequest(AgvCommandRequest? request)
        {
                var errors = new List<string>();
                if (request is null)
                {
                    errors.Add("请求体不能为空");
                    return errors;
                }

                ValidateRequired(request.TaskNumber, "taskNumber", errors);

                if (request.Priority is null)
                {
                    errors.Add("priority 不能为空");
                }
                else if (request.Priority is < 1 or > 3)
                {
                    errors.Add("priority 仅支持 1(高) / 2(中) / 3(低)");
                }

                if (request.Items is null || request.Items.Count == 0)
                {
                    errors.Add("items 至少需要 1 条明细");
                    return errors;
                }

                var duplicateSeq = request.Items
                    .GroupBy(x => x.Seq)
                    .FirstOrDefault(g => g.Count() > 1);
                if (duplicateSeq is not null)
                {
                    errors.Add($"items 中 seq={duplicateSeq.Key} 重复");
                }

                for (var i = 0; i < request.Items.Count; i++)
                {
                    var item = request.Items[i];
                    var prefix = $"items[{i}]";

                    if (item.Seq <= 0)
                    {
                        errors.Add($"{prefix}.seq 必须大于 0");
                    }

                    if (item.TaskType is null)
                    {
                        errors.Add($"{prefix}.taskType 不能为空");
                        continue;
                    }

                    var taskType = item.TaskType.Value;
                    if (taskType is < 1 or > 5)
                    {
                        errors.Add($"{prefix}.taskType 仅支持 1~5");
                        continue;
                    }

                    ValidateRequired(item.ToStation, $"{prefix}.toStation", errors);

                    if (taskType is 1 or 5 && string.IsNullOrWhiteSpace(item.PalletNumber))
                    {
                        errors.Add($"{prefix}.taskType={taskType} 时 palletNumber 必填");
                    }

                    if (taskType == 3 && string.IsNullOrWhiteSpace(item.BinNumber))
                    {
                        errors.Add($"{prefix}.taskType=3 时 binNumber 必填");
                    }

                    if (taskType is 2 or 4 or 5 && string.IsNullOrWhiteSpace(item.FromStation))
                    {
                        errors.Add($"{prefix}.taskType={taskType} 时 fromStation 必填");
                    }
                }

                return errors;
            }

        /// <summary>
        /// 校验必填字段非空。
        /// </summary>
        /// <param name="value">字段值。</param>
        /// <param name="fieldName">字段名（用于错误信息）。</param>
        /// <param name="errors">错误信息集合。</param>
        private static void ValidateRequired(string? value, string fieldName, ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{fieldName} 不能为空");
            }
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

    public class AgvIntegrationResponse
    {
        public int Flag { get; set; }
        public string ErrorMsg { get; set; } = string.Empty;

        public static AgvIntegrationResponse Success()
        {
            return new AgvIntegrationResponse
            {
                Flag = 0,
                ErrorMsg = string.Empty
            };
        }

        public static AgvIntegrationResponse Fail(string errorMsg)
        {
            return new AgvIntegrationResponse
            {
                Flag = -1,
                ErrorMsg = errorMsg
            };
        }
    }
}
