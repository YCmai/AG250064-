using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models;
using WarehouseManagementSystem.Models.Ndc;
using WarehouseManagementSystem.Models.Rcs;
using WarehouseManagementSystem.Services;
using WarehouseManagementSystem.Shared.Ndc;

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
        /// 获取工单分页列表
        /// </summary>
        [HttpGet("workorders")]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<RCS_WorkOrder>>>> GetWorkOrders(
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? orderNumber = null,
            [FromQuery] string? materialNumber = null)
        {
            try
            {
                if (pageIndex < 1) pageIndex = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;

                using var connection = _db.CreateConnection();
                var whereConditions = new List<string>();
                var parameters = new DynamicParameters();

                if (!string.IsNullOrWhiteSpace(orderNumber))
                {
                    whereConditions.Add("OrderNumber LIKE @OrderNumber");
                    parameters.Add("OrderNumber", $"%{orderNumber.Trim()}%");
                }

                if (!string.IsNullOrWhiteSpace(materialNumber))
                {
                    whereConditions.Add("MaterialNumber LIKE @MaterialNumber");
                    parameters.Add("MaterialNumber", $"%{materialNumber.Trim()}%");
                }

                string whereClause = whereConditions.Any()
                    ? "WHERE " + string.Join(" AND ", whereConditions)
                    : string.Empty;

                parameters.Add("Offset", (pageIndex - 1) * pageSize);
                parameters.Add("PageSize", pageSize);

                var countSql = $"SELECT COUNT(*) FROM RCS_WorkOrder {whereClause}";
                var dataSql = $@"
                    SELECT * FROM RCS_WorkOrder
                    {whereClause}
                    ORDER BY CreateTime DESC, ID DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
                var items = await connection.QueryAsync<RCS_WorkOrder>(dataSql, parameters);
                var paginatedData = PaginatedResponse<RCS_WorkOrder>.Create(items.ToList(), totalCount, pageIndex, pageSize);

                return Ok(ApiResponseHelper.Success(paginatedData, "获取工单列表成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取工单列表失败");
                return StatusCode(500, ApiResponseHelper.Failure<PaginatedResponse<RCS_WorkOrder>>("获取工单列表失败"));
            }
        }

        /// <summary>
        /// 获取AGV指令分页列表
        /// </summary>
        [HttpGet("agv-commands")]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<NdcUserTask>>>> GetAgvCommands(
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? taskCode = null,
            [FromQuery] string? taskGroupNo = null)
        {
            try
            {
                if (pageIndex < 1) pageIndex = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;

                using var connection = _db.CreateConnection();
                var whereConditions = new List<string> { "taskCode IS NOT NULL", "taskCode <> ''" };
                var parameters = new DynamicParameters();

                if (!string.IsNullOrWhiteSpace(taskCode))
                {
                    whereConditions.Add("taskCode LIKE @TaskCode");
                    parameters.Add("TaskCode", $"%{taskCode.Trim()}%");
                }

                if (!string.IsNullOrWhiteSpace(taskGroupNo))
                {
                    whereConditions.Add("taskGroupNo LIKE @TaskGroupNo");
                    parameters.Add("TaskGroupNo", $"%{taskGroupNo.Trim()}%");
                }

                string whereClause = "WHERE " + string.Join(" AND ", whereConditions);

                parameters.Add("Offset", (pageIndex - 1) * pageSize);
                parameters.Add("PageSize", pageSize);

                var countSql = $"SELECT COUNT(*) FROM RCS_UserTasks {whereClause}";
                var dataSql = $@"
                    SELECT * FROM RCS_UserTasks
                    {whereClause}
                    ORDER BY creatTime DESC, ID DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
                var items = await connection.QueryAsync<NdcUserTask>(dataSql, parameters);
                var paginatedData = PaginatedResponse<NdcUserTask>.Create(items.ToList(), totalCount, pageIndex, pageSize);

                return Ok(ApiResponseHelper.Success(paginatedData, "获取AGV指令列表成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取AGV指令列表失败");
                return StatusCode(500, ApiResponseHelper.Failure<PaginatedResponse<NdcUserTask>>("获取AGV指令列表失败"));
            }
        }

        /// <summary>
        /// 接收工单信息
        /// POST /api/ApiTask/workorder
        /// </summary>
        [HttpPost("/api/ApiTask/workorder")]
        [AllowAnonymous]
        public async Task<ActionResult<ExternalApiAckResponse>> ReceiveWorkOrder([FromBody] WorkOrderRequest? request)
        {
            if (request?.items == null || request.items.Count == 0)
            {
                return Ok(ExternalApiAckResponse.Fail("items不能为空"));
            }

            for (int index = 0; index < request.items.Count; index++)
            {
                var validationMessage = ValidateWorkOrderItem(request.items[index], index);
                if (!string.IsNullOrWhiteSpace(validationMessage))
                {
                    return Ok(ExternalApiAckResponse.Fail(validationMessage));
                }
            }

            try
            {
                using var connection = _db.CreateConnection();
                connection.Open();
                using var transaction = connection.BeginTransaction();

                const string insertSql = @"
                INSERT INTO RCS_WorkOrder
                (
                    OrderNumber,
                    MaterialNumber,
                    MaterialName,
                    MsgType,
                    CreateTime,
                    ProcessStatus,
                    Remarks
                )
                VALUES
                (
                    @OrderNumber,
                    @MaterialNumber,
                    @MaterialName,
                    @MsgType,
                    @CreateTime,
                    @ProcessStatus,
                    @Remarks
                )";

                var now = DateTime.Now;
                var entities = request.items.Select(item => new RCS_WorkOrder
                {
                    OrderNumber = item.orderNumber!.Trim(),
                    MaterialNumber = item.materialNumber!.Trim(),
                    MaterialName = item.materialName!.Trim(),
                    MsgType = item.msgType!.Trim(),
                    CreateTime = now,
                    ProcessStatus = 0,
                    Remarks = string.Empty
                }).ToList();

                await connection.ExecuteAsync(insertSql, entities, transaction);
                transaction.Commit();

                _logger.LogInformation("接收工单成功, Count={Count}", entities.Count);
                return Ok(ExternalApiAckResponse.Ok());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "接收工单失败");
                return Ok(ExternalApiAckResponse.Fail($"接收工单失败: {ex.Message}"));
            }
        }

        /// <summary>
        /// 接收AGV指令
        /// POST /api/ApiTask/agvcommand
        /// </summary>
        [HttpPost("/api/ApiTask/agvcommand")]
        [AllowAnonymous]
        public async Task<ActionResult<ExternalApiAckResponse>> ReceiveAgvCommand([FromBody] AgvCommandRequest? request)
        {
            if (request == null)
            {
                return Ok(ExternalApiAckResponse.Fail("请求体不能为空"));
            }

            if (string.IsNullOrWhiteSpace(request.taskNumber))
            {
                return Ok(ExternalApiAckResponse.Fail("taskNumber不能为空"));
            }

            if (request.priority is < 1 or > 3)
            {
                return Ok(ExternalApiAckResponse.Fail("priority仅支持1、2、3"));
            }

            if (request.items == null || request.items.Count == 0)
            {
                return Ok(ExternalApiAckResponse.Fail("items不能为空"));
            }

            if (request.items.GroupBy(x => x.seq).Any(g => g.Key.HasValue && g.Count() > 1))
            {
                return Ok(ExternalApiAckResponse.Fail("items中的seq不能重复"));
            }

            for (int index = 0; index < request.items.Count; index++)
            {
                var validationMessage = ValidateAgvCommandItem(request.items[index], index);
                if (!string.IsNullOrWhiteSpace(validationMessage))
                {
                    return Ok(ExternalApiAckResponse.Fail(validationMessage));
                }
            }

            try
            {
                using var connection = _db.CreateConnection();
                connection.Open();
                using var transaction = connection.BeginTransaction();

                string systemType = await connection.QueryFirstOrDefaultAsync<string>(
                    "SELECT Value FROM SystemSettings WHERE [Key] = 'SystemType'",
                    transaction: transaction) ?? "Heartbeat";

                int initialStatus = systemType.Trim().Equals("NDC", StringComparison.OrdinalIgnoreCase) ? -1 : 0;
                var now = DateTime.Now;
                var taskNumber = request.taskNumber.Trim();

                const string insertSql = @"
                INSERT INTO RCS_UserTasks
                (
                    taskStatus,
                    executed,
                    creatTime,
                    requestCode,
                    taskCode,
                    taskType,
                    priority,
                    userPriority,
                    robotCode,
                    sourcePosition,
                    targetPosition,
                    taskGroupNo,
                    palletNo,
                    binNumber,
                    remarks,
                    IsCancelled
                )
                VALUES
                (
                    @taskStatus,
                    @executed,
                    @creatTime,
                    @requestCode,
                    @taskCode,
                    @taskType,
                    @priority,
                    @userPriority,
                    @robotCode,
                    @sourcePosition,
                    @targetPosition,
                    @taskGroupNo,
                    @palletNo,
                    @binNumber,
                    @remarks,
                    @IsCancelled
                )";

                var tasks = request.items
                    .OrderBy(x => x.seq)
                    .Select(item => new NdcUserTask
                    {
                        taskStatus = (WarehouseManagementSystem.Shared.Ndc.TaskStatuEnum)initialStatus,
                        executed = false,
                        creatTime = now,
                        requestCode = BuildRequestCode(taskNumber, item.seq!.Value),
                        taskCode = taskNumber,
                        taskType = (TaskTypeEnum)item.taskType!.Value,
                        priority = BuildTaskPriority(request.priority!.Value, item.seq!.Value),
                        userPriority = request.priority!.Value,
                        robotCode = "0",
                        sourcePosition = NormalizeNullable(item.fromStation),
                        targetPosition = NormalizeNullable(item.toStation),
                        taskGroupNo = taskNumber,
                        palletNo = NormalizeNullable(item.palletNumber),
                        binNumber = NormalizeNullable(item.binNumber),
                        remarks = JsonSerializer.Serialize(new
                        {
                            taskNumber,
                            headerPriority = request.priority,
                            item = new
                            {
                                seq = item.seq,
                                palletNumber = item.palletNumber,
                                binNumber = item.binNumber,
                                fromStation = item.fromStation,
                                toStation = item.toStation,
                                taskType = item.taskType
                            }
                        }),
                        IsCancelled = false
                    })
                    .ToList();

                await connection.ExecuteAsync(insertSql, tasks, transaction);
                transaction.Commit();

                _logger.LogInformation("接收AGV指令成功, TaskNumber={TaskNumber}, Count={Count}", taskNumber, tasks.Count);
                return Ok(ExternalApiAckResponse.Ok());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "接收AGV指令失败, TaskNumber={TaskNumber}", request.taskNumber);
                return Ok(ExternalApiAckResponse.Fail($"接收AGV指令失败: {ex.Message}"));
            }
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
                    : string.Empty;

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

                return NotFound(ApiResponseHelper.Failure("任务不存在"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"删除API任务失败: ID={id}");
                return StatusCode(500, ApiResponseHelper.Failure("删除任务失败"));
            }
        }

        private static string? ValidateWorkOrderItem(WorkOrderItem? item, int index)
        {
            var rowNumber = index + 1;
            if (item == null)
            {
                return $"items[{rowNumber}]不能为空";
            }

            if (string.IsNullOrWhiteSpace(item.orderNumber))
            {
                return $"items[{rowNumber}].orderNumber不能为空";
            }

            if (item.orderNumber.Trim().Length > 14)
            {
                return $"items[{rowNumber}].orderNumber长度不能超过14";
            }

            if (string.IsNullOrWhiteSpace(item.materialNumber))
            {
                return $"items[{rowNumber}].materialNumber不能为空";
            }

            if (item.materialNumber.Trim().Length > 18)
            {
                return $"items[{rowNumber}].materialNumber长度不能超过18";
            }

            if (string.IsNullOrWhiteSpace(item.materialName))
            {
                return $"items[{rowNumber}].materialName不能为空";
            }

            if (item.materialName.Trim().Length > 40)
            {
                return $"items[{rowNumber}].materialName长度不能超过40";
            }

            if (string.IsNullOrWhiteSpace(item.msgType))
            {
                return $"items[{rowNumber}].msgType不能为空";
            }

            var msgType = item.msgType.Trim();
            if (msgType is not "1" and not "2")
            {
                return $"items[{rowNumber}].msgType仅支持1或2";
            }

            return null;
        }

        private static string? ValidateAgvCommandItem(AgvCommandItem? item, int index)
        {
            var rowNumber = index + 1;
            if (item == null)
            {
                return $"items[{rowNumber}]不能为空";
            }

            if (!item.seq.HasValue || item.seq <= 0)
            {
                return $"items[{rowNumber}].seq必须大于0";
            }

            if (!item.taskType.HasValue || item.taskType is < 1 or > 5)
            {
                return $"items[{rowNumber}].taskType仅支持1到5";
            }

            if (string.IsNullOrWhiteSpace(item.toStation))
            {
                return $"items[{rowNumber}].toStation不能为空";
            }

            if (item.toStation.Trim().Length > 20)
            {
                return $"items[{rowNumber}].toStation长度不能超过20";
            }

            if (!string.IsNullOrWhiteSpace(item.fromStation) && item.fromStation.Trim().Length > 20)
            {
                return $"items[{rowNumber}].fromStation长度不能超过20";
            }

            if (!string.IsNullOrWhiteSpace(item.palletNumber) && item.palletNumber.Trim().Length > 20)
            {
                return $"items[{rowNumber}].palletNumber长度不能超过20";
            }

            if (!string.IsNullOrWhiteSpace(item.binNumber) && item.binNumber.Trim().Length > 20)
            {
                return $"items[{rowNumber}].binNumber长度不能超过20";
            }

            if ((item.taskType == 1 || item.taskType == 5) && string.IsNullOrWhiteSpace(item.palletNumber))
            {
                return $"items[{rowNumber}].palletNumber不能为空";
            }

            if (item.taskType == 3 && string.IsNullOrWhiteSpace(item.binNumber))
            {
                return $"items[{rowNumber}].binNumber不能为空";
            }

            if ((item.taskType == 2 || item.taskType == 4 || item.taskType == 5) && string.IsNullOrWhiteSpace(item.fromStation))
            {
                return $"items[{rowNumber}].fromStation不能为空";
            }

            return null;
        }

        private static string BuildRequestCode(string taskNumber, int seq)
        {
            return $"{taskNumber}_{seq:D3}";
        }

        private static int BuildTaskPriority(int headerPriority, int seq)
        {
            return (headerPriority * 1000) + seq;
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    public class ExternalApiAckResponse
    {
        public string flag { get; set; } = "0";

        public string? errorMsg { get; set; }

        public static ExternalApiAckResponse Ok()
        {
            return new ExternalApiAckResponse
            {
                flag = "0",
                errorMsg = string.Empty
            };
        }

        public static ExternalApiAckResponse Fail(string message)
        {
            return new ExternalApiAckResponse
            {
                flag = "-1",
                errorMsg = message
            };
        }
    }

    public class WorkOrderRequest
    {
        [Required]
        public List<WorkOrderItem> items { get; set; } = new();
    }

    public class WorkOrderItem
    {
        public string? orderNumber { get; set; }

        public string? materialNumber { get; set; }

        public string? materialName { get; set; }

        public string? msgType { get; set; }
    }

    public class AgvCommandRequest
    {
        public string? taskNumber { get; set; }

        public int? priority { get; set; }

        [Required]
        public List<AgvCommandItem> items { get; set; } = new();
    }

    public class AgvCommandItem
    {
        public int? seq { get; set; }

        public string? palletNumber { get; set; }

        public string? binNumber { get; set; }

        public string? fromStation { get; set; }

        public string? toStation { get; set; }

        public int? taskType { get; set; }
    }
}
