using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Models.PLC;
using WarehouseManagementSystem.Service.Plc;

namespace WarehouseManagementSystem.Controllers
{
    [ApiController]
    [Route("api/plcsignal")]
    public class ApiPlcSignalController : ControllerBase
    {
        private readonly IPlcSignalService _plcSignalService;
        private readonly ILogger<ApiPlcSignalController> _logger;

        public ApiPlcSignalController(
            IPlcSignalService plcSignalService,
            ILogger<ApiPlcSignalController> logger)
        {
            _plcSignalService = plcSignalService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllDevices()
        {
            try
            {
                var devices = await _plcSignalService.GetAllPlcDevicesAsync();
                return Ok(new { success = true, data = devices });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取PLC设备列表失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDeviceById(int id)
        {
            try
            {
                var device = await _plcSignalService.GetPlcDeviceByIdAsync(id);
                if (device == null)
                    return NotFound(new { success = false, message = "设备不存在" });
                return Ok(new { success = true, data = device });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取设备详情失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("signals/{deviceId}")]
        public async Task<IActionResult> GetSignalsByDevice(string deviceId, [FromQuery] string dbBlock = null)
        {
            try
            {
                var signals = await _plcSignalService.GetPlcSignalsByDeviceIdAsync(deviceId, dbBlock);
                return Ok(new { success = true, data = signals });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取设备信号失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("signal/{id}")]
        public async Task<IActionResult> GetSignalById(int id)
        {
            try
            {
                var signal = await _plcSignalService.GetPlcSignalByIdAsync(id);
                if (signal == null)
                    return NotFound(new { success = false, message = "信号不存在" });
                return Ok(new { success = true, data = signal });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取信号详情失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("device")]
        public async Task<IActionResult> AddDevice([FromBody] RCS_PlcDevice device)
        {
            try
            {
                if (device == null)
                    return BadRequest(new { success = false, message = "设备数据不能为空" });

                if (string.IsNullOrWhiteSpace(device.IpAddress))
                    return BadRequest(new { success = false, message = "IP地址不能为空" });

                if (!System.Net.IPAddress.TryParse(device.IpAddress, out _))
                    return BadRequest(new { success = false, message = "无效的IP地址格式" });

                var id = await _plcSignalService.AddPlcDeviceAsync(device);
                return Ok(new { success = true, data = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加设备失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("signal")]
        public async Task<IActionResult> AddSignal([FromBody] RCS_PlcSignal signal)
        {
            try
            {
                if (signal == null)
                    return BadRequest(new { success = false, message = "信号数据不能为空" });

                if (string.IsNullOrWhiteSpace(signal.Name))
                    return BadRequest(new { success = false, message = "信号名称不能为空" });

                var id = await _plcSignalService.AddPlcSignalAsync(signal);
                return Ok(new { success = true, data = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加信号失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDevice(int id, [FromBody] RCS_PlcDevice device)
        {
            try
            {
                if (device == null || device.Id != id)
                    return BadRequest(new { success = false, message = "设备数据无效" });

                await _plcSignalService.UpdatePlcDeviceAsync(device);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新设备失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("signal/{id}")]
        public async Task<IActionResult> UpdateSignal(int id, [FromBody] RCS_PlcSignal signal)
        {
            try
            {
                if (signal == null || signal.Id != id)
                    return BadRequest(new { success = false, message = "信号数据无效" });

                await _plcSignalService.UpdatePlcSignalAsync(signal);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新信号失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDevice(int id)
        {
            try
            {
                await _plcSignalService.DeletePlcDeviceAsync(id);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除设备失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("signal/{id}")]
        public async Task<IActionResult> DeleteSignal(int id)
        {
            try
            {
                await _plcSignalService.DeletePlcSignalAsync(id);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除信号失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}
