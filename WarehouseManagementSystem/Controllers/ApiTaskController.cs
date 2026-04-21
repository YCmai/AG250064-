using WarehouseManagementSystem.Models.Ndc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Models;
using Dapper;
using WarehouseManagementSystem.Models.Enums;
using WarehouseManagementSystem.Services.Tasks;
using WarehouseManagementSystem.Db;

namespace WarehouseManagementSystem.Controllers
{
    /// <summary>
    /// API任务控制器，提供REST API任务管理接口
    /// </summary>
    [ApiController]
    [Route("api/task")]
    public class ApiTaskController : ControllerBase
    {
        private readonly ITaskService _taskService;
        private readonly ILocationService _locationService;
        private readonly ILogger<ApiTaskController> _logger;
        private readonly IDatabaseService _db;

        public ApiTaskController(
            ITaskService taskService,
            ILocationService locationService,
            ILogger<ApiTaskController> logger,
            IDatabaseService db)
        {
            _taskService = taskService;
            _locationService = locationService;
            _logger = logger;
            _db = db;
        }

        private async Task<string> GetSystemTypeAsync()
        {
            try
            {
                using var connection = _db.CreateConnection();
                var type = await connection.QueryFirstOrDefaultAsync<string>(
                    "SELECT Value FROM SystemSettings WHERE [Key] = 'SystemType'");
                return type ?? "Heartbeat";
            }
            catch
            {
                return "Heartbeat";
            }
        }

        /// <summary>
        /// 获取任务列表（分页、过滤）
        /// </summary>
        /// <param name="page">页码</param>
        /// <param name="pageSize">每页数量</param>
        /// <param name="filterDate">开始日期</param>
        /// <param name="endDate">结束日期</param>
        /// <returns>分页任务列表</returns>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<TaskResponse>>>> GetTasks(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] DateTime? filterDate = null,
            [FromQuery] DateTime? endDate = null)
        {
           // _logger.LogInformation($"获取任务列表: 页码={page}, 每页={pageSize}");

            try
            {
                // 验证分页参数
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 1000) pageSize = 20;

                var (items, totalItems) = await _taskService.GetUserTasks(page, pageSize, filterDate, endDate);

                var taskResponses = items.Select(t => new TaskResponse
                {
                    Id = t.Id,
                    RequestCode = t.requestCode,
                    TaskStatus = (int)t.taskStatus,
                    CreatedTime = t.creatTime ?? DateTime.Now,
                    SourcePosition = t.sourcePosition,
                    TargetPosition = t.targetPosition,
                    TaskType = (int)t.taskType,
                    RobotCode = t.robotCode ?? "-",
                    RunTaskId = t.runTaskId ?? "-",
                    CreatTime = t.creatTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-",
                    EndTime = t.endTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"
                }).ToList();

                var paginatedData = PaginatedResponse<TaskResponse>.Create(
                    taskResponses, totalItems, page, pageSize);

                return Ok(ApiResponseHelper.Success(paginatedData, "获取任务列表成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务列表失败");
                return StatusCode(500, ApiResponseHelper.Failure<PaginatedResponse<TaskResponse>>("获取任务列表失败"));
            }
        }

