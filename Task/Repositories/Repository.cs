using DataAccess.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Task.Contracts;


namespace Task.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _db;
        protected readonly DbSet<T> _set;

        public Repository(ApplicationDbContext db)
        {
            _db = db;
            _set = db.Set<T>();
        }

        public async Task<T?> GetByIdAsync(object id) => await _set.FindAsync(id);
        public async Task<List<T>> GetAllAsync() => await _set.ToListAsync();
        public async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate) => await _set.Where(predicate).ToListAsync();

        public async ValueTask AddAsync(T entity) => await _set.AddAsync(entity);
        public void Update(T entity) => _set.Update(entity);
        public void Remove(T entity) => _set.Remove(entity);

        public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();
    }
}