using WarehouseManagementSystem.Models.Ndc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Models;
using WarehouseManagementSystem.Services.Tasks;

namespace WarehouseManagementSystem.Controllers
{
    /// <summary>
    /// API储位控制器，提供REST API储位管理接口
    /// </summary>
    [ApiController]
    [Route("api/location")]
    public class ApiLocationController : ControllerBase
    {
        private readonly ILocationService _locationService;
        private readonly ILogger<ApiLocationController> _logger;

        public ApiLocationController(
            ILocationService locationService,
            ILogger<ApiLocationController> logger)
        {
            _locationService = locationService;
            _logger = logger;
        }

        /// <summary>
        /// 获取储位列表（分页、搜索）
        /// </summary>
        /// <param name="searchString">搜索字符串</param>
        /// <param name="page">页码</param>
        /// <param name="pageSize">每页数量</param>
        /// <returns>分页储位列表</returns>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<LocationResponse>>>> GetLocations(
            [FromQuery] string? searchString = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
           // _logger.LogInformation($"获取储位列表: 搜索={searchString}, 页码={page}, 每页={pageSize}");

            try
            {
                // 验证分页参数
                if (page < 1) page = 1;
                // 允许更大的页面大小用于仪表盘显示
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 10000) pageSize = 10000;

                var (items, totalItems) = await _locationService.GetLocations(searchString, page, pageSize);

                var locationResponses = items.Select(l => new LocationResponse
                {
                    Id = l.Id,
                    Name = l.Name,
                    NodeRemark = l.NodeRemark,
                    Group = l.Group,
                    MaterialCode = l.MaterialCode,
                    PalletID = l.PalletID,
                    IsEmpty = string.IsNullOrEmpty(l.MaterialCode) || l.MaterialCode == "0",
                    Lock = l.Lock,
                    Enabled = l.Enabled,
                    
                    Weight = l.Weight,
                    Quanitity = l.Quanitity,
                    EntryDate = l.EntryDate,
                    LiftingHeight = l.LiftingHeight,
                    UnloadHeight = l.UnloadHeight,
                    
                    WattingNode = l.WattingNode,
                    
                }).ToList();

                var paginatedData = PaginatedResponse<LocationResponse>.Create(
                    locationResponses, totalItems, page, pageSize);

              //  _logger.LogInformation($"获取储位列表成功: 返回{locationResponses.Count}条记录，总数{totalItems}");
                return Ok(ApiResponseHelper.Success(paginatedData, "获取储位列表成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取储位列表失败");
                return StatusCode(500, ApiResponseHelper.Failure<PaginatedResponse<LocationResponse>>($"获取储位列表失败: {ex.Message}"));
            }
        }

