using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using Dapper;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models.Enums;
using WarehouseManagementSystem.Models.Ndc;

namespace WarehouseManagementSystem.Services.Ndc;

#region 抽象接口定义

/// <summary>
/// ACI 任务仓储交互服务（处理底层搬运业务和源用户派发单追踪的相关读写查基础）
/// </summary>
public interface IAciTaskDataService
{
    Task<NdcTaskMove?> FindNdcTaskByNdcTaskIdAsync(int ndcTaskId);
    Task<NdcTaskMove?> FindNdcTaskByNdcTaskIdAndStatusAsync(int ndcTaskId, TaskStatuEnum status);
    Task<NdcTaskMove?> FindNdcTaskByNdcTaskIdAndStatusNotAsync(int ndcTaskId, TaskStatuEnum status);
    Task<List<NdcTaskMove>> GetNdcTasksByNdcTaskIdAsync(int ndcTaskId);
    Task<List<NdcTaskMove>> GetNdcTasksByStatusRangeAsync(TaskStatuEnum minStatusInclusive, TaskStatuEnum maxStatusExclusive);
    Task UpdateNdcTaskAsync(NdcTaskMove entity);
    Task<NdcUserTask?> GetUserTaskByRequestCodeAsync(string requestCode);
    Task<List<NdcUserTask>> GetUserTasksByGroupNoAsync(string groupNo);
}

/// <summary>
/// 上线点/卸货点物理交互点（Location/Roller配置参数读取）访问支持服务
/// </summary>
public interface IAciLocationDataService
{
    Task<NdcLocation?> GetLocationByNodeRemarkAsync(string nodeRemark);
}

/// <summary>
/// 事件和报警消息互动的操作流水管理持久层
/// </summary>
public interface IAciInteractionDataService
{
    Task<NdcWmsInteraction?> GetWmsInteractionByRequestCodeAsync(string requestCode);
    Task InsertWmsInteractionAsync(NdcWmsInteraction entity);
}

#endregion

#region 基础底层 Dapper 实现类及包装

/// <summary>
/// Dapper 持久层操作的封装实现基类（提供所有仓储共用通用的查询、更新和新增支持以防止长文件冗余结构）
/// </summary>
public abstract class AciDapperDataServiceBase
{
    private readonly IDatabaseService _db;

    protected AciDapperDataServiceBase(IDatabaseService db)
    {
        _db = db;
    }

    protected IDbConnection CreateConnection()
    {
        return _db.CreateConnection();
    }

    protected async Task InsertEntityAsync<T>(string tableName, string keyColumnName, T entity)
        where T : class
    {
        using var connection = _db.CreateConnection();

        var properties = entity.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Where(p => p.GetCustomAttribute<NotMappedAttribute>() is null)
            .Where(p => !string.Equals(GetColumnName(p, keyColumnName), keyColumnName, StringComparison.OrdinalIgnoreCase)
                        || !IsDefaultValue(p.GetValue(entity), p.PropertyType))
            .ToList();

        var columns = string.Join(", ", properties.Select(p => $"[{GetColumnName(p, keyColumnName)}]"));
        var parameters = string.Join(", ", properties.Select(p => $"@{p.Name}"));
        var sql = $"INSERT INTO [{tableName}] ({columns}) VALUES ({parameters})";

        await connection.ExecuteAsync(sql, entity);
    }

    private static string GetColumnName(PropertyInfo property, string keyColumnName)
    {
        if (string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(property.Name, "ID", StringComparison.OrdinalIgnoreCase))
        {
            return keyColumnName;
        }

        return property.Name;
    }

    private static bool IsDefaultValue(object? value, Type type)
    {
        if (value is null)
        {
            return true;
        }

        if (type == typeof(Guid))
        {
            return (Guid)value == Guid.Empty;
        }

        if (type.IsValueType)
        {
            return value.Equals(Activator.CreateInstance(type));
        }

        return false;
    }
}

#endregion

#region 具体接口实现部分

/// <summary>
/// 提供 ACI 等级下的作业执行任务信息以及原始上游端用户的相关基础维护写入。
/// </summary>
public sealed class AciTaskDataService : AciDapperDataServiceBase, IAciTaskDataService
{
    public AciTaskDataService(IDatabaseService db) : base(db)
    {
    }

    public async Task<NdcTaskMove?> FindNdcTaskByNdcTaskIdAsync(int ndcTaskId)
    {
        const string sql = "SELECT TOP 1 * FROM [NdcTask_Moves] WHERE [NdcTaskId] = @NdcTaskId";
        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<NdcTaskMove>(sql, new { NdcTaskId = ndcTaskId });
    }

    public async Task<NdcTaskMove?> FindNdcTaskByNdcTaskIdAndStatusAsync(int ndcTaskId, TaskStatuEnum status)
    {
        const string sql = "SELECT TOP 1 * FROM [NdcTask_Moves] WHERE [NdcTaskId] = @NdcTaskId AND [TaskStatus] = @TaskStatus";
        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<NdcTaskMove>(sql, new { NdcTaskId = ndcTaskId, TaskStatus = status });
    }

