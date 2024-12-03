using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TestX.Data.Common;
using TestX.Data.Contexts;
using TestX.Data.IRepositories;

namespace TestX.Data.Repositories;

/// <summary>
/// Generic repository implementation for handling CRUD operations.
/// </summary>
/// <typeparam name="TEntity"></typeparam>
public class Repository<TEntity> : IRepository<TEntity> where TEntity : Auditable
{
    private readonly DataBaseContext dbContext;
    private readonly DbSet<TEntity> dbSet;

    /// <summary>
    /// Initializes a new instance of the <see cref="Repository{TEntity}"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public Repository(DataBaseContext dbContext)
    {
        this.dbContext = dbContext;
        this.dbSet = dbContext.Set<TEntity>();
    }

    /// <summary>
    /// Saves any pending changes asynchronously.
    /// </summary>
    public async ValueTask SaveAsync()
    {
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Retrieves all entities asynchronously that match the given predicate.
    /// </summary>
    /// <param name="predicate">The lambda expression to filter entities.</param>
    /// <returns>A collection of filtered entities.</returns>
    public async Task<IEnumerable<TEntity>> SelectAllAsync(Expression<Func<TEntity, bool>> predicate = null)
    {
        if (predicate != null)
        {
            return await dbSet.Where(predicate).ToListAsync();
        }

        return await dbSet.ToListAsync(); // No filter, return all entities
    }


    /// <summary>
    /// Retrieves a single entity by its primary key asynchronously.
    /// </summary>
    /// <param name="id">The ID of the entity.</param>
    /// <returns>The entity if found, otherwise null.</returns>
    public async Task<TEntity> SelectByIdAsync(object id)
    {
        return await dbSet.FindAsync(id);
    }

    /// <summary>
    /// Inserts a new entity into the repository.
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    public async Task<TEntity> InsertAsync(TEntity entity)
    {
        await dbSet.AddAsync(entity);
        return entity;
        // Consider calling SaveAsync elsewhere for better control
    }

    /// <summary>
    /// Updates an existing entity in the repository.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    public async Task UpdateAsync(TEntity entity)
    {
        dbSet.Update(entity); // Simplified to directly update the entity
        await SaveAsync();
    }

    /// <summary>
    /// Deletes an entity by its ID.
    /// </summary>
    /// <param name="id">The ID of the entity to delete.</param>
    public async Task DeleteAsync(object id)
    {
        var entity = await SelectByIdAsync(id);
        if (entity != null)
        {
            await DeleteAsync(entity);
        }
        else
        {
            // Optionally handle the case where the entity is not found
            throw new KeyNotFoundException($"Entity with ID {id} not found.");
        }
    }

    /// <summary>
    /// Deletes an entity instance.
    /// </summary>
    /// <param name="entity">The entity instance to delete.</param>
    public async Task DeleteAsync(TEntity entity)
    {
        if (dbContext.Entry(entity).State == EntityState.Detached)
        {
            dbSet.Attach(entity);
        }
        dbSet.Remove(entity);
        await SaveAsync();
    }
}