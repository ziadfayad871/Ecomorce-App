using System.Linq.Expressions;

namespace Core.Application.Common.Persistence;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id);
    Task<List<T>> GetAllAsync();
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);

    ValueTask AddAsync(T entity);
    bool Update(T entity);
    bool Remove(T entity);

    Task<int> SaveChangesAsync();
}