        /// <summary>
        /// 获取单个储位详情
        /// </summary>
        /// <param name="id">储位ID</param>
        /// <returns>储位详情</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<LocationResponse>>> GetLocationById(int id)
        {
           // _logger.LogInformation($"获取储位详情: ID={id}");

            try
            {
                var location = await _locationService.GetLocationById(id);
                if (location == null)
                {
                    return NotFound(ApiResponseHelper.Failure<LocationResponse>("储位不存在"));
                }

                var response = new LocationResponse
                {
                    Id = location.Id,
                    Name = location.Name,
                    NodeRemark = location.NodeRemark,
                    Group = location.Group,
                    MaterialCode = location.MaterialCode,
                    PalletID = location.PalletID,
                    IsEmpty = string.IsNullOrEmpty(location.MaterialCode) || location.MaterialCode == "0",
                    Lock = location.Lock,
                    Enabled = location.Enabled,
                    
                    Weight = location.Weight,
                    Quanitity = location.Quanitity,
                    EntryDate = location.EntryDate,
                    LiftingHeight = location.LiftingHeight,
                    UnloadHeight = location.UnloadHeight,
                    
                    WattingNode = location.WattingNode,
                    
                };

                return Ok(ApiResponseHelper.Success(response, "获取储位详情成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取储位详情失败: ID={id}");
                return StatusCode(500, ApiResponseHelper.Failure<LocationResponse>("获取储位详情失败"));
            }
        }

        /// <summary>
        /// 创建储位
        /// </summary>
        /// <param name="request">创建请求</param>
        /// <returns>创建结果</returns>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<CreateLocationResponse>>> CreateLocation(
            [FromBody] CreateLocationRequest request)
        {
            _logger.LogInformation($"创建储位: {request.Name}");

            try
            {
                // 验证请求
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(ApiResponseHelper.Failure<CreateLocationResponse>("储位名称不能为空"));
                }

                var location = new NdcLocation
                {
                    Name = request.Name,
                    NodeRemark = request.NodeRemark ?? "",
                    Group = request.Group ?? "",
                    WattingNode = request.WattingNode ?? "",
                    LiftingHeight = request.LiftingHeight ?? 0,
                    UnloadHeight = request.UnloadHeight ?? 0,
                    // 
                    Lock = request.Lock ?? false,
                    Enabled = request.Enabled ?? true,
                    // 
                    MaterialCode = request.MaterialCode,
                    PalletID = request.PalletID ?? "0",
                    Weight = request.Weight ?? "0",
                    Quanitity = request.Quanitity ?? "0",
                    EntryDate = request.EntryDate
                };

                var (success, message) = await _locationService.CreateOrUpdateLocation(location);
                if (!success)
                {
                    return BadRequest(ApiResponseHelper.Failure<CreateLocationResponse>(message));
                }

                _logger.LogInformation($"储位创建成功: {request.Name}");

                var response = new CreateLocationResponse
                {
                    Id = location.Id,
                    Name = location.Name
                };

                return Ok(ApiResponseHelper.Success(response, "储位创建成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建储位失败");
                return StatusCode(500, ApiResponseHelper.Failure<CreateLocationResponse>("创建储位失败"));
            }
        }

