using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Models.PLC;
using WarehouseManagementSystem.Service.Plc;
using WarehouseManagementSystem.Db;
using Dapper;
using WarehouseManagementSystem.Models;
using WarehouseManagementSystem.Services.Tasks;

namespace WarehouseManagementSystem.Controllers
{
    /// <summary>
    /// API PLC信号状态控制器，提供REST API PLC信号状态管理接口
    /// </summary>
    [ApiController]
    [Route("api/plc-signal-status")]
    public class ApiPlcSignalStatusController : ControllerBase
    {
        private readonly IPlcSignalService _plcSignalService;
        private readonly ILogger<ApiPlcSignalStatusController> _logger;
        private readonly IDatabaseService _db;

        public ApiPlcSignalStatusController(
            IPlcSignalService plcSignalService,
            ILogger<ApiPlcSignalStatusController> logger,
            IDatabaseService db)
        {
            _plcSignalService = plcSignalService;
            _logger = logger;
            _db = db;
        }

        /// <summary>
        /// 获取所有PLC设备
        /// </summary>
        /// <returns>PLC设备列表</returns>
        [HttpGet("devices")]
        public async Task<ActionResult<ApiResponse<List<PlcDeviceResponse>>>> GetAllDevices()
        {
            _logger.LogInformation("获取所有PLC设备");

            try
            {
                var devices = await _plcSignalService.GetAllPlcDevicesAsync();
                
                var deviceResponses = devices.Select(d => new PlcDeviceResponse
                {
                    Id = d.IpAddress, // 使用IP地址作为ID
                    IpAddress = d.IpAddress,
                    Remark = d.Remark,
                    ModuleAddress = d.ModuleAddress,
                    IsEnabled = d.IsEnabled,
                    SignalCount = d.Signals?.Count ?? 0,
                    LastSignalUpdateTime = d.Signals?
                        .Where(s => s.UpdateTime.HasValue)
                        .Select(s => s.UpdateTime)
                        .OrderByDescending(t => t)
                        .FirstOrDefault()
                }).ToList();

                return Ok(ApiResponseHelper.Success(deviceResponses, "获取PLC设备列表成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取PLC设备列表失败");
                return StatusCode(500, ApiResponseHelper.Failure<List<PlcDeviceResponse>>("获取PLC设备列表失败"));
            }
        }

        /// <summary>
        /// 获取所有PLC信号
        /// </summary>
        /// <returns>PLC信号列表</returns>
        [HttpGet("signals")]
        public async Task<ActionResult<ApiResponse<List<PlcSignalResponse>>>> GetAllPlcSignals()
        {
            _logger.LogInformation("获取所有PLC信号");

            try
            {
                var signals = await _plcSignalService.GetAllPlcSignalsAsync();
                
                var signalResponses = signals.Select(s => new PlcSignalResponse
                {
                    Id = s.Id,
                    Name = s.Name,
                    Address = s.Offset, // 使用 Offset 作为 Address
                    DataType = s.DataType,
                    Value = s.CurrentValue, // 使用 CurrentValue 作为 Value
                    Status = 1, // 默认状态，因为模型中没有 Status 字段
                    LastUpdateTime = s.UpdateTime, // 使用 UpdateTime 作为 LastUpdateTime
                    PlcDeviceId = s.PlcDeviceId,
                    Remark = s.Remark,
                    DbBlock = s.PLCTypeDb,
                    Writer = s.Writer
                }).ToList();

                return Ok(ApiResponseHelper.Success(signalResponses, "获取PLC信号列表成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取PLC信号列表失败");
                return StatusCode(500, ApiResponseHelper.Failure<List<PlcSignalResponse>>("获取PLC信号列表失败"));
            }
        }

        /// <summary>
        /// 获取特定设备的所有信号
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>设备信号列表</returns>
        [HttpGet("signals/{deviceId}")]
        public async Task<ActionResult<ApiResponse<List<PlcSignalResponse>>>> GetSignalsByDevice(string deviceId)
        {
            _logger.LogInformation($"获取设备信号: DeviceId={deviceId}");

            try
            {
                var signals = await _plcSignalService.GetPlcSignalsByDeviceIdAsync(deviceId);
                
                var signalResponses = signals.Select(s => new PlcSignalResponse
                {
                    Id = s.Id,
                    Name = s.Name,
                    Address = s.Offset, // 使用 Offset 作为 Address
                    DataType = s.DataType,
                    Value = s.CurrentValue, // 使用 CurrentValue 作为 Value
                    Status = 1, // 默认状态，因为模型中没有 Status 字段
                    LastUpdateTime = s.UpdateTime, // 使用 UpdateTime 作为 LastUpdateTime
                    PlcDeviceId = s.PlcDeviceId,
                    Remark = s.Remark,
                    DbBlock = s.PLCTypeDb,
                    Writer = s.Writer
                }).ToList();

                return Ok(ApiResponseHelper.Success(signalResponses, "获取设备信号列表成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取设备信号列表失败: DeviceId={deviceId}");
                return StatusCode(500, ApiResponseHelper.Failure<List<PlcSignalResponse>>("获取设备信号列表失败"));
            }
        }

        /// <summary>
        /// 重置PLC信号
        /// </summary>
        /// <param name="request">重置请求</param>
        /// <returns>操作结果</returns>
        [HttpPost("reset")]
        public async Task<ActionResult<ApiResponse>> ResetSignal([FromBody] ResetSignalRequest request)
        {
            _logger.LogInformation($"重置PLC信号: SignalId={request.SignalId}");

            try
            {
                await _plcSignalService.ResetPlcSignalAsync(request.SignalId);
                return Ok(ApiResponseHelper.Success("PLC信号重置成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"重置PLC信号失败: SignalId={request.SignalId}");
                return StatusCode(500, ApiResponseHelper.Failure("重置PLC信号失败"));
            }
        }

        /// <summary>
        /// 手动触发PLC信号
        /// </summary>
        /// <param name="request">触发请求</param>
        /// <returns>操作结果</returns>
        [HttpPost("trigger")]
        public async Task<ActionResult<ApiResponse>> TriggerSignal([FromBody] TriggerSignalRequest request)
        {
            _logger.LogInformation($"手动触发PLC信号: SignalId={request.SignalId}, Value={request.Value}");

            try
            {
                // 获取信号信息
                var signal = await _plcSignalService.GetPlcSignalByIdAsync(request.SignalId);
                if (signal == null)
                {
                    return NotFound(ApiResponseHelper.Failure($"信号ID {request.SignalId} 不存在"));
                }

                // 处理值
                int status = request.Value ? 1 : 2;
                string signalValue = "人工重置";
                
                // 对于字符串类型的信号特殊处理
                if (signal.DataType != null && (signal.DataType.Equals("String", StringComparison.OrdinalIgnoreCase) 
                    || signal.DataType.Equals("string", StringComparison.OrdinalIgnoreCase)))
                {
                    status = request.Value ? 5 : 6;
                    
                    if (status == 5) // 随机字符串
                    {
                        signalValue = Guid.NewGuid().ToString().Substring(0, 8);
                    }
                    else // 空值
                    {
                        signalValue = string.Empty;
                    }
                }

                // 插入RCS_AutoPlcTasks任务
                using (var conn = _db.CreateConnection())
                {
                    string sql = @"INSERT INTO RCS_AutoPlcTasks (OrderCode, Status, IsSend, Signal, CreatingTime, Remark, PlcType, PLCTypeDb)
                                   VALUES (@OrderCode, @Status, @IsSend, @Signal, @CreatingTime, @Remark, @PlcType, @PLCTypeDb)";
                    await conn.ExecuteAsync(sql, new
                    {
                        OrderCode = Guid.NewGuid().ToString(),
                        Status = status,
                        IsSend = 0,
                        Signal = signal.Name,
                        CreatingTime = DateTime.Now,
                        Remark = signalValue,
                        PlcType = signal.PlcDeviceId,
                        PLCTypeDb = signal.PLCTypeDb ?? ""
                    });
                }

                return Ok(ApiResponseHelper.Success("PLC信号触发成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"手动触发PLC信号失败: SignalId={request.SignalId}, Value={request.Value}");
                return StatusCode(500, ApiResponseHelper.Failure($"触发PLC信号失败: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// PLC设备响应模型
    /// </summary>
    public class PlcDeviceResponse
    {
        /// <summary>
        /// 设备ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// IP地址
        /// </summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }

        /// <summary>
        /// 模块地址
        /// </summary>
        public string ModuleAddress { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// 信号数量
        /// </summary>
        public int SignalCount { get; set; }

        /// <summary>
        /// 最近一次信号更新时间
        /// </summary>
        public DateTime? LastSignalUpdateTime { get; set; }
    }

    /// <summary>
    /// PLC信号响应模型
    /// </summary>
    public class PlcSignalResponse
    {
        /// <summary>
        /// 信号ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 信号名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 地址
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// 当前值
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// 状态
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime? LastUpdateTime { get; set; }

        /// <summary>
        /// PLC设备ID
        /// </summary>
        public string PlcDeviceId { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }

        /// <summary>
        /// DB块地址
        /// </summary>
        public string DbBlock { get; set; }

        /// <summary>
        /// 写入者
        /// </summary>
        public string Writer { get; set; }
    }

    /// <summary>
    /// 重置信号请求
    /// </summary>
    public class ResetSignalRequest
    {
        /// <summary>
        /// 信号ID
        /// </summary>
        public int SignalId { get; set; }
    }

    /// <summary>
    /// 触发信号请求
    /// </summary>
    public class TriggerSignalRequest
    {
        /// <summary>
        /// 信号ID
        /// </summary>
        public int SignalId { get; set; }

        /// <summary>
        /// 触发值
        /// </summary>
        public bool Value { get; set; }
    }
}
