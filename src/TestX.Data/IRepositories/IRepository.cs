using System.Linq.Expressions;
using TestX.Data.Common;

namespace TestX.Data.IRepositories;


/// <summary>
/// 
/// </summary>
/// <typeparam name="TEntity"></typeparam>
public interface IRepository<TEntity> where TEntity : Auditable
{
    /// <summary>
    /// Saves any pending changes asynchronously.
    /// </summary>
    /// <returns></returns>
    ValueTask SaveAsync();

    /// <summary>
    /// Retrieves all entities asynchronously.
    /// </summary>
    /// <returns>A collection of all entities.</returns>
    public Task<IEnumerable<TEntity>> SelectAllAsync(Expression<Func<TEntity, bool>> predicate = null);

    /// <summary>
    /// Retrieves a single entity by its primary key asynchronously.
    /// </summary>
    /// <param name="id">The ID of the entity.</param>
    /// <returns>The entity if found, otherwise null.</returns>
    Task<TEntity> SelectByIdAsync(object id);

    /// <summary>
    /// Inserts a new entity into the repository.
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    /// <returns></returns>
    Task<TEntity> InsertAsync(TEntity entity);

    /// <summary>
    /// Updates an existing entity in the repository.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <returns></returns>
    Task UpdateAsync(TEntity entity);

    /// <summary>
    /// Deletes an entity by its ID.
    /// </summary>
    /// <param name="id">The ID of the entity to delete.</param>
    /// <returns></returns>
    Task DeleteAsync(object id);

    /// <summary>
    /// Deletes an entity instance.
    /// </summary>
    /// <param name="entity">The entity instance to delete.</param>
    /// <returns></returns>
    Task DeleteAsync(TEntity entity);
}
