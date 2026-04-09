using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using Dapper;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models.Ndc;

namespace WarehouseManagementSystem.Services.Rcs;

/// <summary>
/// Provides access to RCS user tasks.
/// </summary>
public interface IRcsUserTaskService
{
    Task<List<NdcUserTask>> GetTasksAsync(Func<NdcUserTask, bool> predicate);
    Task UpdateAsync(NdcUserTask entity);
}

/// <summary>
/// Provides access to generated NDC tasks.
/// </summary>
public interface IRcsNdcTaskService
{
    Task<List<NdcTaskMove>> GetTasksAsync(Func<NdcTaskMove, bool> predicate);
    Task<NdcTaskMove?> FindAsync(Func<NdcTaskMove, bool> predicate);
    Task UpdateAsync(NdcTaskMove entity);
    Task InsertAsync(NdcTaskMove entity);
}

/// <summary>
/// Provides access to RCS locations.
/// </summary>
public interface IRcsLocationService
{
    Task<List<NdcLocation>> GetAllAsync();
    Task<NdcLocation?> FindAsync(Func<NdcLocation, bool> predicate);
    Task UpdateAsync(NdcLocation entity);
}

/// <summary>
/// Provides access to WMS interaction records created by RCS flows.
/// </summary>
public interface IRcsInteractionService
{
    Task<NdcWmsInteraction?> FindAsync(Func<NdcWmsInteraction, bool> predicate);
    Task InsertAsync(NdcWmsInteraction entity);
}

public abstract class RcsDapperDataServiceBase
{
    private readonly IDatabaseService _db;

    protected RcsDapperDataServiceBase(IDatabaseService db)
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

/// <summary>
/// Dapper implementation for RCS user task reads and writes.
/// </summary>
public sealed class RcsUserTaskService : RcsDapperDataServiceBase, IRcsUserTaskService
{
    public RcsUserTaskService(IDatabaseService db) : base(db)
    {
    }

    public Task<List<NdcUserTask>> GetTasksAsync(Func<NdcUserTask, bool> predicate)
    {
        return FilterAsync("RCS_UserTasks", predicate);
    }

    public Task UpdateAsync(NdcUserTask entity)
    {
        return UpdateEntityAsync("RCS_UserTasks", "ID", entity);
    }
}

/// <summary>
/// Dapper implementation for NDC task reads and writes used by RCS.
/// </summary>
public sealed class RcsNdcTaskService : RcsDapperDataServiceBase, IRcsNdcTaskService
{
    public RcsNdcTaskService(IDatabaseService db) : base(db)
    {
    }

    public Task<List<NdcTaskMove>> GetTasksAsync(Func<NdcTaskMove, bool> predicate)
    {
        return FilterAsync("NdcTask_Moves", predicate);
    }

    public Task<NdcTaskMove?> FindAsync(Func<NdcTaskMove, bool> predicate)
    {
        return FindFirstAsync("NdcTask_Moves", predicate);
    }

    public Task UpdateAsync(NdcTaskMove entity)
    {
        return UpdateTaskMoveAsync(entity);
    }

    public Task InsertAsync(NdcTaskMove entity)
    {
        return InsertTaskMoveAsync(entity);
    }

    private async Task InsertTaskMoveAsync(NdcTaskMove entity)
    {
        const string sql = @"
INSERT INTO [NdcTask_Moves]
(
    [Id],
    [NdcTaskId],
    [SchedulTaskNo],
    [TaskType],
    [Group],
    [PickupSite],
    [PickupHeight],
    [PickUpDepth],
    [UnloadSite],
    [UnloadHeight],
    [UnloadDepth],
    [TaskStatus],
    [AgvId],
    [Priority],
    [Remark],
    [CancelTask],
    [OrderIndex],
    [CreationTime],
    [CloseTime]
)
VALUES
(
    @Id,
    @NdcTaskId,
    @SchedulTaskNo,
    @TaskType,
    @Group,
    @PickupSite,
    @PickupHeight,
    @PickUpDepth,
    @UnloadSite,
    @UnloadHeight,
    @UnloadDepth,
    @TaskStatus,
    @AgvId,
    @Priority,
    @Remark,
    @CancelTask,
    @OrderIndex,
    @CreationTime,
    @CloseTime
)";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, entity);
    }

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
/// Dapper implementation for RCS location reads and writes.
/// </summary>
public sealed class RcsLocationService : RcsDapperDataServiceBase, IRcsLocationService
{
    public RcsLocationService(IDatabaseService db) : base(db)
    {
    }

    public Task<List<NdcLocation>> GetAllAsync()
    {
        return LoadAllAsync<NdcLocation>("RCS_Locations");
    }

    public Task<NdcLocation?> FindAsync(Func<NdcLocation, bool> predicate)
    {
        return FindFirstAsync("RCS_Locations", predicate);
    }

    public Task UpdateAsync(NdcLocation entity)
    {
        return UpdateEntityAsync("RCS_Locations", "Id", entity);
    }
}

/// <summary>
/// Dapper implementation for RCS WMS interaction records.
/// </summary>
public sealed class RcsInteractionService : RcsDapperDataServiceBase, IRcsInteractionService
{
    public RcsInteractionService(IDatabaseService db) : base(db)
    {
    }

    public Task<NdcWmsInteraction?> FindAsync(Func<NdcWmsInteraction, bool> predicate)
    {
        return FindFirstAsync("RCS_WmsInteraction", predicate);
    }

    public Task InsertAsync(NdcWmsInteraction entity)
    {
        return InsertEntityAsync("RCS_WmsInteraction", "ID", entity);
    }
}
