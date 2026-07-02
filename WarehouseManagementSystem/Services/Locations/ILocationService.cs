using Dapper;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models.Enums;
using WarehouseManagementSystem.Models.Ndc;
using WarehouseManagementSystem.Services.Tasks;

public interface ILocationService
{
    /// <summary>
    /// 获取储位列表。
    /// </summary>
    /// <param name="searchString">搜索关键字。</param>
    /// <param name="page">页码。</param>
    /// <param name="pageSize">每页数量。</param>
    /// <returns>分页储位列表。</returns>
    Task<(IEnumerable<NdcLocation> Items, int TotalItems)> GetLocations(string? searchString = null, int page = 1, int pageSize = 10);

    /// <summary>
    /// 获取搜索结果列表。
    /// </summary>
    /// <param name="searchString">搜索关键字。</param>
    /// <param name="page">页码。</param>
    /// <param name="pageSize">每页数量。</param>
    /// <returns>分页储位列表。</returns>
    Task<(IEnumerable<NdcLocation> Items, int TotalCount)> GetSearchLocations(string searchString, int page, int pageSize);

    /// <summary>
    /// 按 ID 获取储位详情。
    /// </summary>
    /// <param name="id">储位 ID。</param>
    /// <returns>储位实体，未找到时返回 null。</returns>
    Task<NdcLocation?> GetLocationById(int id);

    /// <summary>
    /// 创建或更新储位。
    /// </summary>
    /// <param name="location">待保存的储位。</param>
    /// <returns>是否成功及消息。</returns>
    Task<(bool Success, string Message)> CreateOrUpdateLocation(NdcLocation location);

    /// <summary>
    /// 处理单个储位的标准操作。
    /// </summary>
    /// <param name="id">储位 ID。</param>
    /// <param name="type">操作类型。</param>
    /// <param name="enabledState">目标启用状态。</param>
    /// <returns>是否成功及消息。</returns>
    Task<(bool Success, string Message)> HandleLocationOperation(int id, int type, bool? enabledState = null);

    /// <summary>
    /// 获取库容统计。
    /// </summary>
    /// <returns>总储位和已用储位数量。</returns>
    Task<(int Available, int Used)> GetStorageCapacityStats();

    /// <summary>
    /// 获取储位列表及统计。
    /// </summary>
    /// <param name="searchString">搜索关键字。</param>
    /// <param name="page">页码。</param>
    /// <returns>数据集合及统计信息。</returns>
    Task<(List<NdcLocation> Items, int TotalItems, int Available, int Used)> GetLocationsWithStats(string searchString = "", int page = 1);

    /// <summary>
    /// 按区域批量清空物料。
    /// </summary>
    /// <param name="group">分组。</param>
    /// <returns>执行结果。</returns>
    Task<(bool success, string message, int affectedCount)> BatchClearMaterials(string group);

    /// <summary>
    /// 按区域批量锁定或解锁储位。
    /// </summary>
    /// <param name="group">分组。</param>
    /// <param name="lockState">目标锁定状态。</param>
    /// <returns>执行结果。</returns>
    Task<(bool success, string message, int affectedCount)> BatchToggleLock(string group, bool lockState);

    /// <summary>
    /// 按 ID 列表批量清空物料。
    /// </summary>
    /// <param name="locationIds">储位 ID 列表。</param>
    /// <returns>执行结果。</returns>
    Task<(bool success, string message, int affectedCount)> BatchClearMaterialsByIds(List<int> locationIds);

    /// <summary>
    /// 按 ID 列表批量锁定或解锁储位。
    /// </summary>
    /// <param name="locationIds">储位 ID 列表。</param>
    /// <param name="lockState">目标锁定状态。</param>
    /// <returns>执行结果。</returns>
    Task<(bool success, string message, int affectedCount)> BatchToggleLockByIds(List<int> locationIds, bool lockState);

    /// <summary>
    /// 创建 AGV 移库任务。
    /// </summary>
    /// <param name="sourcePosition">源储位节点备注。</param>
    /// <param name="targetPosition">目标储位节点备注。</param>
    /// <param name="materialCode">物料编码。</param>
    /// <param name="requestedTaskType">指定内部任务类型；为空时默认按移库任务创建。</param>
    /// <param name="priority">指定任务优先级；为空时默认使用 1。</param>
    /// <returns>任务创建结果。</returns>
    Task<(bool success, string message, int taskId)> CreateRelocateTask(
        string sourcePosition,
        string targetPosition,
        string materialCode,
        TaskTypeEnum? requestedTaskType = null,
        int? priority = null);

