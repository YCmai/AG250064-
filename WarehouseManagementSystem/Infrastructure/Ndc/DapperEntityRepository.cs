using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using Dapper;
using WarehouseManagementSystem.Db;
using WarehouseManagementSystem.Models.Ndc;
using WarehouseManagementSystem.Models.Rcs;

namespace WarehouseManagementSystem.Infrastructure.Ndc;

public class DapperEntityRepository<TEntity> : IEntityRepository<TEntity>
    where TEntity : class, new()
{
    private readonly IDatabaseService _databaseService;
    private readonly RepositoryMetadata _metadata;

    public DapperEntityRepository(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
        _metadata = RepositoryMetadata.For<TEntity>();
    }

    public async Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate)
    {
        var items = await GetListAsync();
        return items.Where(predicate.Compile()).ToList();
    }

    public async Task<List<TEntity>> GetListAsync()
    {
        using var connection = _databaseService.CreateConnection();
        var sql = $"SELECT * FROM [{_metadata.TableName}]";
        var items = await connection.QueryAsync<TEntity>(sql);
        return items.ToList();
    }

    public async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate)
    {
        var items = await GetListAsync(predicate);
        return items.FirstOrDefault();
    }

    public Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> predicate)
    {
        return FirstOrDefaultAsync(predicate);
    }

    public async Task InsertAsync(TEntity entity)
    {
        using var connection = _databaseService.CreateConnection();

        var properties = _metadata.Properties
            .Where(p => !p.IsKey || !IsDefaultValue(p.Property.GetValue(entity), p.Property.PropertyType))
            .ToList();

        var columns = string.Join(", ", properties.Select(p => $"[{p.ColumnName}]"));
        var parameters = string.Join(", ", properties.Select(p => $"@{p.Property.Name}"));
        var sql = $"INSERT INTO [{_metadata.TableName}] ({columns}) VALUES ({parameters})";

        await connection.ExecuteAsync(sql, entity);
    }

    public async Task UpdateAsync(TEntity entity, bool autoSave = true)
    {
        using var connection = _databaseService.CreateConnection();

        var setters = string.Join(", ",
            _metadata.Properties
                .Where(p => !p.IsKey)
                .Select(p => $"[{p.ColumnName}] = @{p.Property.Name}"));

        var sql =
            $"UPDATE [{_metadata.TableName}] SET {setters} WHERE [{_metadata.KeyColumnName}] = @{_metadata.KeyProperty.Name}";

        await connection.ExecuteAsync(sql, entity);
    }

    public async Task UpdateManyAsync(IEnumerable<TEntity> entities, bool autoSave = true)
    {
        using var connection = _databaseService.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var setters = string.Join(", ",
            _metadata.Properties
                .Where(p => !p.IsKey)
                .Select(p => $"[{p.ColumnName}] = @{p.Property.Name}"));

        var sql =
            $"UPDATE [{_metadata.TableName}] SET {setters} WHERE [{_metadata.KeyColumnName}] = @{_metadata.KeyProperty.Name}";

        foreach (var entity in entities)
        {
            await connection.ExecuteAsync(sql, entity, transaction);
        }

        transaction.Commit();
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

    private sealed class RepositoryMetadata
    {
        public string TableName { get; init; } = string.Empty;
        public PropertyInfo KeyProperty { get; init; } = null!;
        public string KeyColumnName { get; init; } = string.Empty;
        public List<PropertyMetadata> Properties { get; init; } = new();

        public static RepositoryMetadata For<T>()
        {
            return For(typeof(T));
        }

        private static RepositoryMetadata For(Type entityType)
        {
            var tableName = entityType switch
            {
                var t when t == typeof(NdcTaskMove) => "NdcTask_Moves",
                var t when t == typeof(NdcUserTask) => "RCS_UserTasks",
                var t when t == typeof(NdcLocation) => "RCS_Locations",
                var t when t == typeof(NdcWmsInteraction) => "RCS_WmsInteraction",
                var t when t == typeof(CoupEventLog) => "CoupEventLog",
                var t when t == typeof(NdcApiTask) => "RCS_ApiTasks",
                var t when t == typeof(NdcIoAgvTask) => "RCS_IOAGV_Tasks",
                var t when t == typeof(NdcWmsTask) => "RCS_WmsTask",
                var t when t == typeof(EventLog) => "EventLog",
                _ => throw new NotSupportedException($"Unsupported repository entity: {entityType.FullName}")
            };

            var keyProperty = entityType.GetProperty("Id") ?? entityType.GetProperty("ID")
                ?? throw new InvalidOperationException($"No key property found for {entityType.FullName}");

            var keyColumnName = entityType switch
            {
                var t when t == typeof(NdcUserTask) => "ID",
                var t when t == typeof(NdcWmsInteraction) => "ID",
                var t when t == typeof(NdcApiTask) => "ID",
                var t when t == typeof(NdcWmsTask) => "ID",
                _ => keyProperty.Name
            };

            var properties = entityType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .Where(p => p.GetCustomAttribute<NotMappedAttribute>() is null)
                .Select(p => new PropertyMetadata
                {
                    Property = p,
                    ColumnName = p == keyProperty ? keyColumnName : (p.GetCustomAttribute<ColumnAttribute>()?.Name ?? p.Name),
                    IsKey = p == keyProperty
                })
                .ToList();

            return new RepositoryMetadata
            {
                TableName = tableName,
                KeyProperty = keyProperty,
                KeyColumnName = keyColumnName,
                Properties = properties
            };
        }
    }

    private sealed class PropertyMetadata
    {
        public PropertyInfo Property { get; init; } = null!;
        public string ColumnName { get; init; } = string.Empty;
        public bool IsKey { get; init; }
    }
}
