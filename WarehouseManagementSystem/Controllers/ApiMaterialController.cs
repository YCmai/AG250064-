using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Models;
using WarehouseManagementSystem.Models.DTOs;
using WarehouseManagementSystem.Models.Enums;
using WarehouseManagementSystem.Services;

namespace WarehouseManagementSystem.Controllers
{
    [ApiController]
    [Route("api/material")]
    public class ApiMaterialController : ControllerBase
    {
        private readonly IMaterialService _materialService;
        private readonly ILogger<ApiMaterialController> _logger;

        public ApiMaterialController(IMaterialService materialService, ILogger<ApiMaterialController> logger)
        {
            _materialService = materialService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllMaterials()
        {
            try
            {
                var materials = await _materialService.GetAllMaterialsAsync();
                return Ok(new { success = true, data = materials });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取物料列表失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{code}")]
        public async Task<IActionResult> GetMaterialByCode(string code)
        {
            try
            {
                var material = await _materialService.GetMaterialByCodeAsync(code);
                if (material == null)
                    return NotFound(new { success = false, message = "物料不存在" });
                return Ok(new { success = true, data = material });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取物料信息失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("instock")]
        public async Task<IActionResult> InStock([FromBody] MaterialTransactionDto dto)
        {
            try
            {
                if (dto == null)
                    return BadRequest(new { success = false, message = "数据不能为空" });

                dto.Type = TransactionType.InStock;
                var result = await _materialService.InStockAsync(dto);
                
                if (result.Succeeded)
                    return Ok(new { success = true, data = result.Data });
                return BadRequest(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "入库操作失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("outstock")]
        public async Task<IActionResult> OutStock([FromBody] dynamic request)
        {
            try
            {
                string materialCode = request.materialCode;
                decimal quantity = request.quantity;
                string locationCode = request.locationCode;
                string outReason = request.outReason;
                string operatorName = request.operatorName;
                string remark = request.remark;

                if (string.IsNullOrEmpty(materialCode) || quantity <= 0)
                    return BadRequest(new { success = false, message = "物料编码和出库数量是必须的" });

                var material = await _materialService.GetMaterialByCodeAsync(materialCode);
                if (material == null)
                    return NotFound(new { success = false, message = "物料不存在" });

                var dto = new MaterialTransactionDto
                {
                    MaterialCode = material.Code,
                    Quantity = quantity,
                    LocationCode = locationCode,
                    OperatorName = operatorName,
                    Remark = remark,
                    Type = TransactionType.OutStock,
                    OutReason = outReason
                };

                var result = await _materialService.OutStockAsync(dto);
                if (result.Succeeded)
                    return Ok(new { success = true, data = result.Data });
                return BadRequest(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "出库操作失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("history/{materialCode}")]
        public async Task<IActionResult> GetTransactionHistory(string materialCode)
        {
            try
            {
                var transactions = await _materialService.GetTransactionHistoryAsync(materialCode);
                return Ok(new { success = true, data = transactions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取交易历史失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("transactions/instock")]
        public async Task<IActionResult> GetInStockTransactions()
        {
            try
            {
                var materials = await _materialService.GetAllMaterialsAsync();
                var transactions = new List<RCS_MaterialTransactions>();

                foreach (var material in materials)
                {
                    var materialTransactions = await _materialService.GetTransactionHistoryAsync(material.Code);
                    if (materialTransactions != null && materialTransactions.Any())
                    {
                        transactions.AddRange(materialTransactions.Where(t => t.Type == TransactionType.InStock));
                    }
                }

                return Ok(new { success = true, data = transactions.OrderByDescending(t => t.CreateTime).Take(100).ToList() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取入库交易记录失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("transactions/outstock")]
        public async Task<IActionResult> GetOutStockTransactions()
        {
            try
            {
                var materials = await _materialService.GetAllMaterialsAsync();
                var transactions = new List<RCS_MaterialTransactions>();

                foreach (var material in materials)
                {
                    var materialTransactions = await _materialService.GetTransactionHistoryAsync(material.Code);
                    if (materialTransactions != null && materialTransactions.Any())
                    {
                        transactions.AddRange(materialTransactions.Where(t => t.Type == TransactionType.OutStock));
                    }
                }

                return Ok(new { success = true, data = transactions.OrderByDescending(t => t.CreateTime).Take(100).ToList() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取出库交易记录失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("low-stock")]
        public async Task<IActionResult> GetLowStockMaterials()
        {
            try
            {
                var materials = await _materialService.GetLowStockMaterialsAsync();
                return Ok(new { success = true, data = materials });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取低库存物料失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}
