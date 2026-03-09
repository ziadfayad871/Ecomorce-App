using Core.Application.Common.Persistence;
using DataAccess.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace DataAccess.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly ApplicationDbContext Db;
    protected readonly DbSet<T> Set;

    public Repository(ApplicationDbContext db)
    {
        Db = db;
        Set = db.Set<T>();
    }

    public async Task<T?> GetByIdAsync(object id) => await Set.FindAsync(id);

    public async Task<List<T>> GetAllAsync() => await Set.ToListAsync();

    public async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate) => await Set.Where(predicate).ToListAsync();

    public async ValueTask AddAsync(T entity) => await Set.AddAsync(entity);

    public void Update(T entity) => Set.Update(entity);

    public void Remove(T entity) => Set.Remove(entity);

    public Task<int> SaveChangesAsync() => Db.SaveChangesAsync();
}
