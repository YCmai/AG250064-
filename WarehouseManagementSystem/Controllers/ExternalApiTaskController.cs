using WarehouseManagementSystem.Models.Rcs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Db;
using Dapper;
using WarehouseManagementSystem.Models;
using WarehouseManagementSystem.Services.Tasks;

namespace WarehouseManagementSystem.Controllers
{
    /// <summary>
    /// 外部API任务控制器，提供REST API任务日志管理接口
    /// </summary>
    [ApiController]
    [Route("api/external-api-task")]
    public class ExternalApiTaskController : ControllerBase
    {
        private readonly IDatabaseService _db;
        private readonly ILogger<ExternalApiTaskController> _logger;

        public ExternalApiTaskController(IDatabaseService db, ILogger<ExternalApiTaskController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// 获取分页的任务列表
        /// </summary>
        /// <param name="pageIndex">页码，从1开始</param>
        /// <param name="pageSize">每页记录数</param>
        /// <param name="taskCode">任务编号</param>
        /// <param name="taskType">任务类型</param>
        /// <returns>分页结果</returns>
        [HttpGet("paged-tasks")]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<NdcApiTask>>>> GetPagedTasks(
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? taskCode = null,
            [FromQuery] int? taskType = null)
        {
            _logger.LogInformation($"获取外部API任务列表: 页码={pageIndex}, 每页={pageSize}");

            try
            {
                if (pageIndex < 1) pageIndex = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;

                using var connection = _db.CreateConnection();
                
                // 构建WHERE条件
                var whereConditions = new List<string>();
                var parameters = new DynamicParameters();
                
                if (!string.IsNullOrEmpty(taskCode))
                {
                    whereConditions.Add("TaskCode LIKE @TaskCode");
                    parameters.Add("TaskCode", $"%{taskCode}%");
                }
                
                if (taskType.HasValue)
                {
                    whereConditions.Add("TaskType = @TaskType");
                    parameters.Add("TaskType", taskType.Value);
                }
                
                string whereClause = whereConditions.Any() 
                    ? "WHERE " + string.Join(" AND ", whereConditions) 
                    : "";
                
                // 处理分页参数
                parameters.Add("Offset", (pageIndex - 1) * pageSize);
                parameters.Add("PageSize", pageSize);
                
                var countSql = $"SELECT COUNT(*) FROM NdcApiTask {whereClause}";
                
                var dataSql = $@"
                    SELECT * FROM NdcApiTask 
                    {whereClause}
                    ORDER BY CreateTime DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
                var tasks = await connection.QueryAsync<NdcApiTask>(dataSql, parameters);

                var paginatedData = PaginatedResponse<NdcApiTask>.Create(
                    tasks.ToList(), totalCount, pageIndex, pageSize);

                return Ok(ApiResponseHelper.Success(paginatedData, "获取API任务列表成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取API任务列表失败");
                return StatusCode(500, ApiResponseHelper.Failure<PaginatedResponse<NdcApiTask>>("获取API任务列表失败"));
            }
        }

        /// <summary>
        /// 删除任务
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <returns>操作结果</returns>
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse>> DeleteTask(int id)
        {
            _logger.LogInformation($"删除API任务: ID={id}");

            try
            {
                using var connection = _db.CreateConnection();
                
                var result = await connection.ExecuteAsync(
                    "DELETE FROM NdcApiTask WHERE ID = @ID",
                    new { ID = id });

                if (result > 0)
                {
                    return Ok(ApiResponseHelper.Success("任务删除成功"));
                }
                else
                {
                    return NotFound(ApiResponseHelper.Failure("任务不存在"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"删除API任务失败: ID={id}");
                return StatusCode(500, ApiResponseHelper.Failure("删除任务失败"));
            }
        }
    }
}