    /// <summary>
    /// 获取标准化的储位推荐结果。
    /// </summary>
    /// <param name="excludeLocationId">需要从结果中排除的储位 ID，例如当前源储位。</param>
    /// <returns>按标准逻辑排序后的推荐结果。</returns>
    Task<List<RecommendedLocationResult>> GetRecommendedLocations(int? excludeLocationId = null);

    /// <summary>
    /// 获取全部储位分组。
    /// </summary>
    /// <returns>按名称排序后的分组列表。</returns>
    Task<List<string>> GetLocationGroups();
}

/// <summary>
/// 标准化储位推荐结果。
/// </summary>
public class RecommendedLocationResult
{
    /// <summary>储位 ID。</summary>
    public int Id { get; set; }

    /// <summary>储位名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>节点备注。</summary>
    public string NodeRemark { get; set; } = string.Empty;

    /// <summary>分组。</summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>库道编号。</summary>
    public string LaneCode { get; set; } = string.Empty;

    /// <summary>深度序号。</summary>
    public int DepthIndex { get; set; }

    /// <summary>等待点。</summary>
    public string? WattingNode { get; set; }

    /// <summary>是否为空位。</summary>
    public bool IsEmpty { get; set; }

    /// <summary>是否锁定。</summary>
    public bool IsLocked { get; set; }

    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; }

    /// <summary>物料编码。</summary>
    public string? MaterialCode { get; set; }

    /// <summary>托盘编号。</summary>
    public string? PalletID { get; set; }

    /// <summary>标准规则下是否可作为目标储位。</summary>
    public bool IsReachableTarget { get; set; }

    /// <summary>是否推荐为目标储位。</summary>
    public bool IsRecommendedTarget { get; set; }

    /// <summary>推荐顺序。值越小优先级越高。</summary>
    public int? RecommendationOrder { get; set; }
}

public class LocationService : ILocationService
{
    private readonly IDatabaseService _db;
    private readonly ILocationRepository _locationRepository;
    private readonly IUserTaskCreationService _userTaskCreationService;
    private readonly ILogger<LocationService> _logger;

    public LocationService(
        IDatabaseService db,
        ILocationRepository locationRepository,
        IUserTaskCreationService userTaskCreationService,
        ILogger<LocationService> logger)
    {
        _db = db;
        _locationRepository = locationRepository;
        _userTaskCreationService = userTaskCreationService;
        _logger = logger;
    }

