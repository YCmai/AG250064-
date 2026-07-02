using Microsoft.AspNetCore.Mvc;
using WarehouseManagementSystem.Models;
using WarehouseManagementSystem.Models.Ndc;
using WarehouseManagementSystem.Services.Tasks;

namespace WarehouseManagementSystem.Controllers
{
    /// <summary>
    /// API 储位控制器，提供标准化储位管理接口。
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
        /// 获取储位列表（分页、搜索）。
        /// </summary>
        /// <param name="searchString">搜索字符串。</param>
        /// <param name="page">页码。</param>
        /// <param name="pageSize">每页数量。</param>
        /// <returns>分页储位列表。</returns>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<LocationResponse>>>> GetLocations(
            [FromQuery] string? searchString = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (page < 1)
                {
                    page = 1;
                }

                if (pageSize < 1)
                {
                    pageSize = 20;
                }

                if (pageSize > 10000)
                {
                    pageSize = 10000;
                }

                var (items, totalItems) = await _locationService.GetLocations(searchString, page, pageSize);
                var locationResponses = items.Select(MapLocationResponse).ToList();
                var paginatedData = PaginatedResponse<LocationResponse>.Create(locationResponses, totalItems, page, pageSize);
                return Ok(ApiResponseHelper.Success(paginatedData, "获取储位列表成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取储位列表失败");
                return StatusCode(500, ApiResponseHelper.Failure<PaginatedResponse<LocationResponse>>($"获取储位列表失败: {ex.Message}"));
            }
        }

        /// <summary>
        /// 获取单个储位详情。
        /// </summary>
        /// <param name="id">储位 ID。</param>
        /// <returns>储位详情。</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<LocationResponse>>> GetLocationById(int id)
        {
            try
            {
                var location = await _locationService.GetLocationById(id);
                if (location == null)
                {
                    return NotFound(ApiResponseHelper.Failure<LocationResponse>("储位不存在"));
                }

                return Ok(ApiResponseHelper.Success(MapLocationResponse(location), "获取储位详情成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取储位详情失败: ID={Id}", id);
                return StatusCode(500, ApiResponseHelper.Failure<LocationResponse>("获取储位详情失败"));
            }
        }

        /// <summary>
        /// 获取标准化推荐储位列表。
        /// </summary>
        /// <param name="excludeLocationId">需要排除的储位 ID，例如当前源储位。</param>
        /// <returns>推荐储位列表。</returns>
        [HttpGet("recommended-targets")]
        public async Task<ActionResult<ApiResponse<List<RecommendedLocationResponse>>>> GetRecommendedTargetLocations([FromQuery] int? excludeLocationId = null)
        {
            try
            {
                var locations = await _locationService.GetRecommendedLocations(excludeLocationId);
                var responses = locations.Select(MapRecommendedLocationResponse).ToList();
                return Ok(ApiResponseHelper.Success(responses, "获取推荐储位成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取推荐储位失败");
                return StatusCode(500, ApiResponseHelper.Failure<List<RecommendedLocationResponse>>("获取推荐储位失败"));
            }
        }

        /// <summary>
        /// 获取储位分组列表。
        /// </summary>
        /// <returns>分组名称集合。</returns>
        [HttpGet("groups")]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetLocationGroups()
        {
            try
            {
                var groups = await _locationService.GetLocationGroups();
                return Ok(ApiResponseHelper.Success(groups, "获取储位分组成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取储位分组失败");
                return StatusCode(500, ApiResponseHelper.Failure<List<string>>("获取储位分组失败"));
            }
        }

        /// <summary>
        /// 创建储位。
        /// </summary>
        /// <param name="request">创建请求。</param>
        /// <returns>创建结果。</returns>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<CreateLocationResponse>>> CreateLocation([FromBody] CreateLocationRequest request)
        {
            _logger.LogInformation("创建储位: {Name}", request.Name);

            try
            {
                var validationError = ValidateLocationRequest(request, requireName: true);
                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    return BadRequest(ApiResponseHelper.Failure<CreateLocationResponse>(validationError));
                }

                var location = MapRequestToLocation(request);
                var (success, message) = await _locationService.CreateOrUpdateLocation(location);
                if (!success)
                {
                    return BadRequest(ApiResponseHelper.Failure<CreateLocationResponse>(message));
                }

                var response = new CreateLocationResponse
                {
                    Id = location.Id,
                    Name = location.Name ?? string.Empty
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
        /// 更新储位。
        /// </summary>
        /// <param name="id">储位 ID。</param>
        /// <param name="request">更新请求。</param>
        /// <returns>更新结果。</returns>
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse>> UpdateLocation(int id, [FromBody] UpdateLocationRequest request)
        {
            _logger.LogInformation("更新储位: ID={Id}", id);

            try
            {
                var location = await _locationService.GetLocationById(id);
                if (location == null)
                {
                    return NotFound(ApiResponseHelper.Failure("储位不存在"));
                }

                ApplyUpdateRequest(location, request);
                var validationError = ValidateLocationEntity(location);
                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    return BadRequest(ApiResponseHelper.Failure(validationError));
                }

                var (success, message) = await _locationService.CreateOrUpdateLocation(location);
                if (!success)
                {
                    return BadRequest(ApiResponseHelper.Failure(message));
                }

                return Ok(ApiResponseHelper.Success("储位更新成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新储位失败: ID={Id}", id);
                return StatusCode(500, ApiResponseHelper.Failure("更新储位失败"));
            }
        }

        /// <summary>
        /// 删除储位（硬删除）。
        /// </summary>
        /// <param name="id">储位 ID。</param>
        /// <returns>删除结果。</returns>
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse>> DeleteLocation(int id)
        {
            _logger.LogInformation("删除储位: ID={Id}", id);

            try
            {
                var location = await _locationService.GetLocationById(id);
                if (location == null)
                {
                    return NotFound(ApiResponseHelper.Failure("储位不存在"));
                }

                var (success, message) = await _locationService.HandleLocationOperation(id, 3);
                if (!success)
                {
                    return BadRequest(ApiResponseHelper.Failure(message));
                }

                return Ok(ApiResponseHelper.Success(message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除储位失败: ID={Id}", id);
                return StatusCode(500, ApiResponseHelper.Failure("删除储位失败"));
            }
        }

        /// <summary>
        /// 清空储位物料。
        /// </summary>
        /// <param name="id">储位 ID。</param>
        /// <returns>操作结果。</returns>
        [HttpPost("{id}/clear-material")]
        public async Task<ActionResult<ApiResponse>> ClearMaterial(int id)
        {
            _logger.LogInformation("清空储位物料: ID={Id}", id);

            try
            {
                var location = await _locationService.GetLocationById(id);
                if (location == null)
                {
                    return NotFound(ApiResponseHelper.Failure("储位不存在"));
                }

                var (success, message) = await _locationService.HandleLocationOperation(id, 1);
                if (!success)
                {
                    return BadRequest(ApiResponseHelper.Failure(message));
                }

                return Ok(ApiResponseHelper.Success("储位物料清空成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清空储位物料失败: ID={Id}", id);
                return StatusCode(500, ApiResponseHelper.Failure("清空储位物料失败"));
            }
        }

        /// <summary>
        /// 锁定或解锁储位。
        /// </summary>
        /// <param name="id">储位 ID。</param>
        /// <param name="request">锁定请求。</param>
        /// <returns>操作结果。</returns>
        [HttpPost("{id}/toggle-lock")]
        public async Task<ActionResult<ApiResponse>> ToggleLock(int id, [FromBody] ToggleLockRequest request)
        {
            _logger.LogInformation("切换储位锁定状态: ID={Id}, 锁定={LockState}", id, request.LockState);

            try
            {
                var location = await _locationService.GetLocationById(id);
                if (location == null)
                {
                    return NotFound(ApiResponseHelper.Failure("储位不存在"));
                }

                if (location.Lock == request.LockState)
                {
                    return Ok(ApiResponseHelper.Success($"储位已经是{(request.LockState ? "锁定" : "解锁")}状态"));
                }

                var (success, message) = await _locationService.HandleLocationOperation(id, 2);
                if (!success)
                {
                    return BadRequest(ApiResponseHelper.Failure(message));
                }

                return Ok(ApiResponseHelper.Success(message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "切换储位锁定状态失败: ID={Id}", id);
                return StatusCode(500, ApiResponseHelper.Failure("切换储位锁定状态失败"));
            }
        }

        /// <summary>
        /// 启用或禁用储位。
        /// </summary>
        /// <param name="id">储位 ID。</param>
        /// <param name="request">启用状态请求。</param>
        /// <returns>操作结果。</returns>
        [HttpPost("{id}/toggle-enabled")]
        public async Task<ActionResult<ApiResponse>> ToggleEnabled(int id, [FromBody] ToggleEnabledRequest request)
        {
            _logger.LogInformation("切换储位启用状态: ID={Id}, 启用={EnabledState}", id, request.EnabledState);

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

                return Ok(ApiResponseHelper.Success(message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "切换储位启用状态失败: ID={Id}", id);
                return StatusCode(500, ApiResponseHelper.Failure("切换储位启用状态失败"));
            }
        }

        /// <summary>
        /// 批量清空物料。
        /// </summary>
        /// <param name="request">批量请求。</param>
        /// <returns>操作结果。</returns>
        [HttpPost("batch/clear-material")]
        public async Task<ActionResult<ApiResponse<BatchOperationResponse>>> BatchClearMaterial([FromBody] BatchOperationRequest request)
        {
            _logger.LogInformation("批量清空物料: 数量={Count}", request.Ids?.Count ?? 0);

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
        /// 批量锁定或解锁。
        /// </summary>
        /// <param name="request">批量请求。</param>
        /// <returns>操作结果。</returns>
        [HttpPost("batch/toggle-lock")]
        public async Task<ActionResult<ApiResponse<BatchOperationResponse>>> BatchToggleLock([FromBody] BatchToggleLockRequest request)
        {
            _logger.LogInformation("批量切换锁定状态: 数量={Count}, 锁定={LockState}", request.Ids?.Count ?? 0, request.LockState);

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
        /// 批量导入储位。
        /// </summary>
        /// <param name="request">批量导入请求。</param>
        /// <returns>导入结果。</returns>
        [HttpPost("batch/import")]
        public async Task<ActionResult<ApiResponse<BatchImportResponse>>> BatchImportLocations([FromBody] BatchImportRequest request)
        {
            _logger.LogInformation("批量导入储位: 数量={Count}", request.Locations?.Count ?? 0);

            try
            {
                if (request.Locations == null || request.Locations.Count == 0)
                {
                    return BadRequest(ApiResponseHelper.Failure<BatchImportResponse>("导入数据为空"));
                }

                var successCount = 0;
                var failCount = 0;
                var errors = new List<string>();

                foreach (var locationData in request.Locations)
                {
                    var rowNumber = successCount + failCount + 1;

                    try
                    {
                        var validationError = ValidateImportLocation(locationData);
                        if (!string.IsNullOrWhiteSpace(validationError))
                        {
                            errors.Add($"行 {rowNumber}: {validationError}");
                            failCount++;
                            continue;
                        }

                        var location = MapImportLocation(locationData);
                        var (success, message) = await _locationService.CreateOrUpdateLocation(location);
                        if (success)
                        {
                            successCount++;
                        }
                        else
                        {
                            errors.Add($"行 {rowNumber}: {message}");
                            failCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"行 {rowNumber}: {ex.Message}");
                        failCount++;
                    }
                }

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
        /// 物料转移，直接将物料信息从起点转移到终点。
        /// </summary>
        /// <param name="request">转移请求。</param>
        /// <returns>操作结果。</returns>
        [HttpPost("transfer-material")]
        public async Task<ActionResult<ApiResponse>> TransferMaterial([FromBody] TransferMaterialRequest request)
        {
            _logger.LogInformation("物料转移: 源储位ID={SourceLocationId}, 目标储位ID={TargetLocationId}", request.SourceLocationId, request.TargetLocationId);

            try
            {
                if (request.SourceLocationId <= 0 || request.TargetLocationId <= 0)
                {
                    return BadRequest(ApiResponseHelper.Failure("请求参数无效"));
                }

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

                if (string.IsNullOrEmpty(sourceLocation.MaterialCode))
                {
                    return BadRequest(ApiResponseHelper.Failure("源储位没有物料，无法转移"));
                }

                if (targetLocation.Lock)
                {
                    return BadRequest(ApiResponseHelper.Failure("目标储位已锁定，无法接收物料"));
                }

                if (!string.IsNullOrEmpty(targetLocation.MaterialCode))
                {
                    return BadRequest(ApiResponseHelper.Failure("目标储位已有物料，无法转移"));
                }

                var recommendationItems = await _locationService.GetRecommendedLocations(request.SourceLocationId);
                var targetRecommendation = recommendationItems.FirstOrDefault(item => item.Id == request.TargetLocationId);
                if (targetRecommendation != null && !targetRecommendation.IsReachableTarget)
                {
                    return BadRequest(ApiResponseHelper.Failure("目标储位在当前库道结构下不可达，请优先处理外侧储位。"));
                }

                targetLocation.MaterialCode = sourceLocation.MaterialCode;
                targetLocation.PalletID = sourceLocation.PalletID;
                targetLocation.Weight = sourceLocation.Weight;
                targetLocation.Quanitity = sourceLocation.Quanitity;
                targetLocation.EntryDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                sourceLocation.MaterialCode = null;
                sourceLocation.PalletID = "0";
                sourceLocation.Weight = "0";
                sourceLocation.Quanitity = "0";
                sourceLocation.EntryDate = null;

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

                return Ok(ApiResponseHelper.Success($"物料已从 {sourceLocation.NodeRemark} 转移到 {targetLocation.NodeRemark}"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "物料转移失败");
                return StatusCode(500, ApiResponseHelper.Failure("物料转移失败"));
            }
        }

        /// <summary>
        /// 物料移库，生成 AGV 任务进行物理移库。
        /// </summary>
        /// <param name="request">移库请求。</param>
        /// <returns>操作结果。</returns>
        [HttpPost("relocate-material")]
        public async Task<ActionResult<ApiResponse<RelocateMaterialResponse>>> RelocateMaterial([FromBody] TransferMaterialRequest request)
        {
            _logger.LogInformation("物料移库: 源储位ID={SourceLocationId}, 目标储位ID={TargetLocationId}", request.SourceLocationId, request.TargetLocationId);

            try
            {
                if (request.SourceLocationId <= 0 || request.TargetLocationId <= 0)
                {
                    return BadRequest(ApiResponseHelper.Failure<RelocateMaterialResponse>("请求参数无效"));
                }

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

                if (string.IsNullOrEmpty(sourceLocation.MaterialCode))
                {
                    return BadRequest(ApiResponseHelper.Failure<RelocateMaterialResponse>("源储位没有物料，无法移库"));
                }

                if (targetLocation.Lock)
                {
                    return BadRequest(ApiResponseHelper.Failure<RelocateMaterialResponse>("目标储位已锁定，无法接收物料"));
                }

                if (!string.IsNullOrEmpty(targetLocation.MaterialCode))
                {
                    return BadRequest(ApiResponseHelper.Failure<RelocateMaterialResponse>("目标储位已有物料，无法移库"));
                }

                var (success, message, taskId) = await _locationService.CreateRelocateTask(
                    sourceLocation.NodeRemark ?? string.Empty,
                    targetLocation.NodeRemark ?? string.Empty,
                    sourceLocation.MaterialCode);

                if (!success)
                {
                    return BadRequest(ApiResponseHelper.Failure<RelocateMaterialResponse>(message));
                }

                var response = new RelocateMaterialResponse
                {
                    TaskId = taskId,
                    SourceLocation = sourceLocation.NodeRemark ?? string.Empty,
                    TargetLocation = targetLocation.NodeRemark ?? string.Empty,
                    MaterialCode = sourceLocation.MaterialCode ?? string.Empty
                };

                return Ok(ApiResponseHelper.Success(response, $"移库任务已创建，任务ID: {taskId}"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建移库任务失败");
                return StatusCode(500, ApiResponseHelper.Failure<RelocateMaterialResponse>("创建移库任务失败"));
            }
        }

        private static LocationResponse MapLocationResponse(NdcLocation location)
        {
            return new LocationResponse
            {
                Id = location.Id,
                Name = location.Name ?? string.Empty,
                NodeRemark = location.NodeRemark ?? string.Empty,
                Group = location.Group ?? string.Empty,
                MaterialCode = location.MaterialCode,
                PalletID = location.PalletID,
                IsEmpty = string.IsNullOrWhiteSpace(location.MaterialCode) || location.MaterialCode == "0",
                Lock = location.Lock,
                Enabled = location.Enabled,
                Weight = location.Weight,
                Quanitity = location.Quanitity,
                EntryDate = location.EntryDate,
                LiftingHeight = location.LiftingHeight,
                UnloadHeight = location.UnloadHeight,
                WattingNode = location.WattingNode,
                LaneCode = location.LaneCode,
                DepthIndex = location.DepthIndex
            };
        }

        private static RecommendedLocationResponse MapRecommendedLocationResponse(RecommendedLocationResult location)
        {
            return new RecommendedLocationResponse
            {
                Id = location.Id,
                Name = location.Name,
                NodeRemark = location.NodeRemark,
                Group = location.Group,
                LaneCode = location.LaneCode,
                DepthIndex = location.DepthIndex,
                WattingNode = location.WattingNode,
                IsEmpty = location.IsEmpty,
                IsLocked = location.IsLocked,
                Enabled = location.Enabled,
                MaterialCode = location.MaterialCode,
                PalletID = location.PalletID,
                IsReachableTarget = location.IsReachableTarget,
                IsRecommendedTarget = location.IsRecommendedTarget,
                RecommendationOrder = location.RecommendationOrder
            };
        }

        private static NdcLocation MapRequestToLocation(CreateLocationRequest request)
        {
            return new NdcLocation
            {
                Name = request.Name?.Trim(),
                NodeRemark = request.NodeRemark?.Trim(),
                Group = request.Group?.Trim(),
                WattingNode = request.WattingNode?.Trim(),
                LiftingHeight = request.LiftingHeight ?? 0,
                UnloadHeight = request.UnloadHeight ?? 0,
                LaneCode = request.LaneCode?.Trim(),
                DepthIndex = request.DepthIndex ?? 0,
                Lock = request.Lock ?? false,
                Enabled = request.Enabled ?? true,
                MaterialCode = request.MaterialCode,
                PalletID = request.PalletID ?? "0",
                Weight = request.Weight ?? "0",
                Quanitity = request.Quanitity ?? "0",
                EntryDate = request.EntryDate
            };
        }

        private static void ApplyUpdateRequest(NdcLocation location, UpdateLocationRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                location.Name = request.Name.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.NodeRemark))
            {
                location.NodeRemark = request.NodeRemark.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Group))
            {
                location.Group = request.Group.Trim();
            }

            if (request.WattingNode != null)
            {
                location.WattingNode = request.WattingNode.Trim();
            }

            if (request.LiftingHeight.HasValue)
            {
                location.LiftingHeight = request.LiftingHeight.Value;
            }

            if (request.UnloadHeight.HasValue)
            {
                location.UnloadHeight = request.UnloadHeight.Value;
            }

            if (request.LaneCode != null)
            {
                location.LaneCode = request.LaneCode.Trim();
            }

            if (request.DepthIndex.HasValue)
            {
                location.DepthIndex = request.DepthIndex.Value;
            }

            if (request.Lock.HasValue)
            {
                location.Lock = request.Lock.Value;
            }

            if (request.Enabled.HasValue)
            {
                location.Enabled = request.Enabled.Value;
            }

            if (request.MaterialCode != null)
            {
                location.MaterialCode = request.MaterialCode;
            }

            if (request.PalletID != null)
            {
                location.PalletID = request.PalletID;
            }

            if (request.Weight != null)
            {
                location.Weight = request.Weight;
            }

            if (request.Quanitity != null)
            {
                location.Quanitity = request.Quanitity;
            }

            if (request.EntryDate != null)
            {
                location.EntryDate = request.EntryDate;
            }
        }

        private static NdcLocation MapImportLocation(ImportLocationData source)
        {
            return new NdcLocation
            {
                Name = source.Name?.Trim(),
                NodeRemark = source.NodeRemark?.Trim(),
                Group = source.Group?.Trim(),
                WattingNode = source.WattingNode?.Trim(),
                LiftingHeight = source.LiftingHeight ?? 0,
                UnloadHeight = source.UnloadHeight ?? 0,
                LaneCode = source.LaneCode?.Trim(),
                DepthIndex = source.DepthIndex ?? 0,
                Lock = source.Lock ?? false,
                Enabled = source.Enabled ?? true,
                MaterialCode = source.MaterialCode,
                PalletID = source.PalletID ?? "0",
                Weight = source.Weight ?? "0",
                Quanitity = source.Quanitity ?? "0",
                EntryDate = source.EntryDate
            };
        }

        private static string? ValidateLocationRequest(CreateLocationRequest request, bool requireName)
        {
            if (requireName && string.IsNullOrWhiteSpace(request.Name))
            {
                return "储位名称不能为空";
            }

            if (string.IsNullOrWhiteSpace(request.NodeRemark))
            {
                return "节点备注不能为空";
            }

            if (string.IsNullOrWhiteSpace(request.Group))
            {
                return "分组不能为空";
            }

            if (string.IsNullOrWhiteSpace(request.LaneCode))
            {
                return "库道编号不能为空";
            }

            if (!request.DepthIndex.HasValue || request.DepthIndex.Value <= 0)
            {
                return "深度序号必须大于 0";
            }

            return null;
        }

        private static string? ValidateLocationEntity(NdcLocation location)
        {
            if (string.IsNullOrWhiteSpace(location.Name))
            {
                return "储位名称不能为空";
            }

            if (string.IsNullOrWhiteSpace(location.NodeRemark))
            {
                return "节点备注不能为空";
            }

            if (string.IsNullOrWhiteSpace(location.Group))
            {
                return "分组不能为空";
            }

            if (string.IsNullOrWhiteSpace(location.LaneCode))
            {
                return "库道编号不能为空";
            }

            if (location.DepthIndex <= 0)
            {
                return "深度序号必须大于 0";
            }

            return null;
        }

        private static string? ValidateImportLocation(ImportLocationData locationData)
        {
            if (string.IsNullOrWhiteSpace(locationData.Name))
            {
                return "储位名称不能为空";
            }

            if (string.IsNullOrWhiteSpace(locationData.NodeRemark))
            {
                return "节点备注不能为空";
            }

            if (string.IsNullOrWhiteSpace(locationData.Group))
            {
                return "分组不能为空";
            }

            if (string.IsNullOrWhiteSpace(locationData.LaneCode))
            {
                return "库道编号不能为空";
            }

            if (!locationData.DepthIndex.HasValue || locationData.DepthIndex.Value <= 0)
            {
                return "深度序号必须大于 0";
            }

            return null;
        }
    }

    /// <summary>
    /// 储位响应模型。
    /// </summary>
    public class LocationResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string NodeRemark { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string? MaterialCode { get; set; }
        public string? PalletID { get; set; }
        public string? Weight { get; set; }
        public string? Quanitity { get; set; }
        public string? EntryDate { get; set; }
        public int LiftingHeight { get; set; }
        public int UnloadHeight { get; set; }
        public string? WattingNode { get; set; }
        public string? LaneCode { get; set; }
        public int DepthIndex { get; set; }
        public bool IsEmpty { get; set; }
        public bool Lock { get; set; }
        public bool Enabled { get; set; }
    }

    /// <summary>
    /// 推荐储位响应模型。
    /// </summary>
    public class RecommendedLocationResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string NodeRemark { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string LaneCode { get; set; } = string.Empty;
        public int DepthIndex { get; set; }
        public string? WattingNode { get; set; }
        public bool IsEmpty { get; set; }
        public bool IsLocked { get; set; }
        public bool Enabled { get; set; }
        public string? MaterialCode { get; set; }
        public string? PalletID { get; set; }
        public bool IsReachableTarget { get; set; }
        public bool IsRecommendedTarget { get; set; }
        public int? RecommendationOrder { get; set; }
    }

    /// <summary>
    /// 创建储位请求。
    /// </summary>
    public class CreateLocationRequest
    {
        public string Name { get; set; } = string.Empty;
        public string NodeRemark { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string? WattingNode { get; set; }
        public int? LiftingHeight { get; set; }
        public int? UnloadHeight { get; set; }
        public string LaneCode { get; set; } = string.Empty;
        public int? DepthIndex { get; set; }
        public bool? Lock { get; set; }
        public bool? Enabled { get; set; }
        public string? MaterialCode { get; set; }
        public string? PalletID { get; set; }
        public string? Weight { get; set; }
        public string? Quanitity { get; set; }
        public string? EntryDate { get; set; }
    }

    /// <summary>
    /// 创建储位响应。
    /// </summary>
    public class CreateLocationResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// 更新储位请求。
    /// </summary>
    public class UpdateLocationRequest
    {
        public string? Name { get; set; }
        public string? NodeRemark { get; set; }
        public string? Group { get; set; }
        public string? WattingNode { get; set; }
        public int? LiftingHeight { get; set; }
        public int? UnloadHeight { get; set; }
        public string? LaneCode { get; set; }
        public int? DepthIndex { get; set; }
        public bool? Lock { get; set; }
        public bool? Enabled { get; set; }
        public string? MaterialCode { get; set; }
        public string? PalletID { get; set; }
        public string? Weight { get; set; }
        public string? Quanitity { get; set; }
        public string? EntryDate { get; set; }
    }

    /// <summary>
    /// 锁定请求。
    /// </summary>
    public class ToggleLockRequest
    {
        public bool LockState { get; set; }
    }

    /// <summary>
    /// 启用状态请求。
    /// </summary>
    public class ToggleEnabledRequest
    {
        public bool EnabledState { get; set; }
    }

    /// <summary>
    /// 批量操作请求。
    /// </summary>
    public class BatchOperationRequest
    {
        public List<int> Ids { get; set; } = new();
    }

    /// <summary>
    /// 批量切换锁定请求。
    /// </summary>
    public class BatchToggleLockRequest
    {
        public List<int> Ids { get; set; } = new();
        public bool LockState { get; set; }
    }

    /// <summary>
    /// 批量操作响应。
    /// </summary>
    public class BatchOperationResponse
    {
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
    }

    /// <summary>
    /// 批量导入请求。
    /// </summary>
    public class BatchImportRequest
    {
        public List<ImportLocationData> Locations { get; set; } = new();
    }

    /// <summary>
    /// 导入储位数据。
    /// </summary>
    public class ImportLocationData
    {
        public string Name { get; set; } = string.Empty;
        public string NodeRemark { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string? WattingNode { get; set; }
        public int? LiftingHeight { get; set; }
        public int? UnloadHeight { get; set; }
        public string LaneCode { get; set; } = string.Empty;
        public int? DepthIndex { get; set; }
        public bool? Lock { get; set; }
        public bool? Enabled { get; set; }
        public string? MaterialCode { get; set; }
        public string? PalletID { get; set; }
        public string? Weight { get; set; }
        public string? Quanitity { get; set; }
        public string? EntryDate { get; set; }
    }

    /// <summary>
    /// 批量导入响应。
    /// </summary>
    public class BatchImportResponse
    {
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// 物料转移/移库请求。
    /// </summary>
    public class TransferMaterialRequest
    {
        public int SourceLocationId { get; set; }
        public int TargetLocationId { get; set; }
    }

    /// <summary>
    /// 移库响应。
    /// </summary>
    public class RelocateMaterialResponse
    {
        public int TaskId { get; set; }
        public string SourceLocation { get; set; } = string.Empty;
        public string TargetLocation { get; set; } = string.Empty;
        public string MaterialCode { get; set; } = string.Empty;
    }
}
