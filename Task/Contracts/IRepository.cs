using System.Linq.Expressions;

namespace Task.Contracts
{
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(object id);
        Task<List<T>> GetAllAsync();
        Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);

        ValueTask AddAsync(T entity);
        void Update(T entity);
        void Remove(T entity);

        Task<int> SaveChangesAsync();
    }
}
