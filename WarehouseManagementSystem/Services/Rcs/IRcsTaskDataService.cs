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
    Task<List<NdcUserTask>> GetCancelableTasksAsync();
    Task<List<NdcUserTask>> GetActiveTasksAsync();
    Task<List<NdcUserTask>> GetPendingTasksAsync();
    Task<bool> ExistsUnfinishedTasksInGroupAsync(string taskGroupNo);
    Task UpdateAsync(NdcUserTask entity);
}

/// <summary>
/// Provides access to generated NDC tasks.
/// </summary>
public interface IRcsNdcTaskService
{
    Task<List<NdcTaskMove>> GetByScheduleTaskNosAsync(IEnumerable<string> scheduleTaskNos);
    Task<List<NdcTaskMove>> GetUnfinishedTasksAsync();
    Task<NdcTaskMove?> FindByScheduleTaskNoAsync(string scheduleTaskNo);
    Task UpdateAsync(NdcTaskMove entity);
    Task InsertAsync(NdcTaskMove entity);
}

/// <summary>
/// Provides access to RCS locations.
/// </summary>
public interface IRcsLocationService
{
    Task<List<NdcLocation>> GetAllAsync();
    Task<List<NdcLocation>> GetByNodeRemarksAsync(IEnumerable<string> nodeRemarks);
    Task<NdcLocation?> FindByNodeRemarkAsync(string nodeRemark);
    Task UpdateAsync(NdcLocation entity);
}

/// <summary>
/// Provides access to WMS interaction records created by RCS flows.
/// </summary>
public interface IRcsInteractionService
{
    Task<NdcWmsInteraction?> FindByRequestCodeAsync(string requestCode);
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

    public async Task<List<NdcUserTask>> GetCancelableTasksAsync()
    {
        const string sql = @"
        SELECT *
        FROM [RCS_UserTasks]
        WHERE [taskStatus] < @TaskFinish
          AND [IsCancelled] = 1";

        using var connection = CreateConnection();
        var items = await connection.QueryAsync<NdcUserTask>(sql, new
        {
            TaskFinish = (int)Models.Enums.TaskStatuEnum.TaskFinish
        });
        return items.ToList();
    }

    public async Task<List<NdcUserTask>> GetActiveTasksAsync()
    {
        const string sql = @"
        SELECT *
        FROM [RCS_UserTasks]
        WHERE [taskStatus] <> @Canceled
          AND [taskStatus] <> @TaskFinish";

        using var connection = CreateConnection();
        var items = await connection.QueryAsync<NdcUserTask>(sql, new
        {
            Canceled = (int)Models.Enums.TaskStatuEnum.Canceled,
            TaskFinish = (int)Models.Enums.TaskStatuEnum.TaskFinish
        });
        return items.ToList();
    }

    public async Task<List<NdcUserTask>> GetPendingTasksAsync()
    {
        const string sql = @"
        SELECT *
        FROM [RCS_UserTasks]
        WHERE [taskStatus] = @None";

        using var connection = CreateConnection();
        var items = await connection.QueryAsync<NdcUserTask>(sql, new
        {
            None = (int)Models.Enums.TaskStatuEnum.None
        });
        return items.ToList();
    }

    public Task UpdateAsync(NdcUserTask entity)
    {
        return UpdateEntityAsync("RCS_UserTasks", "ID", entity);
    }

    public async Task<bool> ExistsUnfinishedTasksInGroupAsync(string taskGroupNo)
    {
        if (string.IsNullOrWhiteSpace(taskGroupNo))
        {
            return false;
        }

        const string sql = @"
        SELECT COUNT(1)
        FROM [RCS_UserTasks]
        WHERE [taskGroupNo] = @TaskGroupNo
          AND [taskStatus] NOT IN @TerminalStatuses;";

        using var connection = CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(sql, new
        {
            TaskGroupNo = taskGroupNo.Trim(),
            TerminalStatuses = new[]
            {
                (int)Models.Enums.TaskStatuEnum.TaskFinish,
                (int)Models.Enums.TaskStatuEnum.Canceled,
                (int)Models.Enums.TaskStatuEnum.CanceledWashFinish,
                (int)Models.Enums.TaskStatuEnum.OrderAgvFinish,
                (int)Models.Enums.TaskStatuEnum.InvalidUp,
                (int)Models.Enums.TaskStatuEnum.InvalidDown,
                (int)Models.Enums.TaskStatuEnum.RedirectRequest
            }
        });

        return count > 0;
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

    public async Task<List<NdcTaskMove>> GetByScheduleTaskNosAsync(IEnumerable<string> scheduleTaskNos)
    {
        var keys = scheduleTaskNos
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (keys.Length == 0)
        {
            return new List<NdcTaskMove>();
        }

        const string sql = @"
        SELECT *
        FROM [NdcTask_Moves]
        WHERE [SchedulTaskNo] IN @ScheduleTaskNos";

        using var connection = CreateConnection();
        var items = await connection.QueryAsync<NdcTaskMove>(sql, new { ScheduleTaskNos = keys });
        return items.ToList();
    }

    public async Task<List<NdcTaskMove>> GetUnfinishedTasksAsync()
    {
        const string sql = @"
        SELECT *
        FROM [NdcTask_Moves]
        WHERE [TaskStatus] <> @TaskFinish
          AND [TaskStatus] <> @Canceled";

        using var connection = CreateConnection();
        var items = await connection.QueryAsync<NdcTaskMove>(sql, new
        {
            TaskFinish = (int)Models.Enums.TaskStatuEnum.TaskFinish,
            Canceled = (int)Models.Enums.TaskStatuEnum.Canceled
        });
        return items.ToList();
    }

    public async Task<NdcTaskMove?> FindByScheduleTaskNoAsync(string scheduleTaskNo)
    {
        const string sql = @"
        SELECT TOP 1 *
        FROM [NdcTask_Moves]
        WHERE [SchedulTaskNo] = @ScheduleTaskNo";

        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<NdcTaskMove>(sql, new { ScheduleTaskNo = scheduleTaskNo });
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

    public async Task<List<NdcLocation>> GetByNodeRemarksAsync(IEnumerable<string> nodeRemarks)
    {
        var keys = nodeRemarks
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (keys.Length == 0)
        {
            return new List<NdcLocation>();
        }

        const string sql = @"
        SELECT *
        FROM [RCS_Locations]
        WHERE [NodeRemark] IN @NodeRemarks";

        using var connection = CreateConnection();
        var items = await connection.QueryAsync<NdcLocation>(sql, new { NodeRemarks = keys });
        return items.ToList();
    }

    public async Task<NdcLocation?> FindByNodeRemarkAsync(string nodeRemark)
    {
        const string sql = @"
        SELECT TOP 1 *
        FROM [RCS_Locations]
        WHERE [NodeRemark] = @NodeRemark";

        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<NdcLocation>(sql, new { NodeRemark = nodeRemark });
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

    public async Task<NdcWmsInteraction?> FindByRequestCodeAsync(string requestCode)
    {
        const string sql = @"
        SELECT TOP 1 *
        FROM [RCS_WmsInteraction]
        WHERE [RequestCode] = @RequestCode";

        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<NdcWmsInteraction>(sql, new { RequestCode = requestCode });
    }

    public Task InsertAsync(NdcWmsInteraction entity)
    {
        return InsertEntityAsync("RCS_WmsInteraction", "ID", entity);
    }
}
