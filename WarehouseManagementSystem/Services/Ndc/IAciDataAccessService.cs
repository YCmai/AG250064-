using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using Dapper;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models.Ndc;

namespace WarehouseManagementSystem.Services.Ndc;

#region 抽象接口定义

/// <summary>
/// ACI 任务仓储交互服务（处理底层搬运业务和源用户派发单追踪的相关读写查基础）
/// </summary>
public interface IAciTaskDataService
{
    Task<NdcTaskMove?> FindNdcTaskAsync(Func<NdcTaskMove, bool> predicate);
    Task<List<NdcTaskMove>> GetNdcTasksAsync(Func<NdcTaskMove, bool> predicate);
    Task UpdateNdcTaskAsync(NdcTaskMove entity);
    Task<NdcUserTask?> FindUserTaskAsync(Func<NdcUserTask, bool> predicate);
    Task<List<NdcUserTask>> GetUserTasksAsync(Func<NdcUserTask, bool> predicate);
    Task<NdcUserTask?> GetUserTaskByRequestCodeAsync(string requestCode);
}

/// <summary>
/// 上线点/卸货点物理交互点（Location/Roller配置参数读取）访问支持服务
/// </summary>
public interface IAciLocationDataService
{
    Task<NdcLocation?> FindLocationAsync(Func<NdcLocation, bool> predicate);
}

/// <summary>
/// 事件和报警消息互动的操作流水管理持久层
/// </summary>
public interface IAciInteractionDataService
{
    Task<NdcWmsInteraction?> FindWmsInteractionAsync(Func<NdcWmsInteraction, bool> predicate);
    Task InsertWmsInteractionAsync(NdcWmsInteraction entity);
}

/// <summary>
/// 支持接驳线 PLC 控制层消息数据交互管理的仓储支持协议
/// </summary>

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

    protected async Task<T?> FindFirstAsync<T>(string tableName, Func<T, bool> predicate)
        where T : class, new()
    {
        var items = await LoadAllAsync<T>(tableName);
        return items.FirstOrDefault(predicate);
    }

    protected async Task<List<T>> FilterAsync<T>(string tableName, Func<T, bool> predicate)
        where T : class, new()
    {
        var items = await LoadAllAsync<T>(tableName);
        return items.Where(predicate).ToList();
    }

    protected async Task<List<T>> LoadAllAsync<T>(string tableName)
        where T : class, new()
    {
        using var connection = _db.CreateConnection();
        var items = await connection.QueryAsync<T>($"SELECT * FROM [{tableName}]");
        return items.ToList();
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

    protected async Task UpdateEntityAsync<T>(string tableName, string keyColumnName, T entity)
        where T : class
    {
        using var connection = _db.CreateConnection();

        var properties = entity.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Where(p => p.GetCustomAttribute<NotMappedAttribute>() is null)
            .ToList();

        var keyProperty = properties.First(p =>
            string.Equals(GetColumnName(p, keyColumnName), keyColumnName, StringComparison.OrdinalIgnoreCase));

        var setters = string.Join(", ", properties
            .Where(p => p != keyProperty)
            .Select(p => $"[{GetColumnName(p, keyColumnName)}] = @{p.Name}"));

        var sql = $"UPDATE [{tableName}] SET {setters} WHERE [{keyColumnName}] = @{keyProperty.Name}";
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

    public Task<NdcTaskMove?> FindNdcTaskAsync(Func<NdcTaskMove, bool> predicate)
    {
        return FindFirstAsync("NdcTask_Moves", predicate);
    }

    public Task<List<NdcTaskMove>> GetNdcTasksAsync(Func<NdcTaskMove, bool> predicate)
    {
        return FilterAsync("NdcTask_Moves", predicate);
    }

    public Task UpdateNdcTaskAsync(NdcTaskMove entity)
    {
        return UpdateTaskMoveAsync(entity);
    }

    public Task<NdcUserTask?> FindUserTaskAsync(Func<NdcUserTask, bool> predicate)
    {
        return FindFirstAsync("RCS_UserTasks", predicate);
    }

    public Task<List<NdcUserTask>> GetUserTasksAsync(Func<NdcUserTask, bool> predicate)
    {
        return FilterAsync("RCS_UserTasks", predicate);
    }

    public Task<NdcUserTask?> GetUserTaskByRequestCodeAsync(string requestCode)
    {
        return FindUserTaskAsync(x => x.requestCode == requestCode);
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

    public Task<NdcLocation?> FindLocationAsync(Func<NdcLocation, bool> predicate)
    {
        return FindFirstAsync("RCS_Locations", predicate);
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

    public Task<NdcWmsInteraction?> FindWmsInteractionAsync(Func<NdcWmsInteraction, bool> predicate)
    {
        return FindFirstAsync("RCS_WmsInteraction", predicate);
    }

    public Task InsertWmsInteractionAsync(NdcWmsInteraction entity)
    {
        return InsertEntityAsync("RCS_WmsInteraction", "ID", entity);
    }
}
#endregion

