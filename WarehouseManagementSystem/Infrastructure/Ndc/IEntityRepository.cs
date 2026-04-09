using System.Linq.Expressions;

namespace WarehouseManagementSystem.Infrastructure.Ndc;

public interface IEntityRepository<TEntity>
    where TEntity : class
{
    Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate);

    Task<List<TEntity>> GetListAsync();

    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate);

    Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> predicate);

    Task InsertAsync(TEntity entity);

    Task UpdateAsync(TEntity entity, bool autoSave = true);

    Task UpdateManyAsync(IEnumerable<TEntity> entities, bool autoSave = true);
}