        /// <summary>
        /// 更新储位
        /// </summary>
        /// <param name="id">储位ID</param>
        /// <param name="request">更新请求</param>
        /// <returns>更新结果</returns>
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse>> UpdateLocation(int id, [FromBody] UpdateLocationRequest request)
        {
            _logger.LogInformation($"更新储位: ID={id}");

            try
            {
                var location = await _locationService.GetLocationById(id);
                if (location == null)
                {
                    return NotFound(ApiResponseHelper.Failure("储位不存在"));
                }

                // 更新所有字段
                if (!string.IsNullOrWhiteSpace(request.Name))
                    location.Name = request.Name;
                if (!string.IsNullOrWhiteSpace(request.NodeRemark))
                    location.NodeRemark = request.NodeRemark;
                if (!string.IsNullOrWhiteSpace(request.Group))
                    location.Group = request.Group;
                if (request.WattingNode != null)
                    location.WattingNode = request.WattingNode;
                if (request.LiftingHeight.HasValue)
                    location.LiftingHeight = request.LiftingHeight.Value;
                if (request.UnloadHeight.HasValue)
                    location.UnloadHeight = request.UnloadHeight.Value;
                
                if (request.Lock.HasValue)
                    location.Lock = request.Lock.Value;
                if (request.Enabled.HasValue)
                    location.Enabled = request.Enabled.Value;
                
                if (request.MaterialCode != null)
                    location.MaterialCode = request.MaterialCode;
                if (request.PalletID != null)
                    location.PalletID = request.PalletID;
                if (request.Weight != null)
                    location.Weight = request.Weight;
                if (request.Quanitity != null)
                    location.Quanitity = request.Quanitity;
                if (request.EntryDate != null)
                    location.EntryDate = request.EntryDate;

                var (success, message) = await _locationService.CreateOrUpdateLocation(location);
                if (!success)
                {
                    return BadRequest(ApiResponseHelper.Failure(message));
                }

                _logger.LogInformation($"储位更新成功: ID={id}");
                return Ok(ApiResponseHelper.Success("储位更新成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新储位失败: ID={id}");
                return StatusCode(500, ApiResponseHelper.Failure("更新储位失败"));
            }
        }

        /// <summary>
        /// 删除储位（硬删除）
        /// </summary>
        /// <param name="id">储位ID</param>
        /// <returns>删除结果</returns>
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse>> DeleteLocation(int id)
        {
            _logger.LogInformation($"删除储位: ID={id}");

            try
            {
                var location = await _locationService.GetLocationById(id);
                if (location == null)
                {
                    return NotFound(ApiResponseHelper.Failure("储位不存在"));
                }

                var (success, message) = await _locationService.HandleLocationOperation(id, 3); // 3 = 删除（硬删除）
                if (!success)
                {
                    return BadRequest(ApiResponseHelper.Failure(message));
                }

                _logger.LogInformation($"储位删除成功: ID={id}");
                return Ok(ApiResponseHelper.Success(message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"删除储位失败: ID={id}");
                return StatusCode(500, ApiResponseHelper.Failure("删除储位失败"));
            }
        }

        /// <summary>
        /// 清空储位物料
        /// </summary>
        /// <param name="id">储位ID</param>
        /// <returns>操作结果</returns>
        [HttpPost("{id}/clear-material")]
        public async Task<ActionResult<ApiResponse>> ClearMaterial(int id)
        {
            _logger.LogInformation($"清空储位物料: ID={id}");

            try
            {
                var location = await _locationService.GetLocationById(id);
                if (location == null)
                {
                    return NotFound(ApiResponseHelper.Failure("储位不存在"));
                }

                var (success, message) = await _locationService.HandleLocationOperation(id, 1); // 1 = 清空物料
                if (!success)
                {
                    return BadRequest(ApiResponseHelper.Failure(message));
                }

                _logger.LogInformation($"储位物料清空成功: ID={id}");
                return Ok(ApiResponseHelper.Success("储位物料清空成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"清空储位物料失败: ID={id}");
                return StatusCode(500, ApiResponseHelper.Failure("清空储位物料失败"));
            }
        }

        /// <summary>
        /// 锁定/解锁储位
        /// </summary>
        /// <param name="id">储位ID</param>
        /// <param name="request">锁定请求</param>
        /// <returns>操作结果</returns>
        [HttpPost("{id}/toggle-lock")]
        public async Task<ActionResult<ApiResponse>> ToggleLock(int id, [FromBody] ToggleLockRequest request)
        {
            _logger.LogInformation($"切换储位锁定状态: ID={id}, 锁定={request.LockState}");

            try
            {
                var location = await _locationService.GetLocationById(id);
                if (location == null)
                {
                    return NotFound(ApiResponseHelper.Failure("储位不存在"));
                }

                // 检查当前状态是否已经是目标状态
                if (location.Lock == request.LockState)
                {
                    return Ok(ApiResponseHelper.Success($"储位已经是{(request.LockState ? "锁定" : "解锁")}状态"));
                }

                var (success, message) = await _locationService.HandleLocationOperation(id, 2); // 2 = 切换锁定状态
                if (!success)
                {
                    return BadRequest(ApiResponseHelper.Failure(message));
                }

                _logger.LogInformation($"储位锁定状态切换成功: ID={id}");
                return Ok(ApiResponseHelper.Success(message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"切换储位锁定状态失败: ID={id}");
                return StatusCode(500, ApiResponseHelper.Failure("切换储位锁定状态失败"));
            }
        }

        /// <summary>
        /// 启用/禁用储位
        /// </summary>
        /// <param name="id">储位ID</param>
        /// <param name="request">启用状态请求</param>
        /// <returns>操作结果</returns>
        [HttpPost("{id}/toggle-enabled")]
        public async Task<ActionResult<ApiResponse>> ToggleEnabled(int id, [FromBody] ToggleEnabledRequest request)
        {
            _logger.LogInformation($"切换储位启用状态: ID={id}, 启用={request.EnabledState}");

            try
            {
                var location = await _locationService.GetLocationById(id);
                if (location == null)
                {
                    return NotFound(ApiResponseHelper.Failure("储位不存在"));
                }

                if (location.Enabled == request.EnabledState)
                {
                    return Ok(ApiResponseHelper.Success($"储位已经是{(request.EnabledState ? "启用" : "禁用")}状态"));
                }

                var (success, message) = await _locationService.HandleLocationOperation(id, 5, request.EnabledState);
                if (!success)
                {
                    return BadRequest(ApiResponseHelper.Failure(message));
                }

                _logger.LogInformation($"储位启用状态切换成功: ID={id}");
                return Ok(ApiResponseHelper.Success(message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"切换储位启用状态失败: ID={id}");
                return StatusCode(500, ApiResponseHelper.Failure("切换储位启用状态失败"));
            }
        }

        /// <summary>
        /// 批量清空物料
        /// </summary>
        /// <param name="request">批量请求</param>
        /// <returns>操作结果</returns>
        [HttpPost("batch/clear-material")]
        public async Task<ActionResult<ApiResponse<BatchOperationResponse>>> BatchClearMaterial(
            [FromBody] BatchOperationRequest request)
        {
            _logger.LogInformation($"批量清空物料: 数量={request.Ids?.Count ?? 0}");

            try
            {
                if (request.Ids == null || request.Ids.Count == 0)
                {
                    return BadRequest(ApiResponseHelper.Failure<BatchOperationResponse>("请选择至少一个储位"));
                }

                var (success, message, affectedCount) = await _locationService.BatchClearMaterialsByIds(request.Ids);
                if (!success)
                {
                    return BadRequest(ApiResponseHelper.Failure<BatchOperationResponse>(message));
                }

                _logger.LogInformation($"批量清空物料成功: 数量={affectedCount}");

                var response = new BatchOperationResponse
                {
                    SuccessCount = affectedCount,
                    FailCount = 0
                };

                return Ok(ApiResponseHelper.Success(response, message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量清空物料失败");
                return StatusCode(500, ApiResponseHelper.Failure<BatchOperationResponse>("批量清空物料失败"));
            }
        }

        /// <summary>
        /// 批量锁定/解锁
        /// </summary>
        /// <param name="request">批量请求</param>
        /// <returns>操作结果</returns>
        [HttpPost("batch/toggle-lock")]
        public async Task<ActionResult<ApiResponse<BatchOperationResponse>>> BatchToggleLock(
            [FromBody] BatchToggleLockRequest request)
        {
            _logger.LogInformation($"批量切换锁定状态: 数量={request.Ids?.Count ?? 0}, 锁定={request.LockState}");

            try
            {
                if (request.Ids == null || request.Ids.Count == 0)
                {
                    return BadRequest(ApiResponseHelper.Failure<BatchOperationResponse>("请选择至少一个储位"));
                }

                var (success, message, affectedCount) = await _locationService.BatchToggleLockByIds(request.Ids, request.LockState);
                if (!success)
                {
                    return BadRequest(ApiResponseHelper.Failure<BatchOperationResponse>(message));
                }

                _logger.LogInformation($"批量切换锁定状态成功: 数量={affectedCount}");

                var response = new BatchOperationResponse
                {
                    SuccessCount = affectedCount,
                    FailCount = 0
                };

                return Ok(ApiResponseHelper.Success(response, message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量切换锁定状态失败");
                return StatusCode(500, ApiResponseHelper.Failure<BatchOperationResponse>("批量切换锁定状态失败"));
            }
        }

        /// <summary>
        /// 批量导入储位
        /// </summary>
        /// <param name="request">批量导入请求</param>
        /// <returns>导入结果</returns>
        [HttpPost("batch/import")]
        public async Task<ActionResult<ApiResponse<BatchImportResponse>>> BatchImportLocations(
            [FromBody] BatchImportRequest request)
        {
            _logger.LogInformation($"批量导入储位: 数量={request.Locations?.Count ?? 0}");

            try
            {
                if (request.Locations == null || request.Locations.Count == 0)
                {
                    return BadRequest(ApiResponseHelper.Failure<BatchImportResponse>("导入数据为空"));
                }

                int successCount = 0;
                int failCount = 0;
                var errors = new List<string>();

                foreach (var locationData in request.Locations)
                {
                    try
                    {
                        // 验证必填字段
                        if (string.IsNullOrWhiteSpace(locationData.Name))
                        {
                            errors.Add($"行 {successCount + failCount + 1}: 储位名称不能为空");
                            failCount++;
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(locationData.NodeRemark))
                        {
                            errors.Add($"行 {successCount + failCount + 1}: 节点备注不能为空");
                            failCount++;
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(locationData.Group))
                        {
                            errors.Add($"行 {successCount + failCount + 1}: 分组不能为空");
                            failCount++;
                            continue;
                        }

                        var location = new NdcLocation
                        {
                            Name = locationData.Name,
                            NodeRemark = locationData.NodeRemark,
                            Group = locationData.Group,
                            WattingNode = locationData.WattingNode ?? "",
                            LiftingHeight = locationData.LiftingHeight ?? 0,
                            UnloadHeight = locationData.UnloadHeight ?? 0,
                            Lock = locationData.Lock ?? false,
                            Enabled = locationData.Enabled ?? true,
                            MaterialCode = locationData.MaterialCode,
                            PalletID = locationData.PalletID ?? "0",
                            Weight = locationData.Weight ?? "0",
                            Quanitity = locationData.Quanitity ?? "0",
                            EntryDate = locationData.EntryDate
                        };

                        var (success, message) = await _locationService.CreateOrUpdateLocation(location);
                        if (success)
                        {
                            successCount++;
                        }
                        else
                        {
                            errors.Add($"行 {successCount + failCount + 1}: {message}");
                            failCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"行 {successCount + failCount + 1}: {ex.Message}");
                        failCount++;
                    }
                }

                _logger.LogInformation($"批量导入完成: 成功={successCount}, 失败={failCount}");

                var response = new BatchImportResponse
                {
                    SuccessCount = successCount,
                    FailCount = failCount,
                    Errors = errors
                };

                return Ok(ApiResponseHelper.Success(response, $"导入完成：成功 {successCount} 条，失败 {failCount} 条"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量导入储位失败");
                return StatusCode(500, ApiResponseHelper.Failure<BatchImportResponse>("批量导入失败"));
            }
        }

        /// <summary>
        /// 物料转移 - 直接将物料信息从起点转移到终点
        /// </summary>
        /// <param name="request">转移请求</param>
        /// <returns>操作结果</returns>
        [HttpPost("transfer-material")]
        public async Task<ActionResult<ApiResponse>> TransferMaterial([FromBody] TransferMaterialRequest request)
        {
            _logger.LogInformation($"物料转移: 源储位ID={request.SourceLocationId}, 目标储位ID={request.TargetLocationId}");

            try
            {
                if (request.SourceLocationId <= 0 || request.TargetLocationId <= 0)
                {
                    return BadRequest(ApiResponseHelper.Failure("请求参数无效"));
                }

                // 获取源储位和目标储位
                var sourceLocation = await _locationService.GetLocationById(request.SourceLocationId);
                var targetLocation = await _locationService.GetLocationById(request.TargetLocationId);

                if (sourceLocation == null)
                {
                    return NotFound(ApiResponseHelper.Failure("源储位不存在"));
                }

                if (targetLocation == null)
                {
                    return NotFound(ApiResponseHelper.Failure("目标储位不存在"));
                }

                // 验证源储位有物料
                if (string.IsNullOrEmpty(sourceLocation.MaterialCode))
                {
                    return BadRequest(ApiResponseHelper.Failure("源储位没有物料，无法转移"));
                }

                // 验证目标储位状态
                

                if (targetLocation.Lock)
                {
                    return BadRequest(ApiResponseHelper.Failure("目标储位已锁定，无法接收物料"));
                }

                if (!string.IsNullOrEmpty(targetLocation.MaterialCode))
                {
                    return BadRequest(ApiResponseHelper.Failure("目标储位已有物料，无法转移"));
                }

                // 执行转移：将源储位的物料信息复制到目标储位，然后清空源储位
                targetLocation.MaterialCode = sourceLocation.MaterialCode;
                targetLocation.PalletID = sourceLocation.PalletID;
                targetLocation.Weight = sourceLocation.Weight;
                targetLocation.Quanitity = sourceLocation.Quanitity;
                targetLocation.EntryDate = DateTime.Now.ToString(); // 更新入库时间

                // 清空源储位
                sourceLocation.MaterialCode = null;
                sourceLocation.PalletID = "0";
                sourceLocation.Weight = "0";
                sourceLocation.Quanitity = "0";
                sourceLocation.EntryDate = null;

                // 保存更改
                var (targetSuccess, targetMessage) = await _locationService.CreateOrUpdateLocation(targetLocation);
                if (!targetSuccess)
                {
                    return BadRequest(ApiResponseHelper.Failure($"更新目标储位失败: {targetMessage}"));
                }

                var (sourceSuccess, sourceMessage) = await _locationService.CreateOrUpdateLocation(sourceLocation);
                if (!sourceSuccess)
                {
                    return BadRequest(ApiResponseHelper.Failure($"清空源储位失败: {sourceMessage}"));
                }

                _logger.LogInformation($"物料转移成功: {sourceLocation.NodeRemark} -> {targetLocation.NodeRemark}, 物料编码: {targetLocation.MaterialCode}");
                
                return Ok(ApiResponseHelper.Success($"物料已从 {sourceLocation.NodeRemark} 转移到 {targetLocation.NodeRemark}"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "物料转移失败");
                return StatusCode(500, ApiResponseHelper.Failure("物料转移失败"));
            }
        }

        /// <summary>
        /// 物料移库 - 生成AGV任务进行物理移库
        /// </summary>
        /// <param name="request">移库请求</param>
        /// <returns>操作结果</returns>
        [HttpPost("relocate-material")]
        public async Task<ActionResult<ApiResponse<RelocateMaterialResponse>>> RelocateMaterial([FromBody] TransferMaterialRequest request)
        {
            _logger.LogInformation($"物料移库: 源储位ID={request.SourceLocationId}, 目标储位ID={request.TargetLocationId}");

            try
            {
                if (request.SourceLocationId <= 0 || request.TargetLocationId <= 0)
                {
                    return BadRequest(ApiResponseHelper.Failure<RelocateMaterialResponse>("请求参数无效"));
                }

                // 获取源储位和目标储位
                var sourceLocation = await _locationService.GetLocationById(request.SourceLocationId);
                var targetLocation = await _locationService.GetLocationById(request.TargetLocationId);

                if (sourceLocation == null)
                {
                    return NotFound(ApiResponseHelper.Failure<RelocateMaterialResponse>("源储位不存在"));
                }

                if (targetLocation == null)
                {
                    return NotFound(ApiResponseHelper.Failure<RelocateMaterialResponse>("目标储位不存在"));
                }

                // 验证源储位有物料
                if (string.IsNullOrEmpty(sourceLocation.MaterialCode))
                {
                    return BadRequest(ApiResponseHelper.Failure<RelocateMaterialResponse>("源储位没有物料，无法移库"));
                }

                // 验证目标储位状态
                

                if (targetLocation.Lock)
                {
                    return BadRequest(ApiResponseHelper.Failure<RelocateMaterialResponse>("目标储位已锁定，无法接收物料"));
                }

                if (!string.IsNullOrEmpty(targetLocation.MaterialCode))
                {
                    return BadRequest(ApiResponseHelper.Failure<RelocateMaterialResponse>("目标储位已有物料，无法移库"));
                }

                // 调用LocationService创建AGV任务
                var (success, message, taskId) = await _locationService.CreateRelocateTask(
                    sourceLocation.NodeRemark,
                    targetLocation.NodeRemark,
                    sourceLocation.MaterialCode
                );

                if (!success)
                {
                    return BadRequest(ApiResponseHelper.Failure<RelocateMaterialResponse>(message));
                }

                _logger.LogInformation($"移库任务创建成功: {sourceLocation.NodeRemark} -> {targetLocation.NodeRemark}, 任务ID: {taskId}, 物料编码: {sourceLocation.MaterialCode}");
                
                var response = new RelocateMaterialResponse
                {
                    TaskId = taskId,
                    SourceLocation = sourceLocation.NodeRemark,
                    TargetLocation = targetLocation.NodeRemark,
                    MaterialCode = sourceLocation.MaterialCode
                };

                return Ok(ApiResponseHelper.Success(response, $"移库任务已创建，任务ID: {taskId}"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建移库任务失败");
                return StatusCode(500, ApiResponseHelper.Failure<RelocateMaterialResponse>("创建移库任务失败"));
            }
        }
    }

    /// <summary>
    /// 储位响应模型
    /// </summary>
    public class LocationResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string NodeRemark { get; set; }
        public string Group { get; set; }
        public string MaterialCode { get; set; }
        public string PalletID { get; set; }
        public string Weight { get; set; }
        public string Quanitity { get; set; }
        public string EntryDate { get; set; }
        public int LiftingHeight { get; set; }
        public int UnloadHeight { get; set; }
        
        public string WattingNode { get; set; }
        public bool IsEmpty { get; set; }
        public bool Lock { get; set; }
        public bool Enabled { get; set; }
        
        
    }

    /// <summary>
    /// 创建储位请求
    /// </summary>
    public class CreateLocationRequest
    {
        public string Name { get; set; }
        public string NodeRemark { get; set; }
        public string Group { get; set; }
        public string? WattingNode { get; set; }
        public int? LiftingHeight { get; set; }
        public int? UnloadHeight { get; set; }
        
        public bool? Lock { get; set; }
        public bool? Enabled { get; set; }
        
        public string? MaterialCode { get; set; }
        public string? PalletID { get; set; }
        public string? Weight { get; set; }
        public string? Quanitity { get; set; }
        public string? EntryDate { get; set; }
    }

    /// <summary>
    /// 创建储位响应
    /// </summary>
    public class CreateLocationResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// 更新储位请求
    /// </summary>
    public class UpdateLocationRequest
    {
        public string? Name { get; set; }
        public string? NodeRemark { get; set; }
        public string? Group { get; set; }
        public string? WattingNode { get; set; }
        public int? LiftingHeight { get; set; }
        public int? UnloadHeight { get; set; }
        
        public bool? Lock { get; set; }
        public bool? Enabled { get; set; }
        
        public string? MaterialCode { get; set; }
        public string? PalletID { get; set; }
        public string? Weight { get; set; }
        public string? Quanitity { get; set; }
        public string? EntryDate { get; set; }
    }

    /// <summary>
    /// 锁定请求
    /// </summary>
    public class ToggleLockRequest
    {
        /// <summary>
        /// 是否锁定
        /// </summary>
        public bool LockState { get; set; }
    }

    /// <summary>
    /// 启用状态请求
    /// </summary>
    public class ToggleEnabledRequest
    {
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool EnabledState { get; set; }
    }

    /// <summary>
    /// 批量操作请求
    /// </summary>
    public class BatchOperationRequest
    {
        /// <summary>
        /// 储位ID列表
        /// </summary>
        public List<int> Ids { get; set; }
    }

    /// <summary>
    /// 批量切换锁定请求
    /// </summary>
    public class BatchToggleLockRequest
    {
        /// <summary>
        /// 储位ID列表
        /// </summary>
        public List<int> Ids { get; set; }

        /// <summary>
        /// 是否锁定
        /// </summary>
        public bool LockState { get; set; }
    }

    /// <summary>
    /// 批量操作响应
    /// </summary>
    public class BatchOperationResponse
    {
        /// <summary>
        /// 成功数量
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// 失败数量
        /// </summary>
        public int FailCount { get; set; }
    }

    /// <summary>
    /// 批量导入请求
    /// </summary>
    public class BatchImportRequest
    {
        public List<ImportLocationData> Locations { get; set; }
    }

    /// <summary>
    /// 导入储位数据
    /// </summary>
    public class ImportLocationData
    {
        public string Name { get; set; }
        public string NodeRemark { get; set; }
        public string Group { get; set; }
        public string? WattingNode { get; set; }
        public int? LiftingHeight { get; set; }
        public int? UnloadHeight { get; set; }
        
        public bool? Lock { get; set; }
        public bool? Enabled { get; set; }
        
        public string? MaterialCode { get; set; }
        public string? PalletID { get; set; }
        public string? Weight { get; set; }
        public string? Quanitity { get; set; }
        public string? EntryDate { get; set; }
    }

    /// <summary>
    /// 批量导入响应
    /// </summary>
    public class BatchImportResponse
    {
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public List<string> Errors { get; set; }
    }

    /// <summary>
    /// 物料转移/移库请求
    /// </summary>
    public class TransferMaterialRequest
    {
        /// <summary>
        /// 源储位ID
        /// </summary>
        public int SourceLocationId { get; set; }

        /// <summary>
        /// 目标储位ID
        /// </summary>
        public int TargetLocationId { get; set; }
    }

    /// <summary>
    /// 移库响应
    /// </summary>
    public class RelocateMaterialResponse
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public int TaskId { get; set; }

        /// <summary>
        /// 源储位
        /// </summary>
        public string SourceLocation { get; set; }

        /// <summary>
        /// 目标储位
        /// </summary>
        public string TargetLocation { get; set; }

        /// <summary>
        /// 物料代码
        /// </summary>
        public string MaterialCode { get; set; }
    }
}