    public async Task<NdcTaskMove?> FindNdcTaskByNdcTaskIdAndStatusNotAsync(int ndcTaskId, TaskStatuEnum status)
    {
        const string sql = "SELECT TOP 1 * FROM [NdcTask_Moves] WHERE [NdcTaskId] = @NdcTaskId AND [TaskStatus] <> @TaskStatus";
        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<NdcTaskMove>(sql, new { NdcTaskId = ndcTaskId, TaskStatus = status });
    }

    public async Task<List<NdcTaskMove>> GetNdcTasksByNdcTaskIdAsync(int ndcTaskId)
    {
        const string sql = "SELECT * FROM [NdcTask_Moves] WHERE [NdcTaskId] = @NdcTaskId";
        using var connection = CreateConnection();
        var items = await connection.QueryAsync<NdcTaskMove>(sql, new { NdcTaskId = ndcTaskId });
        return items.ToList();
    }

    public async Task<List<NdcTaskMove>> GetNdcTasksByStatusRangeAsync(TaskStatuEnum minStatusInclusive, TaskStatuEnum maxStatusExclusive)
    {
        const string sql = "SELECT * FROM [NdcTask_Moves] WHERE [TaskStatus] >= @MinStatus AND [TaskStatus] < @MaxStatus";
        using var connection = CreateConnection();
        var items = await connection.QueryAsync<NdcTaskMove>(sql, new { MinStatus = minStatusInclusive, MaxStatus = maxStatusExclusive });
        return items.ToList();
    }

    public Task UpdateNdcTaskAsync(NdcTaskMove entity)
    {
        return UpdateTaskMoveAsync(entity);
    }

    public async Task<NdcUserTask?> GetUserTaskByRequestCodeAsync(string requestCode)
    {
        const string sql = "SELECT TOP 1 * FROM [RCS_UserTasks] WHERE [requestCode] = @RequestCode";
        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<NdcUserTask>(sql, new { RequestCode = requestCode });
    }

    public async Task<List<NdcUserTask>> GetUserTasksByGroupNoAsync(string groupNo)
    {
        const string sql = "SELECT * FROM [RCS_UserTasks] WHERE [taskGroupNo] = @TaskGroupNo";
        using var connection = CreateConnection();
        var items = await connection.QueryAsync<NdcUserTask>(sql, new { TaskGroupNo = groupNo });
        return items.ToList();
    }

    /// <summary>
    /// 对 NDC 下发任务表的属性完全展开刷新更新 (由于实体结构较大，在此直接全量重写 SQL 方式完成)
    /// </summary>
    private async Task UpdateTaskMoveAsync(NdcTaskMove entity)
    {
        const string sql = @"
UPDATE [NdcTask_Moves]
SET
    [NdcTaskId] = @NdcTaskId,
    [SchedulTaskNo] = @SchedulTaskNo,
    [TaskType] = @TaskType,
    [Group] = @Group,
    [PickupSite] = @PickupSite,
    [PickupHeight] = @PickupHeight,
    [PickUpDepth] = @PickUpDepth,
    [UnloadSite] = @UnloadSite,
    [UnloadHeight] = @UnloadHeight,
    [UnloadDepth] = @UnloadDepth,
    [TaskStatus] = @TaskStatus,
    [AgvId] = @AgvId,
    [Priority] = @Priority,
    [Remark] = @Remark,
    [CancelTask] = @CancelTask,
    [OrderIndex] = @OrderIndex,
    [CreationTime] = @CreationTime,
    [CloseTime] = @CloseTime
WHERE [Id] = @Id";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, entity);
    }
}

/// <summary>
/// ACI 的物理交互节点、站台或滚筒线状态服务查询实现
/// </summary>
public sealed class AciLocationDataService : AciDapperDataServiceBase, IAciLocationDataService
{
    public AciLocationDataService(IDatabaseService db) : base(db)
    {
    }

    public async Task<NdcLocation?> GetLocationByNodeRemarkAsync(string nodeRemark)
    {
        const string sql = "SELECT TOP 1 * FROM [RCS_Locations] WHERE [NodeRemark] = @NodeRemark";
        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<NdcLocation>(sql, new { NodeRemark = nodeRemark });
    }
}

/// <summary>
/// 操作报警和流水消息的插入交互读取记录实现
/// </summary>
public sealed class AciInteractionDataService : AciDapperDataServiceBase, IAciInteractionDataService
{
    public AciInteractionDataService(IDatabaseService db) : base(db)
    {
    }

    public async Task<NdcWmsInteraction?> GetWmsInteractionByRequestCodeAsync(string requestCode)
    {
        const string sql = "SELECT TOP 1 * FROM [RCS_WmsInteraction] WHERE [RequestCode] = @RequestCode";
        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<NdcWmsInteraction>(sql, new { RequestCode = requestCode });
    }

    public Task InsertWmsInteractionAsync(NdcWmsInteraction entity)
    {
        return InsertEntityAsync("RCS_WmsInteraction", "ID", entity);
    }
}

#endregion
