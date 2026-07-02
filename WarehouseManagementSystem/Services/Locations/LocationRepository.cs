using System.Data;
using Dapper;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models.Ndc;

public interface ILocationRepository
{
    /// <summary>
    /// 按条件分页获取储位列表。
    /// </summary>
    /// <param name="searchString">搜索关键字。</param>
    /// <param name="page">页码，从 1 开始。</param>
    /// <param name="pageSize">每页数量。</param>
    /// <returns>分页结果。</returns>
    Task<(List<NdcLocation> Items, int TotalItems)> GetPagedAsync(string? searchString, int page, int pageSize);

    /// <summary>
    /// 获取全部储位，用于推荐、统计等需要完整上下文的场景。
    /// </summary>
    /// <param name="searchString">可选搜索关键字。</param>
    /// <returns>储位集合。</returns>
    Task<List<NdcLocation>> GetAllAsync(string? searchString = null);

    /// <summary>
    /// 获取全部已配置的储位分组。
    /// </summary>
    /// <returns>按名称排序后的分组列表。</returns>
    Task<List<string>> GetGroupsAsync();

    /// <summary>
    /// 按主键获取储位。
    /// </summary>
    /// <param name="id">储位 ID。</param>
    /// <returns>储位实体，未找到时返回 null。</returns>
    Task<NdcLocation?> GetByIdAsync(int id);

    /// <summary>
    /// 按节点备注获取储位。
    /// </summary>
    /// <param name="nodeRemark">节点备注。</param>
    /// <returns>储位实体，未找到时返回 null。</returns>
    Task<NdcLocation?> GetByNodeRemarkAsync(string nodeRemark);

    /// <summary>
    /// 检查节点备注是否已存在。
    /// </summary>
    /// <param name="nodeRemark">节点备注。</param>
    /// <param name="excludeId">更新场景下排除的储位 ID。</param>
    /// <returns>是否存在重复节点备注。</returns>
    Task<bool> ExistsNodeRemarkAsync(string nodeRemark, int? excludeId = null);

    /// <summary>
    /// 新增储位。
    /// </summary>
    /// <param name="location">储位实体。</param>
    /// <returns>新储位 ID。</returns>
    Task<int> InsertAsync(NdcLocation location);

    /// <summary>
    /// 更新储位。
    /// </summary>
    /// <param name="location">储位实体。</param>
    Task UpdateAsync(NdcLocation location);

    /// <summary>
    /// 删除储位。
    /// </summary>
    /// <param name="id">储位 ID。</param>
    Task DeleteAsync(int id);

    /// <summary>
    /// 清空单个储位的物料信息。
    /// </summary>
    /// <param name="id">储位 ID。</param>
    Task ClearMaterialAsync(int id);

    /// <summary>
    /// 设置单个储位锁定状态。
    /// </summary>
    /// <param name="id">储位 ID。</param>
    /// <param name="lockState">锁定状态。</param>
    Task SetLockStateAsync(int id, bool lockState);

    /// <summary>
    /// 设置单个储位启用状态。
    /// </summary>
    /// <param name="id">储位 ID。</param>
    /// <param name="enabledState">启用状态。</param>
    Task SetEnabledStateAsync(int id, bool enabledState);

    /// <summary>
    /// 按 ID 列表批量清空物料。
    /// </summary>
    /// <param name="locationIds">储位 ID 列表。</param>
    /// <returns>影响行数。</returns>
    Task<int> BatchClearMaterialsByIdsAsync(List<int> locationIds);

    /// <summary>
    /// 按 ID 列表批量设置锁定状态。
    /// </summary>
    /// <param name="locationIds">储位 ID 列表。</param>
    /// <param name="lockState">目标锁定状态。</param>
    /// <returns>影响行数。</returns>
    Task<int> BatchToggleLockByIdsAsync(List<int> locationIds, bool lockState);

    /// <summary>
    /// 按分组批量清空物料。
    /// </summary>
    /// <param name="group">分组。</param>
    /// <returns>影响行数。</returns>
    Task<int> BatchClearMaterialsByGroupAsync(string group);

    /// <summary>
    /// 按分组批量设置锁定状态。
    /// </summary>
    /// <param name="group">分组。</param>
    /// <param name="lockState">目标锁定状态。</param>
    /// <returns>影响行数。</returns>
    Task<int> BatchToggleLockByGroupAsync(string group, bool lockState);
}

public class LocationRepository : ILocationRepository
{
    private readonly IDatabaseService _databaseService;

