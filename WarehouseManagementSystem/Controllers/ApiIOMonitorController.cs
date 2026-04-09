using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Models.IO;
using WarehouseManagementSystem.Service.Io;

namespace WarehouseManagementSystem.Controllers
{
    [ApiController]
    [Route("api/iomonitor")]
    public class ApiIOMonitorController : ControllerBase
    {
        private readonly IIODeviceService _deviceService;
        private readonly IIOService _ioService;
        private readonly ILogger<ApiIOMonitorController> _logger;

        public ApiIOMonitorController(
            IIODeviceService deviceService,
            IIOService ioService,
            ILogger<ApiIOMonitorController> logger)
        {
            _deviceService = deviceService;
            _ioService = ioService;
            _logger = logger;
        }

        [HttpGet("devices")]
        public async Task<IActionResult> GetAllDevices()
        {
            try
            {
                var devices = await _deviceService.GetAllDevicesAsync();
                
                return Ok(new { success = true, data = devices });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取IO设备列表失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("signals")]
        public async Task<IActionResult> GetLatestSignals()
        {
            try
            {
                var signals = await _deviceService.GetLatestSignalsAsync();
                return Ok(new { success = true, data = signals });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取IO信号失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("device")]
        public async Task<IActionResult> AddDevice([FromBody] RCS_IODevices device)
        {
            try
            {
                if (device == null)
                    return BadRequest(new { success = false, message = "设备数据不能为空" });

                if (string.IsNullOrWhiteSpace(device.Name))
                    return BadRequest(new { success = false, message = "设备名称不能为空" });

                if (string.IsNullOrWhiteSpace(device.IP))
                    return BadRequest(new { success = false, message = "IP地址不能为空" });

                if (!System.Net.IPAddress.TryParse(device.IP, out _))
                    return BadRequest(new { success = false, message = "无效的IP地址格式" });

                device.CreatedTime = DateTime.Now;
                device.UpdatedTime = DateTime.Now;

                var newDevice = await _deviceService.AddDeviceAsync(device);
                return Ok(new { success = true, data = newDevice });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加IO设备失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("signal")]
        public async Task<IActionResult> AddSignal([FromBody] RCS_IOSignals signal)
        {
            try
            {
                if (signal == null)
                    return BadRequest(new { success = false, message = "信号数据不能为空" });

                if (string.IsNullOrWhiteSpace(signal.Name))
                    return BadRequest(new { success = false, message = "信号名称不能为空" });

                signal.CreatedTime = DateTime.Now;
                signal.UpdatedTime = DateTime.Now;
                signal.Value = 0;

                var signalId = await _deviceService.AddSignalAsync(signal);
                return Ok(new { success = true, data = signalId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加IO信号失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("device/{id}")]
        public async Task<IActionResult> UpdateDevice(int id, [FromBody] RCS_IODevices device)
        {
            try
            {
                if (device == null || device.Id != id)
                    return BadRequest(new { success = false, message = "设备数据无效" });

                device.UpdatedTime = DateTime.Now;
                await _deviceService.UpdateDeviceAsync(device);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新IO设备失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("device/{id}")]
        public async Task<IActionResult> DeleteDevice(int id)
        {
            try
            {
                await _deviceService.DeleteDeviceAsync(id);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除IO设备失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("signal/{id}")]
        public async Task<IActionResult> DeleteSignal(int id)
        {
            try
            {
                await _deviceService.DeleteSignAsync(id);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除IO信号失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("device/{id}/toggle")]
        public async Task<IActionResult> ToggleDevice(int id, [FromBody] System.Text.Json.JsonElement request)
        {
            try
            {
                var device = await _deviceService.GetDeviceByIdAsync(id);
                if (device == null)
                    return NotFound(new { success = false, message = "设备不存在" });

                bool isEnabled;
                if (request.TryGetProperty("isEnabled", out var isEnabledProp))
                {
                    isEnabled = isEnabledProp.GetBoolean();
                }
                else if (request.TryGetProperty("IsEnabled", out var isEnabledPropUpper))
                {
                    isEnabled = isEnabledPropUpper.GetBoolean();
                }
                else 
                {
                    return BadRequest(new { success = false, message = "请求体中缺少 isEnabled 字段" });
                }

                device.IsEnabled = isEnabled;
                device.UpdatedTime = DateTime.Now;
                await _deviceService.UpdateDeviceAsync(device);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "切换IO设备状态失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("signal/read")]
        public async Task<IActionResult> ReadSignal([FromQuery] string ip, [FromQuery] string address)
        {
            try
            {
                if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(address))
                    return BadRequest(new { success = false, message = "IP和地址不能为空" });

                // 尝试解析地址字符串为枚举值
                if (!Enum.TryParse<EIOAddress>(address, out EIOAddress addressEnum))
                {
                    return BadRequest(new { success = false, message = $"无效的地址值: {address}" });
                }

                _logger.LogInformation("正在读取信号: IP={IP}, Address={Address}, User={User}", ip, addressEnum, "User");

                var value = await _ioService.ReadSignal(ip, addressEnum);

                _logger.LogInformation("信号读取成功: IP={IP}, Address={Address}, Value={Value}", ip, addressEnum, value);

                return Ok(new { success = true, data = new { value = value ? 1 : 0, address } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取IO信号失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("signal/write")]
        public async Task<IActionResult> WriteSignal([FromBody] WriteSignalRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { success = false, message = "无效的请求数据" });

                if (string.IsNullOrEmpty(request.IP) || string.IsNullOrEmpty(request.Address))
                    return BadRequest(new { success = false, message = "IP和地址不能为空" });

                // 尝试解析地址字符串为枚举值
                // 如果直接解析失败，尝试去掉可能的"DI"/"DO"前缀后再次解析，或者映射数字
                if (!Enum.TryParse<EIOAddress>(request.Address, true, out EIOAddress addressEnum))
                {
                     // 兼容处理：如果用户输入的是 100.1 这种格式，或者 0, 1, 2 这种数字索引，需要进行转换逻辑
                     // 但根据 EIOAddress 定义，目前只支持 DI1-DI8, DO1-DO8
                     // 这里我们记录日志并返回更详细的错误
                    _logger.LogWarning("无法解析地址: {Address}", request.Address);
                    return BadRequest(new { success = false, message = $"无效的地址值: {request.Address}。有效值示例: DI1, DO1" });
                }

                // 检查是否是输入地址（DI）
                if (addressEnum.ToString().StartsWith("DI"))
                {
                    return BadRequest(new { success = false, message = "DI地址为只读，不能写入" });
                }

                _logger.LogInformation("正在创建写入信号任务: IP={IP}, Address={Address}, Value={Value}",
                    request.IP, addressEnum, request.Value);

                var taskId = await _ioService.AddIOTask(
                    taskType: "ArrivalNotify",
                    deviceIP: request.IP,
                    signalAddress: request.Address,
                    value: request.Value,
                    taskId: $"MANUAL_{DateTime.Now:yyyyMMddHHmmssfff}"
                );

                return Ok(new { success = true, data = taskId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "写入IO信号失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("tasks")]
        public async Task<IActionResult> GetIOTasks()
        {
            try
            {
                var tasks = await _deviceService.GetRCS_IOAGV_TasksAsync();
                return Ok(new { success = true, data = tasks });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取IO任务列表失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("task")]
        public async Task<IActionResult> AddIOTask([FromBody] dynamic task)
        {
            try
            {
                if (task == null)
                    return BadRequest(new { success = false, message = "任务数据不能为空" });

                string taskType = task.taskType;
                string deviceIP = task.deviceIP;
                string signalAddress = task.signalAddress;
                int value = task.value;
                string taskId = task.taskId;

                var newTaskId = await _ioService.AddIOTask(
                    taskType: taskType,
                    deviceIP: deviceIP,
                    signalAddress: signalAddress,
                    value: value > 0,
                    taskId: taskId
                );

                return Ok(new { success = true, data = newTaskId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加IO任务失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}
