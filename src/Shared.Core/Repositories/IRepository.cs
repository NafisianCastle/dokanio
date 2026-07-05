using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;

namespace Shared.Core.Repositories;

/// <summary>
/// Base repository interface providing common CRUD operations for all entities
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Gets an entity by its unique identifier
    /// </summary>
    /// <param name="id">Entity identifier</param>
    /// <returns>Entity if found, null otherwise</returns>
    Task<T?> GetByIdAsync(Guid id);
    
    /// <summary>
    /// Gets all entities of type T
    /// </summary>
    /// <returns>Collection of all entities</returns>
    Task<IEnumerable<T>> GetAllAsync();
    
    /// <summary>
    /// Finds entities matching the specified predicate
    /// </summary>
    /// <param name="predicate">Search predicate</param>
    /// <returns>Collection of matching entities</returns>
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    
    /// <summary>
    /// Adds a new entity to the repository
    /// </summary>
    /// <param name="entity">Entity to add</param>
    Task AddAsync(T entity);
    
    /// <summary>
    /// Updates an existing entity in the repository
    /// </summary>
    /// <param name="entity">Entity to update</param>
    Task UpdateAsync(T entity);
    
    /// <summary>
    /// Deletes an entity by its identifier (soft delete)
    /// </summary>
    /// <param name="id">Entity identifier</param>
    Task DeleteAsync(Guid id);
    
    /// <summary>
    /// Saves all pending changes to the underlying storage
    /// </summary>
    /// <returns>Number of affected records</returns>
    Task<int> SaveChangesAsync();

    // ── Transaction support (Requirement 9.3) ─────────────────────────────────

    /// <summary>
    /// Begins a database transaction for multi-step operations that must succeed or fail atomically.
    /// The caller is responsible for committing or rolling back the transaction.
    /// </summary>
    /// <returns>An <see cref="IDbContextTransaction"/> that must be disposed after use.</returns>
    Task<IDbContextTransaction> BeginTransactionAsync();

    /// <summary>
    /// Executes <paramref name="operation"/> inside a database transaction.
    /// Automatically commits on success and rolls back on any exception.
    /// </summary>
    /// <typeparam name="TResult">Return type of the operation.</typeparam>
    /// <param name="operation">Async delegate to execute within the transaction.</param>
    /// <returns>The value returned by <paramref name="operation"/>.</returns>
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation);

    /// <summary>
    /// Executes <paramref name="operation"/> inside a database transaction (no return value).
    /// Automatically commits on success and rolls back on any exception.
    /// </summary>
    /// <param name="operation">Async delegate to execute within the transaction.</param>
    Task ExecuteInTransactionAsync(Func<Task> operation);
}