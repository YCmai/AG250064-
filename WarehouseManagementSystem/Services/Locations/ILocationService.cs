using WarehouseManagementSystem.Models.Ndc;
using System.Data;
using WarehouseManagementSystem.Db;
using Dapper;
using WarehouseManagementSystem.Shared.Ndc;

public interface ILocationService
{
    Task<(IEnumerable<NdcLocation> Items, int TotalItems)> GetLocations(string? searchString = null, int page = 1, int pageSize = 10);
    Task<(IEnumerable<NdcLocation> Items, int TotalCount)> GetSearchLocations(string searchString, int page, int pageSize);
    Task<NdcLocation> GetLocationById(int id);
    Task<(bool Success, string Message)> CreateOrUpdateLocation(NdcLocation location);
    Task<(bool Success, string Message)> HandleLocationOperation(int id, int type);
    Task<(int Available, int Used)> GetStorageCapacityStats();
    Task<(List<NdcLocation> Items, int TotalItems, int Available, int Used)> GetLocationsWithStats(string searchString = "", int page = 1);

    // 按区域批量清空物料
    Task<(bool success, string message, int affectedCount)> BatchClearMaterials(string group);

    // 按区域批量锁定/解锁储位
    Task<(bool success, string message, int affectedCount)> BatchToggleLock(string group, bool lockState);

    // 按ID列表批量清空物料
    Task<(bool success, string message, int affectedCount)> BatchClearMaterialsByIds(List<int> locationIds);

    // 按ID列表批量锁定/解锁储位
    Task<(bool success, string message, int affectedCount)> BatchToggleLockByIds(List<int> locationIds, bool lockState);

    // 创建AGV移库任务
    Task<(bool success, string message, int taskId)> CreateRelocateTask(string sourcePosition, string targetPosition, string materialCode);
}

public class LocationService : ILocationService
{
    private readonly IDatabaseService _db;
    private readonly ILogger<LocationService> _logger;