    public LocationRepository(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<(List<NdcLocation> Items, int TotalItems)> GetPagedAsync(string? searchString, int page, int pageSize)
    {
        using var connection = _databaseService.CreateConnection();
        var whereClause = BuildSearchWhereClause(searchString);
        var parameters = BuildSearchParameters(searchString, page, pageSize);
        var totalItems = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM RCS_Locations {whereClause}",
            parameters);

        var sql = $@"
SELECT *
FROM RCS_Locations
{whereClause}
ORDER BY [Group],
         CASE WHEN ISNULL(LaneCode, '') = '' THEN 1 ELSE 0 END,
         LaneCode,
         CASE WHEN ISNULL(DepthIndex, 0) <= 0 THEN 2147483647 ELSE DepthIndex END,
         NodeRemark
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        var items = (await connection.QueryAsync<NdcLocation>(sql, parameters)).ToList();
        return (items, totalItems);
    }

    public async Task<List<NdcLocation>> GetAllAsync(string? searchString = null)
    {
        using var connection = _databaseService.CreateConnection();
        var whereClause = BuildSearchWhereClause(searchString);
        var parameters = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(searchString))
        {
            parameters.Add("@Search", $"%{searchString.Trim()}%");
        }

        var sql = $@"
SELECT *
FROM RCS_Locations
{whereClause}
ORDER BY [Group],
         CASE WHEN ISNULL(LaneCode, '') = '' THEN 1 ELSE 0 END,
         LaneCode,
         CASE WHEN ISNULL(DepthIndex, 0) <= 0 THEN 2147483647 ELSE DepthIndex END,
         NodeRemark";