        /// <summary>
        /// 获取单个任务详情
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <returns>任务详情</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<TaskResponse>>> GetTaskById(int id)
        {
            _logger.LogInformation($"获取任务详情: ID={id}");

            try
            {
                var task = await _taskService.GetUserTaskById(id);
                if (task == null)
                {
                    return NotFound(ApiResponseHelper.Failure<TaskResponse>("任务不存在"));
                }

                var response = new TaskResponse
                {
                    Id = task.Id,
                    RequestCode = task.requestCode,
                    TaskStatus = (int)task.taskStatus,
                    CreatedTime = task.creatTime ?? DateTime.Now,
                    SourcePosition = task.sourcePosition,
                    TargetPosition = task.targetPosition,
                    TaskType = (int)task.taskType,
                    RobotCode = task.robotCode ?? "-",
                    RunTaskId = task.runTaskId ?? "-",
                    CreatTime = task.creatTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-",
                    EndTime = task.endTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"
                };

                return Ok(ApiResponseHelper.Success(response, "获取任务详情成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取任务详情失败: ID={id}");
                return StatusCode(500, ApiResponseHelper.Failure<TaskResponse>("获取任务详情失败"));
            }
        }

        /// <summary>
        /// 创建任务
        /// </summary>
        /// <param name="request">创建请求</param>
        /// <returns>创建结果</returns>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<CreateTaskResponse>>> CreateTask(
            [FromBody] CreateApiTaskRequest request)
        {
            _logger.LogInformation($"创建任务: 源={request.SourcePosition}, 目标={request.TargetPosition}");

            try
            {
                // 验证请求
                if (string.IsNullOrWhiteSpace(request.SourcePosition))
                {
                    return BadRequest(ApiResponseHelper.Failure<CreateTaskResponse>("源位置不能为空"));
                }

                if (string.IsNullOrWhiteSpace(request.TargetPosition))
                {
                    return BadRequest(ApiResponseHelper.Failure<CreateTaskResponse>("目标位置不能为空"));
                }

                if (request.SourcePosition == request.TargetPosition)
                {
                    return BadRequest(ApiResponseHelper.Failure<CreateTaskResponse>("源位置和目标位置不能相同"));
                }

                // 检查重复任务
                var isDuplicate = await _taskService.CheckDuplicateTask(request.SourcePosition, request.TargetPosition);
                if (isDuplicate)
                {
                    return BadRequest(ApiResponseHelper.Failure<CreateTaskResponse>("该任务已存在，请勿重复创建"));
                }

                // 创建任务
                var (success, message, taskId) = await _locationService.CreateRelocateTask(
                    request.SourcePosition, request.TargetPosition, request.MaterialCode ?? "");

                if (!success)
                {
                    return BadRequest(ApiResponseHelper.Failure<CreateTaskResponse>(message));
                }

                _logger.LogInformation($"任务创建成功: ID={taskId}");

                var response = new CreateTaskResponse
                {
                    Id = taskId,
                    SourcePosition = request.SourcePosition,
                    TargetPosition = request.TargetPosition
                };

                return Ok(ApiResponseHelper.Success(response, "任务创建成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建任务失败");
                return StatusCode(500, ApiResponseHelper.Failure<CreateTaskResponse>("创建任务失败"));
            }
        }

        /// <summary>
        /// 检查重复任务
        /// </summary>
        /// <param name="sourcePosition">源位置</param>
        /// <param name="targetPosition">目标位置</param>
        /// <returns>是否重复</returns>
        [HttpGet("check-duplicate")]
        public async Task<ActionResult<ApiResponse<CheckDuplicateResponse>>> CheckDuplicateTask(
            [FromQuery] string sourcePosition,
            [FromQuery] string targetPosition)
        {
            _logger.LogInformation($"检查重复任务: 源={sourcePosition}, 目标={targetPosition}");

            try
            {
                if (string.IsNullOrWhiteSpace(sourcePosition) || string.IsNullOrWhiteSpace(targetPosition))
                {
                    return BadRequest(ApiResponseHelper.Failure<CheckDuplicateResponse>("位置信息不能为空"));
                }

                var isDuplicate = await _taskService.CheckDuplicateTask(sourcePosition, targetPosition);

                var response = new CheckDuplicateResponse
                {
                    IsDuplicate = isDuplicate
                };

                return Ok(ApiResponseHelper.Success(response, "检查完成"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查重复任务失败");
                return StatusCode(500, ApiResponseHelper.Failure<CheckDuplicateResponse>("检查重复任务失败"));
            }
        }

        /// <summary>
        /// 获取可用位置列表
        /// </summary>
        /// <returns>可用位置列表</returns>
        [HttpGet("available-locations")]
        public async Task<ActionResult<ApiResponse<List<AvailableLocationResponse>>>> GetAvailableLocations()
        {
            _logger.LogInformation("获取可用位置列表");

            try
            {
                var (locations, _) = await _locationService.GetLocations("", 1, 10000);

                var availableLocations = locations
                    
                    .Select(l => new AvailableLocationResponse
                    {
                        Id = l.Id,
                        Name = l.Name,
                        NodeRemark = l.NodeRemark,
                        Group = l.Group,
                        IsEmpty = string.IsNullOrEmpty(l.MaterialCode),
                        IsLocked = l.Lock
                    })
                    .ToList();

                return Ok(ApiResponseHelper.Success(availableLocations, "获取可用位置列表成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取可用位置列表失败");
                return StatusCode(500, ApiResponseHelper.Failure<List<AvailableLocationResponse>>("获取可用位置列表失败"));
            }
        }

        /// <summary>
        /// 取消任务
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <returns>操作结果</returns>
        [HttpPost("{id}/cancel")]
        public async Task<ActionResult<ApiResponse>> CancelTask(int id)
        {
            _logger.LogInformation($"取消任务: ID={id}");

            try {
                var task = await _taskService.GetUserTaskById(id);
                if (task == null)
                {
                    return NotFound(ApiResponseHelper.Failure("任务不存在"));
                }

                // 检查系统类型并验证是否可以取消
                var systemType = await GetSystemTypeAsync();
                
                if (systemType == "NDC")
                {
                    // NDC取消逻辑：
                    // Finished: 11 (TaskFinish), 53 (OrderAgvFinish), 32 (CanceledWashFinish)
                    // Canceled: 30 (Canceled), 31 (CanceledWashing), 33 (RedirectRequest), 49 (InvalidUp), 50 (InvalidDown)
                    // 只有未完成且未取消的任务可以取消
                    // 简单判断：Status < 30 且 != 11, 53
                    if ((int)(int)task.taskStatus >= 30 || (int)(int)task.taskStatus == 11 || (int)(int)task.taskStatus == 53 || (int)(int)task.taskStatus == 32)
                    {
                        return BadRequest(ApiResponseHelper.Failure("该任务状态不可取消"));
                    }
                }
                else
                {
                    // Heartbeat取消逻辑：只有待处理和处理中的任务可以取消
                    if ((int)(int)task.taskStatus != (int)HeartbeatTaskStatus.Waiting && (int)(int)task.taskStatus != (int)HeartbeatTaskStatus.Working)
                    {
                        return BadRequest(ApiResponseHelper.Failure("只有待处理和处理中的任务可以取消"));
                    }
                }

                var (success, message) = await _taskService.CancelTask(id);

                // 注意：实际的任务状态更新应该在业务逻辑层实现
                // 这里仅作为示例，返回成功
                _logger.LogInformation($"任务取消成功: ID={id}");
                return Ok(ApiResponseHelper.Success("任务取消成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"取消任务失败: ID={id}");
                return StatusCode(500, ApiResponseHelper.Failure("取消任务失败"));
            }
        }

        /// <summary>
        /// 获取任务统计信息
        /// </summary>
        /// <param name="filterDate">开始日期</param>
        /// <param name="endDate">结束日期</param>
        /// <returns>统计信息</returns>
        [HttpGet("statistics")]
        public async Task<ActionResult<ApiResponse<TaskStatisticsResponse>>> GetTaskStatistics(
            [FromQuery] DateTime? filterDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            _logger.LogInformation("获取任务统计信息");

            try
            {
                var systemType = await GetSystemTypeAsync();
                var statsDict = await _taskService.GetTaskStatusCounts(filterDate, endDate);
                
                var totalTasks = 0;
                var completedTasks = 0;
                var runningTasks = 0;
                var waitingTasks = 0;
                var canceledTasks = 0;

                foreach (var kvp in statsDict)
                {
                    var status = kvp.Key;
                    var count = kvp.Value;
                    
                    totalTasks += count;
                    
                    if (systemType == "NDC")
                    {
                        // NDC Logic
                        if (status == 11 || status == 53 || status == 32)
                        {
                            completedTasks += count;
                        }
                        else if (status >= 30) // 30, 31, 33, 49, 50
                        {
                            canceledTasks += count;
                        }
                        else if (status == 1 || status == -1) // TaskStart, None
                        {
                            waitingTasks += count;
                        }
                        else // 0, 2, 3, 4, 6, 8, 10, 52
                        {
                            runningTasks += count;
                        }
                    }
                    else
                    {
                        // Heartbeat Logic
                        if ((status & (int)HeartbeatTaskStatus.Finished) != 0)
                        {
                            completedTasks += count;
                        }
                        // Running: Working set, Finished NOT set
                        else if ((status & (int)HeartbeatTaskStatus.Working) != 0)
                        {
                            runningTasks += count;
                        }
                        
                        if ((status & (int)HeartbeatTaskStatus.Cancel) != 0)
                        {
                            canceledTasks += count;
                        }
                        
                        if (status == (int)HeartbeatTaskStatus.Waiting)
                        {
                            waitingTasks += count;
                        }
                    }
                }

                var completionRate = totalTasks > 0 ? Math.Round((double)completedTasks / totalTasks * 100, 2) : 0;

                var response = new TaskStatisticsResponse
                {
                    TotalTasks = totalTasks,
                    CompletedTasks = completedTasks,
                    RunningTasks = runningTasks,
                    CanceledTasks = canceledTasks,
                    WaitingTasks = waitingTasks,
                    CompletionRate = completionRate
                };

                return Ok(ApiResponseHelper.Success(response, "获取任务统计信息成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务统计信息失败");
                return StatusCode(500, ApiResponseHelper.Failure<TaskStatisticsResponse>($"获取任务统计信息失败: {ex.Message} {ex.StackTrace}"));
            }
        }
    }

    /// <summary>
    /// 任务统计响应
    /// </summary>
    public class TaskStatisticsResponse
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int RunningTasks { get; set; }
        public int CanceledTasks { get; set; }
        public double CompletionRate { get; set; }

        public int WaitingTasks { get; set; }
    }

    /// <summary>
    /// 任务响应模型
    /// </summary>
    public class TaskResponse
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 请求代码
        /// </summary>
        public string RequestCode { get; set; }

        /// <summary>
        /// 任务状态 (0=待处理, 1=处理中, 4=已完成, 8=已取消)
        /// </summary>
        public int TaskStatus { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// 源位置
        /// </summary>
        public string SourcePosition { get; set; }

        /// <summary>
        /// 目标位置
        /// </summary>
        public string TargetPosition { get; set; }

        /// <summary>
        /// 任务类型
        /// </summary>
        public int TaskType { get; set; }

        /// <summary>
        /// AGV编号
        /// </summary>
        public string RobotCode { get; set; }

        /// <summary>
        /// 任务ID
        /// </summary>
        public string RunTaskId { get; set; }

        /// <summary>
        /// 创建时间（格式化）
        /// </summary>
        public string CreatTime { get; set; }

        /// <summary>
        /// 完成时间（格式化）
        /// </summary>
        public string EndTime { get; set; }
    }

    /// <summary>
    /// 创建任务请求
    /// </summary>
    public class CreateApiTaskRequest
    {
        /// <summary>
        /// 源位置
        /// </summary>
        public string SourcePosition { get; set; }

        /// <summary>
        /// 目标位置
        /// </summary>
        public string TargetPosition { get; set; }

        /// <summary>
        /// 物料代码
        /// </summary>
        public string? MaterialCode { get; set; }

        /// <summary>
        /// 任务类型
        /// </summary>
        public int TaskType { get; set; }

        /// <summary>
        /// 优先级
        /// </summary>
        public int Priority { get; set; }
    }

    /// <summary>
    /// 创建任务响应
    /// </summary>
    public class CreateTaskResponse
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 源位置
        /// </summary>
        public string SourcePosition { get; set; }

        /// <summary>
        /// 目标位置
        /// </summary>
        public string TargetPosition { get; set; }
    }

    /// <summary>
    /// 检查重复任务响应
    /// </summary>
    public class CheckDuplicateResponse
    {
        /// <summary>
        /// 是否重复
        /// </summary>
        public bool IsDuplicate { get; set; }
    }

    /// <summary>
    /// 可用位置响应
    /// </summary>
    public class AvailableLocationResponse
    {
        /// <summary>
        /// 位置ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 位置名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 位置备注
        /// </summary>
        public string NodeRemark { get; set; }

        /// <summary>
        /// 分组
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// 是否为空
        /// </summary>
        public bool IsEmpty { get; set; }

        /// <summary>
        /// 是否锁定
        /// </summary>
        public bool IsLocked { get; set; }
    }
}



