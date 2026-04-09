using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Db;
using Dapper;
using WarehouseManagementSystem.Models;
using WarehouseManagementSystem.Services;

namespace WarehouseManagementSystem.Controllers
{
    /// <summary>
    /// API自动PLC任务控制器，提供REST API自动PLC任务管理接口
    /// </summary>
    [ApiController]
    [Route("api/auto-plc-task")]
    public class ApiAutoPlcTaskController : ControllerBase
    {
        private readonly IDatabaseService _db;
        private readonly ILogger<ApiAutoPlcTaskController> _logger;

        public ApiAutoPlcTaskController(IDatabaseService db, ILogger<ApiAutoPlcTaskController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// 获取分页的PLC任务列表
        /// </summary>
        /// <param name="pageIndex">页码，从1开始</param>
        /// <param name="pageSize">每页记录数</param>
        /// <param name="plcRemark">PLC备注</param>
        /// <param name="plcTypeDb">PLC DB块类型</param>
        /// <returns>分页结果</returns>
        [HttpGet("paged-tasks")]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<AutoPlcTaskResponse>>>> GetPagedTasks(
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? plcRemark = null,
            [FromQuery] string? plcTypeDb = null)
        {
            _logger.LogInformation($"获取PLC任务列表: 页码={pageIndex}, 每页={pageSize}");

            try
            {
                if (pageIndex < 1) pageIndex = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;

                using var connection = _db.CreateConnection();
                
                // 构建WHERE条件
                var whereConditions = new List<string>();
                var parameters = new DynamicParameters();
                
                // 过滤掉心跳信号任务
                whereConditions.Add("t.Remark <> '心跳信号'");
                
                if (!string.IsNullOrEmpty(plcRemark))
                {
                    whereConditions.Add("d.Remark = @PlcRemark");
                    parameters.Add("PlcRemark", plcRemark);
                }
                
                if (!string.IsNullOrEmpty(plcTypeDb))
                {
                    whereConditions.Add("t.PLCTypeDb = @PLCTypeDb");
                    parameters.Add("PLCTypeDb", plcTypeDb);
                }
                
                string whereClause = whereConditions.Any() 
                    ? "WHERE " + string.Join(" AND ", whereConditions) 
                    : "";
                
                // 处理分页参数
                parameters.Add("Offset", (pageIndex - 1) * pageSize);
                parameters.Add("PageSize", pageSize);
                
                // 修正 JOIN 逻辑以匹配数据查询
                var countSql = $@"
                    SELECT COUNT(DISTINCT t.OrderCode) 
                    FROM RCS_AutoPlcTasks t 
                    LEFT JOIN RCS_PlcDevice d ON t.PlcType = d.IpAddress AND 
                        (d.ModuleAddress = t.PLCTypeDb OR (d.ModuleAddress IS NULL AND t.PLCTypeDb IS NULL))
                    {whereClause}";
                
                // 使用 ROW_NUMBER() 分组并取最新一条，实现合并重复项
                var dataSql = $@"
                    WITH RankedTasks AS (
                        SELECT t.*, d.Remark AS PlcRemark, d.IsEnabled AS DeviceEnabled,
                               ROW_NUMBER() OVER (PARTITION BY t.OrderCode ORDER BY t.CreatingTime DESC, t.Id DESC) as rn
                        FROM RCS_AutoPlcTasks t
                        LEFT JOIN RCS_PlcDevice d ON t.PlcType = d.IpAddress AND 
                            (d.ModuleAddress = t.PLCTypeDb OR (d.ModuleAddress IS NULL AND t.PLCTypeDb IS NULL))
                        {whereClause}
                    )
                    SELECT * FROM RankedTasks
                    WHERE rn = 1
                    ORDER BY CreatingTime DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
                var tasks = await connection.QueryAsync(dataSql, parameters);

                var taskResponses = tasks.Select(t => new AutoPlcTaskResponse
                {
                    Id = t.Id != null ? (int)t.Id : 0,
                    OrderCode = t.OrderCode,
                    Status = t.Status != null ? (int)t.Status : 0,
                    IsSend = t.IsSend != null && Convert.ToInt32(t.IsSend) == 1,
                    Signal = t.Signal,
                    CreateTime = t.CreatingTime,
                    UpdateTime = t.UpdateTime,
                    Remark = t.Remark,
                    PlcType = t.PlcType,
                    PlcTypeDb = t.PLCTypeDb,
                    PlcRemark = t.PlcRemark,
                    AgvNo = t.AgvNo,
                    DeviceEnabled = t.DeviceEnabled != null && Convert.ToBoolean(t.DeviceEnabled)
                }).ToList();

                var paginatedData = PaginatedResponse<AutoPlcTaskResponse>.Create(
                    taskResponses, totalCount, pageIndex, pageSize);

                return Ok(ApiResponseHelper.Success(paginatedData, "获取PLC任务列表成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取PLC任务列表失败");
                return StatusCode(500, ApiResponseHelper.Failure<PaginatedResponse<AutoPlcTaskResponse>>("获取PLC任务列表失败"));
            }
        }

        /// <summary>
        /// 获取所有PLC设备remark（去重）
        /// </summary>
        /// <returns>PLC设备备注列表</returns>
        [HttpGet("plc-types")]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetPlcTypes()
        {
            _logger.LogInformation("获取PLC设备类型列表");

            try
            {
                using var connection = _db.CreateConnection();
                var types = await connection.QueryAsync<string>("SELECT DISTINCT Remark FROM RCS_PlcDevice WHERE Remark IS NOT NULL AND Remark <> ''");
                
                return Ok(ApiResponseHelper.Success(types.ToList(), "获取PLC设备类型列表成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取PLC设备类型列表失败");
                return StatusCode(500, ApiResponseHelper.Failure<List<string>>("获取PLC设备类型列表失败"));
            }
        }

        /// <summary>
        /// 获取所有PLC DB块类型
        /// </summary>
        /// <returns>PLC DB块类型列表</returns>
        [HttpGet("plc-type-db")]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetPlcTypeDb()
        {
            _logger.LogInformation("获取PLC DB块类型列表");

            try
            {
                using var connection = _db.CreateConnection();
                var dbTypes = await connection.QueryAsync<string>(@"
                    SELECT DISTINCT PLCTypeDb 
                    FROM RCS_AutoPlcTasks 
                    WHERE PLCTypeDb IS NOT NULL AND PLCTypeDb <> ''");
                
                return Ok(ApiResponseHelper.Success(dbTypes.ToList(), "获取PLC DB块类型列表成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取PLC DB块类型列表失败");
                return StatusCode(500, ApiResponseHelper.Failure<List<string>>("获取PLC DB块类型列表失败"));
            }
        }

        /// <summary>
        /// 删除PLC任务
        /// </summary>
        /// <param name="orderCode">订单编号</param>
        /// <returns>操作结果</returns>
        [HttpDelete("{orderCode}")]
        public async Task<ActionResult<ApiResponse>> DeleteTask(string orderCode)
        {
            _logger.LogInformation($"删除PLC任务: OrderCode={orderCode}");

            try
            {
                using var connection = _db.CreateConnection();
                
                // 检查任务是否存在且未发送
                var task = await connection.QueryFirstOrDefaultAsync(
                    "SELECT * FROM RCS_AutoPlcTasks WHERE OrderCode = @OrderCode",
                    new { OrderCode = orderCode });

                if (task == null)
                {
                    return NotFound(ApiResponseHelper.Failure("任务不存在"));
                }

                if (task.IsSend)
                {
                    return BadRequest(ApiResponseHelper.Failure("已发送的任务不能删除"));
                }

                // 删除任务
                var result = await connection.ExecuteAsync(
                    "DELETE FROM RCS_AutoPlcTasks WHERE OrderCode = @OrderCode",
                    new { OrderCode = orderCode });

                if (result > 0)
                {
                    _logger.LogInformation($"PLC任务删除成功: OrderCode={orderCode}");
                    return Ok(ApiResponseHelper.Success("任务删除成功"));
                }
                else
                {
                    return BadRequest(ApiResponseHelper.Failure("任务删除失败"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"删除PLC任务失败: OrderCode={orderCode}");
                return StatusCode(500, ApiResponseHelper.Failure("删除任务失败"));
            }
        }

        /// <summary>
        /// 获取任务状态描述
        /// </summary>
        /// <param name="status">状态码</param>
        /// <returns>状态描述</returns>
        [HttpGet("status-description/{status}")]
        public ActionResult<ApiResponse<StatusDescriptionResponse>> GetStatusDescription(int status)
        {
            string description = status switch
            {
                1 => "写入bool值",
                2 => "重置bool值",
                3 => "写入INT",
                4 => "重置INT",
                5 => "写入String值",
                6 => "重置String值",
                _ => "未知状态"
            };

            var response = new StatusDescriptionResponse { Description = description };
            return Ok(ApiResponseHelper.Success(response, "获取状态描述成功"));
        }
    }

    /// <summary>
    /// 自动PLC任务响应模型
    /// </summary>
    public class AutoPlcTaskResponse
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 订单编号
        /// </summary>
        public string OrderCode { get; set; }

        /// <summary>
        /// 状态
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// 是否已发送
        /// </summary>
        public bool IsSend { get; set; }

        /// <summary>
        /// 信号名称
        /// </summary>
        public string Signal { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime? CreateTime { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime? UpdateTime { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }

        /// <summary>
        /// PLC类型
        /// </summary>
        public string PlcType { get; set; }

        /// <summary>
        /// PLC DB块类型
        /// </summary>
        public string PlcTypeDb { get; set; }

        /// <summary>
        /// PLC备注
        /// </summary>
        public string PlcRemark { get; set; }

        /// <summary>
        /// 执行的AGV
        /// </summary>
        public string AgvNo { get; set; }

        /// <summary>
        /// 对应PLC设备是否启用
        /// </summary>
        public bool DeviceEnabled { get; set; }
    }

    /// <summary>
    /// 状态描述响应模型
    /// </summary>
    public class StatusDescriptionResponse
    {
        /// <summary>
        /// 状态描述
        /// </summary>
        public string Description { get; set; }
    }
}