    public LocationService(IDatabaseService db, ILogger<LocationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(bool success, string message, int affectedCount)> BatchClearMaterialsByIds(List<int> locationIds)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            int affectedCount = await connection.ExecuteAsync(@"
            UPDATE RCS_Locations
            SET MaterialCode = NULL, PalletID = '0', Weight = '0', Quanitity = '0', EntryDate = NULL
            WHERE Id IN @LocationIds",
                new { LocationIds = locationIds },
                transaction);
            transaction.Commit();
            return (true, $"成功清空 {affectedCount} 个储位的物料", affectedCount);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "批量清空储位物料失败");
            return (false, "清空物料失败，请稍后再试", 0);
        }
    }

    public async Task<(bool success, string message, int affectedCount)> BatchToggleLockByIds(List<int> locationIds, bool lockState)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            string operation = lockState ? "锁定" : "解锁";
            int affectedCount = await connection.ExecuteAsync(@"
            UPDATE RCS_Locations
            SET Lock = @LockState
            WHERE Id IN @LocationIds AND Lock <> @LockState",
                new { LocationIds = locationIds, LockState = lockState ? 1 : 0 },
                transaction);
            transaction.Commit();
            return (true, $"成功{operation} {affectedCount} 个储位", affectedCount);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, $"批量锁定/解锁储位失败");
            return (false, $"批量操作失败，请稍后再试", 0);
        }
    }

    public async Task<(IEnumerable<NdcLocation> Items, int TotalCount)> GetSearchLocations(string searchString, int page, int pageSize)
    {
        using var connection = _db.CreateConnection();
        try
        {
            var whereClause = string.IsNullOrEmpty(searchString) ? "" : "WHERE NodeRemark LIKE @Search OR MaterialCode LIKE @Search OR Name LIKE @Search";
            var countSql = $"SELECT COUNT(*) FROM RCS_Locations {whereClause}";
            var totalCount = await connection.ExecuteScalarAsync<int>(countSql, new { Search = $"%{searchString}%" });
            var sql = $@"SELECT * FROM RCS_Locations {whereClause}";
            var items = await connection.QueryAsync<NdcLocation>(sql, new { Search = $"%{searchString}%" });
            var result = items.Skip((page - 1) * pageSize).Take(pageSize);
            return (result, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取库位列表失败");
            throw;
        }
    }

    public async Task<(IEnumerable<NdcLocation> Items, int TotalItems)> GetLocations(string? searchString = null, int page = 1, int pageSize = 10)
    {
        try
        {
            using var conn = _db.CreateConnection();
            var allItems = await conn.QueryAsync<NdcLocation>("SELECT * FROM RCS_Locations");
            var filtered = allItems.ToList();
            if (!string.IsNullOrEmpty(searchString))
            {
                filtered = filtered.Where(x => x.NodeRemark != null && x.NodeRemark.Contains(searchString)).ToList();
            }
            var totalItems = filtered.Count;
            var items = filtered.OrderBy(x => x.Group).ThenBy(x => x.NodeRemark).Skip((page - 1) * pageSize).Take(pageSize).ToList();
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
        using var connection = _db.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            int affectedCount = await connection.ExecuteAsync(@"
                UPDATE RCS_Locations
                SET MaterialCode = NULL, PalletID = '0', Weight = '0', Quanitity = '0', EntryDate = NULL
                WHERE [Group] = @Group",
                new { Group = group }, transaction);
            transaction.Commit();
            return (true, $"成功清空区域 {group} 中的 {affectedCount} 个储位物料", affectedCount);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, $"批量清空区域 {group} 物料失败");
            return (false, "清空物料失败，请稍后再试", 0);
        }
    }

    public async Task<(bool success, string message, int affectedCount)> BatchToggleLock(string group, bool lockState)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            string operation = lockState ? "锁定" : "解锁";
            int affectedCount = await connection.ExecuteAsync(@"
                UPDATE RCS_Locations SET Lock = @LockState WHERE [Group] = @Group AND Lock <> @LockState",
                new { Group = group, LockState = lockState ? 1 : 0 }, transaction);
            transaction.Commit();
            return (true, $"成功{operation}区域 {group} 中的 {affectedCount} 个储位", affectedCount);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, $"批量锁定/解锁区域 {group} 储位失败");
            return (false, $"批量操作失败，请稍后再试", 0);
        }
    }

    public async Task<NdcLocation> GetLocationById(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<NdcLocation>("SELECT * FROM RCS_Locations WHERE Id = @Id", new { Id = id });
    }

    public async Task<(bool Success, string Message)> CreateOrUpdateLocation(NdcLocation location)
    {
        try
        {
            using var conn = _db.CreateConnection();
            var existing = await conn.QueryFirstOrDefaultAsync<NdcLocation>("SELECT * FROM RCS_Locations WHERE NodeRemark = @NodeRemark", new { location.NodeRemark });
            if (existing != null)
            {
                var sql = @"UPDATE RCS_Locations SET Name = @Name, NodeRemark = @NodeRemark, MaterialCode = @MaterialCode, PalletID = @PalletID, Weight = @Weight, Quanitity = @Quanitity, EntryDate = @EntryDate, [Group] = @Group, LiftingHeight = @LiftingHeight, Lock = @Lock, WattingNode = @WattingNode, UnloadHeight = @UnloadHeight WHERE NodeRemark = @NodeRemark";
                await conn.ExecuteAsync(sql, location);
                return (true, "修改成功");
            }
            else
            {
                var sql = @"INSERT INTO RCS_Locations (Name, NodeRemark, MaterialCode, PalletID, Weight, Quanitity, EntryDate, [Group], LiftingHeight, Lock, WattingNode, UnloadHeight, CreatedTime) VALUES (@Name, @NodeRemark, @MaterialCode, @PalletID, @Weight, @Quanitity, @EntryDate, @Group, @LiftingHeight, @Lock, @WattingNode, @UnloadHeight, @CreatedTime)";
                await conn.ExecuteAsync(sql, location);
                return (true, "新存储位置已成功创建！");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存库位信息失败");
            throw;
        }
    }

    public async Task<(bool Success, string Message)> HandleLocationOperation(int id, int type)
    {
        try
        {
            using var conn = _db.CreateConnection();
            var location = await conn.QueryFirstOrDefaultAsync<NdcLocation>("SELECT * FROM RCS_Locations WHERE Id = @Id", new { Id = id });
            if (location == null) return (false, "操作失败，找不到该储位。");
            switch (type)
            {
                case 1: // 清空物料
                    await conn.ExecuteAsync("UPDATE RCS_Locations SET MaterialCode = NULL, PalletID = '0', Weight = '0', Quanitity = '0', EntryDate = NULL WHERE Id = @Id", new { Id = id });
                    return (true, "物料清空成功！");
                case 2: // 切换锁定状态
                    await conn.ExecuteAsync("UPDATE RCS_Locations SET Lock = @Lock WHERE Id = @Id", new { Id = id, Lock = !location.Lock });
                    return (true, location.Lock ? "储位解锁成功！" : "储位锁定成功！");
                case 3: // 删除（硬删除）
                    await conn.ExecuteAsync("DELETE FROM RCS_Locations WHERE Id = @Id", new { Id = id });
                    return (true, "储位删除成功！");
                case 4: // 重置异常物料
                    if (location.MaterialCode != null && location.MaterialCode.StartsWith("Err_"))
                    {
                        await conn.ExecuteAsync("UPDATE RCS_Locations SET MaterialCode = @MaterialCode WHERE Id = @Id", new { Id = id, MaterialCode = location.MaterialCode.Replace("Err_", "") });
                        return (true, "异常物料重置成功！");
                    }
                    return (false, "该储位不包含异常物料！");
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
            using var conn = _db.CreateConnection();
            var locations = await conn.QueryAsync<NdcLocation>("SELECT MaterialCode FROM RCS_Locations");
            var used = locations.Count(loc => !string.IsNullOrEmpty(loc.MaterialCode) && loc.MaterialCode != "empty");
            return (locations.Count(), used);
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
            using var conn = _db.CreateConnection();
            const int pageSize = 15;
            var query = "SELECT * FROM RCS_Locations";
            var countQuery = "SELECT COUNT(*) FROM RCS_Locations";
            var parameters = new DynamicParameters();
            if (!string.IsNullOrEmpty(searchString)) { query += " WHERE NodeRemark LIKE @Search"; countQuery += " WHERE NodeRemark LIKE @Search"; parameters.Add("@Search", $"%{searchString}%"); }
            query += " ORDER BY [Group] OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
            parameters.Add("@Offset", (page - 1) * pageSize); parameters.Add("@PageSize", pageSize);
            var items = await conn.QueryAsync<NdcLocation>(query, parameters);
            var totalItems = await conn.ExecuteScalarAsync<int>(countQuery, parameters);
            var (total, used) = await GetStorageCapacityStats();
            return (items.ToList(), totalItems, total, used);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取库位列表和统计信息失败");
            return (new List<NdcLocation>(), 0, 0, 0);
        }
    }

    public async Task<(bool success, string message, int taskId)> CreateRelocateTask(string sourcePosition, string targetPosition, string materialCode)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            string requestCode = $"RELOCATE_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}";
            string systemType = await connection.QueryFirstOrDefaultAsync<string>("SELECT Value FROM SystemSettings WHERE [Key] = 'SystemType'", transaction: transaction) ?? "Heartbeat";
            int initialStatus = systemType.Trim().Equals("NDC", StringComparison.OrdinalIgnoreCase) ? -1 : 0;
            var task = new NdcUserTask
            {
                taskStatus = (TaskStatuEnum)initialStatus,
                executed = false,
                creatTime = DateTime.Now,
                requestCode = requestCode,
                taskType = TaskTypeEnum.Transfer,
                priority = 1,
                robotCode = "0",
                sourcePosition = sourcePosition,
                targetPosition = targetPosition,
                IsCancelled = false
            };
            string insertSql = @"INSERT INTO RCS_UserTasks (taskStatus, executed, creatTime, requestCode, taskType, priority, robotCode, sourcePosition, targetPosition, IsCancelled) VALUES (@taskStatus, @executed, @creatTime, @requestCode, @taskType, @priority, @robotCode, @sourcePosition, @targetPosition, @IsCancelled); SELECT CAST(SCOPE_IDENTITY() as int);";
            int taskId = await connection.ExecuteScalarAsync<int>(insertSql, task, transaction);
            int lockedCount = await connection.ExecuteAsync("UPDATE RCS_Locations SET Lock = 1 WHERE NodeRemark IN (@SourcePosition, @TargetPosition)", new { SourcePosition = sourcePosition, TargetPosition = targetPosition }, transaction);
            if (lockedCount < 2) { transaction.Rollback(); return (false, "锁定储位失败，任务创建已取消", 0); }
            transaction.Commit();
            return (true, "移库任务创建成功，起点和终点已锁定", taskId);
        }
        catch (Exception ex) { transaction.Rollback(); _logger.LogError(ex, $"创建AGV移库任务失败"); return (false, $"创建移库任务失败: {ex.Message}", 0); }
    }
}