    public async Task<(bool success, string message, int affectedCount)> BatchClearMaterialsByIds(List<int> locationIds)
    {
        try
        {
            var affectedCount = await _locationRepository.BatchClearMaterialsByIdsAsync(locationIds);
            return (true, $"成功清空 {affectedCount} 个储位的物料", affectedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量清空储位物料失败");
            return (false, "清空物料失败，请稍后再试", 0);
        }
    }

    public async Task<(bool success, string message, int affectedCount)> BatchToggleLockByIds(List<int> locationIds, bool lockState)
    {
        try
        {
            var operation = lockState ? "锁定" : "解锁";
            var affectedCount = await _locationRepository.BatchToggleLockByIdsAsync(locationIds, lockState);
            return (true, $"成功{operation} {affectedCount} 个储位", affectedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量锁定/解锁储位失败");
            return (false, "批量操作失败，请稍后再试", 0);
        }
    }

    public async Task<(IEnumerable<NdcLocation> Items, int TotalCount)> GetSearchLocations(string searchString, int page, int pageSize)
    {
        try
        {
            var (items, totalItems) = await _locationRepository.GetPagedAsync(searchString, page, pageSize);
            return (items, totalItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取库位搜索列表失败");
            throw;
        }
    }

    public async Task<(IEnumerable<NdcLocation> Items, int TotalItems)> GetLocations(string? searchString = null, int page = 1, int pageSize = 10)
    {
        try
        {
            var (items, totalItems) = await _locationRepository.GetPagedAsync(searchString, page, pageSize);
            return (items, totalItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取库位列表失败");
            throw;
        }
    }

    public async Task<(bool success, string message, int affectedCount)> BatchClearMaterials(string group)
    {
        try
        {
            var affectedCount = await _locationRepository.BatchClearMaterialsByGroupAsync(group);
            return (true, $"成功清空区域 {group} 中的 {affectedCount} 个储位物料", affectedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量清空区域 {Group} 物料失败", group);
            return (false, "清空物料失败，请稍后再试", 0);
        }
    }

    public async Task<(bool success, string message, int affectedCount)> BatchToggleLock(string group, bool lockState)
    {
        try
        {
            var operation = lockState ? "锁定" : "解锁";
            var affectedCount = await _locationRepository.BatchToggleLockByGroupAsync(group, lockState);
            return (true, $"成功{operation}区域 {group} 中的 {affectedCount} 个储位", affectedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量锁定/解锁区域 {Group} 储位失败", group);
            return (false, "批量操作失败，请稍后再试", 0);
        }
    }

    public Task<NdcLocation?> GetLocationById(int id)
    {
        return _locationRepository.GetByIdAsync(id);
    }

    public async Task<(bool Success, string Message)> CreateOrUpdateLocation(NdcLocation location)
    {
        try
        {
            NormalizeLocation(location);
            var validationMessage = await ValidateLocationAsync(location);
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                return (false, validationMessage);
            }

            var existingById = location.Id > 0
                ? await _locationRepository.GetByIdAsync(location.Id)
                : null;

            if (existingById != null)
            {
                await _locationRepository.UpdateAsync(location);
                return (true, "修改成功");
            }

            location.Id = await _locationRepository.InsertAsync(location);
            return (true, "新存储位置已成功创建！");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存库位信息失败");
            return (false, $"保存库位信息失败: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> HandleLocationOperation(int id, int type, bool? enabledState = null)
    {
        try
        {
            var location = await _locationRepository.GetByIdAsync(id);
            if (location == null)
            {
                return (false, "操作失败，找不到该储位。");
            }

            switch (type)
            {
                case 1:
                    await _locationRepository.ClearMaterialAsync(id);
                    return (true, "物料清空成功！");
                case 2:
                    await _locationRepository.SetLockStateAsync(id, !location.Lock);
                    return (true, location.Lock ? "储位解锁成功！" : "储位锁定成功！");
                case 3:
                    await _locationRepository.DeleteAsync(id);
                    return (true, "储位删除成功！");
                case 4:
                    if (location.MaterialCode != null && location.MaterialCode.StartsWith("Err_", StringComparison.OrdinalIgnoreCase))
                    {
                        location.MaterialCode = location.MaterialCode.Replace("Err_", string.Empty, StringComparison.OrdinalIgnoreCase);
                        await _locationRepository.UpdateAsync(location);
                        return (true, "异常物料重置成功！");
                    }

                    return (false, "该储位不包含异常物料！");
                case 5:
                    if (!enabledState.HasValue)
                    {
                        return (false, "缺少目标启用状态！");
                    }

                    await _locationRepository.SetEnabledStateAsync(id, enabledState.Value);
                    return (true, enabledState.Value ? "储位启用成功！" : "储位禁用成功！");
                default:
                    return (false, "无效的操作类型！");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "操作失败");
            return (false, $"操作失败: {ex.Message}");
        }
    }

    public async Task<(int Available, int Used)> GetStorageCapacityStats()
    {
        try
        {
            var locations = await _locationRepository.GetAllAsync();
            var used = locations.Count(IsMaterialPresent);
            return (locations.Count, used);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取存储容量统计失败");
            return (0, 0);
        }
    }

    public async Task<(List<NdcLocation> Items, int TotalItems, int Available, int Used)> GetLocationsWithStats(string searchString = "", int page = 1)
    {
        try
        {
            const int pageSize = 15;
            var (items, totalItems) = await _locationRepository.GetPagedAsync(searchString, page, pageSize);
            var (available, used) = await GetStorageCapacityStats();
            return (items, totalItems, available, used);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取库位列表和统计信息失败");
            return (new List<NdcLocation>(), 0, 0, 0);
        }
    }

    public async Task<(bool success, string message, int taskId)> CreateRelocateTask(
        string sourcePosition,
        string targetPosition,
        string materialCode,
        TaskTypeEnum? requestedTaskType = null,
        int? priority = null)
    {
        try
        {
            var result = await _userTaskCreationService.CreateTaskAsync(new UserTaskCreationRequest
            {
                SourceType = UserTaskSourceType.Manual,
                SourcePosition = sourcePosition,
                TargetPosition = targetPosition,
                MaterialCode = materialCode,
                RequestedTaskType = requestedTaskType,
                Priority = priority,
                Remarks = "Manual relocate task",
                ValidateLocationExistence = true,
                ValidateReachableTarget = true,
                LockLocations = true
            });

            if (!result.Success)
            {
                return (false, result.Message, 0);
            }

            return (true, "移库任务创建成功，起点和终点已锁定", result.TaskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建AGV移库任务失败");
            return (false, $"创建移库任务失败: {ex.Message}", 0);
        }
    }

    public async Task<List<RecommendedLocationResult>> GetRecommendedLocations(int? excludeLocationId = null)
    {
        var locations = await _locationRepository.GetAllAsync();
        if (excludeLocationId.HasValue)
        {
            locations = locations.Where(item => item.Id != excludeLocationId.Value).ToList();
        }

        return await BuildRecommendedLocationsAsync(locations);
    }

    public Task<List<string>> GetLocationGroups()
    {
        return _locationRepository.GetGroupsAsync();
    }

    private async Task<string?> ValidateLocationAsync(NdcLocation location)
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

        var exists = await _locationRepository.ExistsNodeRemarkAsync(location.NodeRemark!, location.Id > 0 ? location.Id : null);
        if (exists)
        {
            return "节点备注已存在";
        }

        return null;
    }

    private static void NormalizeLocation(NdcLocation location)
    {
        location.Name = location.Name?.Trim();
        location.NodeRemark = location.NodeRemark?.Trim();
        location.Group = location.Group?.Trim();
        location.WattingNode = location.WattingNode?.Trim();
        location.LaneCode = location.LaneCode?.Trim();
        location.PalletID ??= "0";
        location.Weight ??= "0";
        location.Quanitity ??= "0";
    }

    private async Task<List<RecommendedLocationResult>> BuildRecommendedLocationsAsync(List<NdcLocation> locations)
    {
        var laneGroups = locations
            .Where(HasStructuredLaneInfo)
            .GroupBy(location => location.LaneCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DepthIndex).ThenBy(item => item.NodeRemark).ToList(), StringComparer.OrdinalIgnoreCase);

        var recommendationIndex = 1;
        var standardCandidates = new List<RecommendedLocationResult>();
        var fallbackCandidates = new List<RecommendedLocationResult>();

        foreach (var location in locations)
        {
            var result = new RecommendedLocationResult
            {
                Id = location.Id,
                Name = location.Name ?? string.Empty,
                NodeRemark = location.NodeRemark ?? string.Empty,
                Group = location.Group ?? string.Empty,
                LaneCode = location.LaneCode ?? string.Empty,
                DepthIndex = location.DepthIndex,
                WattingNode = location.WattingNode,
                IsEmpty = !IsMaterialPresent(location),
                IsLocked = location.Lock,
                Enabled = location.Enabled,
                MaterialCode = location.MaterialCode,
                PalletID = location.PalletID
            };

            if (HasStructuredLaneInfo(location))
            {
                var laneLocations = laneGroups[location.LaneCode!];
                result.IsReachableTarget = IsReachableTarget(location, laneLocations);
                result.IsRecommendedTarget = result.IsReachableTarget && result.IsEmpty && location.Enabled && !location.Lock;

                if (result.IsRecommendedTarget)
                {
                    standardCandidates.Add(result);
                }
                else
                {
                    fallbackCandidates.Add(result);
                }
            }
            else
            {
                result.IsReachableTarget = result.IsEmpty && location.Enabled && !location.Lock;
                result.IsRecommendedTarget = false;
                fallbackCandidates.Add(result);
            }
        }

        var orderedStandardCandidates = standardCandidates
            .OrderBy(item => item.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.LaneCode, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(item => item.DepthIndex)
            .ThenBy(item => item.NodeRemark, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var item in orderedStandardCandidates)
        {
            item.RecommendationOrder = recommendationIndex++;
        }

        return orderedStandardCandidates
            .Concat(fallbackCandidates
                .OrderByDescending(item => item.IsReachableTarget)
                .ThenBy(item => item.Group, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => string.IsNullOrWhiteSpace(item.LaneCode) ? 1 : 0)
                .ThenBy(item => item.LaneCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.DepthIndex <= 0 ? int.MaxValue : item.DepthIndex)
                .ThenBy(item => item.NodeRemark, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool IsReachableTarget(NdcLocation candidate, List<NdcLocation> laneLocations)
    {
        if (!candidate.Enabled || candidate.Lock || IsMaterialPresent(candidate))
        {
            return false;
        }

        if (!HasStructuredLaneInfo(candidate))
        {
            return true;
        }

        var outerLocations = laneLocations
            .Where(item => item.DepthIndex < candidate.DepthIndex)
            .OrderBy(item => item.DepthIndex)
            .ToList();

        // Why: 深位储位只有在外侧每一层都已被占用时才真正可达，避免继续依赖名称推断前后关系。
        return outerLocations.All(IsMaterialPresent);
    }

    private static bool HasStructuredLaneInfo(NdcLocation location)
    {
        return !string.IsNullOrWhiteSpace(location.LaneCode) && location.DepthIndex > 0;
    }

    private static bool IsMaterialPresent(NdcLocation location)
    {
        return !string.IsNullOrWhiteSpace(location.MaterialCode) &&
               !string.Equals(location.MaterialCode, "0", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(location.MaterialCode, "empty", StringComparison.OrdinalIgnoreCase);
    }
}