        return (await connection.QueryAsync<NdcLocation>(sql, parameters)).ToList();
    }

    public async Task<List<string>> GetGroupsAsync()
    {
        using var connection = _databaseService.CreateConnection();
        const string sql = @"
SELECT DISTINCT [Group]
FROM RCS_Locations
WHERE [Group] IS NOT NULL
  AND LTRIM(RTRIM([Group])) <> ''
ORDER BY [Group];";

        return (await connection.QueryAsync<string>(sql)).ToList();
    }

    public async Task<NdcLocation?> GetByIdAsync(int id)
    {
        using var connection = _databaseService.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<NdcLocation>(
            "SELECT * FROM RCS_Locations WHERE Id = @Id",
            new { Id = id });
    }

    public async Task<NdcLocation?> GetByNodeRemarkAsync(string nodeRemark)
    {
        using var connection = _databaseService.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<NdcLocation>(
            "SELECT * FROM RCS_Locations WHERE NodeRemark = @NodeRemark",
            new { NodeRemark = nodeRemark });
    }

    public async Task<bool> ExistsNodeRemarkAsync(string nodeRemark, int? excludeId = null)
    {
        using var connection = _databaseService.CreateConnection();
        var sql = @"
SELECT COUNT(*)
FROM RCS_Locations
WHERE NodeRemark = @NodeRemark
  AND (@ExcludeId IS NULL OR Id <> @ExcludeId)";

        var count = await connection.ExecuteScalarAsync<int>(
            sql,
            new { NodeRemark = nodeRemark, ExcludeId = excludeId });

        return count > 0;
    }

    public async Task<int> InsertAsync(NdcLocation location)
    {
        using var connection = _databaseService.CreateConnection();
        const string sql = @"
INSERT INTO RCS_Locations
(
    Name,
    NodeRemark,
    MaterialCode,
    PalletID,
    Weight,
    Quanitity,
    EntryDate,
    [Group],
    LiftingHeight,
    Lock,
    Enabled,
    WattingNode,
    UnloadHeight,
    LaneCode,
    DepthIndex
)
VALUES
(
    @Name,
    @NodeRemark,
    @MaterialCode,
    @PalletID,
    @Weight,
    @Quanitity,
    @EntryDate,
    @Group,
    @LiftingHeight,
    @Lock,
    @Enabled,
    @WattingNode,
    @UnloadHeight,
    @LaneCode,
    @DepthIndex
);
SELECT CAST(SCOPE_IDENTITY() AS int);";

        return await connection.ExecuteScalarAsync<int>(
            sql,
            new
            {
                location.Name,
                location.NodeRemark,
                location.MaterialCode,
                location.PalletID,
                location.Weight,
                location.Quanitity,
                location.EntryDate,
                location.Group,
                location.LiftingHeight,
                location.Lock,
                location.Enabled,
                location.WattingNode,
                location.UnloadHeight,
                location.LaneCode,
                location.DepthIndex
            });
    }

    public async Task UpdateAsync(NdcLocation location)
    {
        using var connection = _databaseService.CreateConnection();
        const string sql = @"
UPDATE RCS_Locations
SET Name = @Name,
    NodeRemark = @NodeRemark,
    MaterialCode = @MaterialCode,
    PalletID = @PalletID,
    Weight = @Weight,
    Quanitity = @Quanitity,
    EntryDate = @EntryDate,
    [Group] = @Group,
    LiftingHeight = @LiftingHeight,
    Lock = @Lock,
    Enabled = @Enabled,
    WattingNode = @WattingNode,
    UnloadHeight = @UnloadHeight,
    LaneCode = @LaneCode,
    DepthIndex = @DepthIndex
WHERE Id = @Id";

        await connection.ExecuteAsync(sql, location);
    }

    public async Task DeleteAsync(int id)
    {
        using var connection = _databaseService.CreateConnection();
        await connection.ExecuteAsync(
            "DELETE FROM RCS_Locations WHERE Id = @Id",
            new { Id = id });
    }

    public async Task ClearMaterialAsync(int id)
    {
        using var connection = _databaseService.CreateConnection();
        await connection.ExecuteAsync(@"
UPDATE RCS_Locations
SET MaterialCode = NULL,
    PalletID = '0',
    Weight = '0',
    Quanitity = '0',
    EntryDate = NULL
WHERE Id = @Id",
            new { Id = id });
    }

    public async Task SetLockStateAsync(int id, bool lockState)
    {
        using var connection = _databaseService.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE RCS_Locations SET Lock = @LockState WHERE Id = @Id",
            new { Id = id, LockState = lockState });
    }

    public async Task SetEnabledStateAsync(int id, bool enabledState)
    {
        using var connection = _databaseService.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE RCS_Locations SET Enabled = @EnabledState WHERE Id = @Id",
            new { Id = id, EnabledState = enabledState });
    }

    public async Task<int> BatchClearMaterialsByIdsAsync(List<int> locationIds)
    {
        using var connection = _databaseService.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var affectedCount = await connection.ExecuteAsync(@"
UPDATE RCS_Locations
SET MaterialCode = NULL,
    PalletID = '0',
    Weight = '0',
    Quanitity = '0',
    EntryDate = NULL
WHERE Id IN @LocationIds",
            new { LocationIds = locationIds },
            transaction);

        transaction.Commit();
        return affectedCount;
    }

    public async Task<int> BatchToggleLockByIdsAsync(List<int> locationIds, bool lockState)
    {
        using var connection = _databaseService.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var affectedCount = await connection.ExecuteAsync(@"
UPDATE RCS_Locations
SET Lock = @LockState
WHERE Id IN @LocationIds
  AND Lock <> @LockState",
            new { LocationIds = locationIds, LockState = lockState ? 1 : 0 },
            transaction);

        transaction.Commit();
        return affectedCount;
    }

    public async Task<int> BatchClearMaterialsByGroupAsync(string group)
    {
        using var connection = _databaseService.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var affectedCount = await connection.ExecuteAsync(@"
UPDATE RCS_Locations
SET MaterialCode = NULL,
    PalletID = '0',
    Weight = '0',
    Quanitity = '0',
    EntryDate = NULL
WHERE [Group] = @Group",
            new { Group = group },
            transaction);

        transaction.Commit();
        return affectedCount;
    }

    public async Task<int> BatchToggleLockByGroupAsync(string group, bool lockState)
    {
        using var connection = _databaseService.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var affectedCount = await connection.ExecuteAsync(@"
UPDATE RCS_Locations
SET Lock = @LockState
WHERE [Group] = @Group
  AND Lock <> @LockState",
            new { Group = group, LockState = lockState ? 1 : 0 },
            transaction);

        transaction.Commit();
        return affectedCount;
    }

    private static string BuildSearchWhereClause(string? searchString)
    {
        return string.IsNullOrWhiteSpace(searchString)
            ? string.Empty
            : "WHERE NodeRemark LIKE @Search OR MaterialCode LIKE @Search OR Name LIKE @Search OR LaneCode LIKE @Search";
    }

    private static DynamicParameters BuildSearchParameters(string? searchString, int page, int pageSize)
    {
        var parameters = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(searchString))
        {
            parameters.Add("@Search", $"%{searchString.Trim()}%");
        }

        parameters.Add("@Offset", (page - 1) * pageSize);
        parameters.Add("@PageSize", pageSize);
        return parameters;
    }
}
