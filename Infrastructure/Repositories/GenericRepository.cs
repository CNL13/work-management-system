using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Infrastructure.Repositories;
using WorkManagementSystem.Infrastructure.Data;
public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    private readonly AppDbContext _context;

    public GenericRepository(AppDbContext context)
    {
        _context = context;
    }

    public IQueryable<T> Query() => _context.Set<T>();

    public async Task<T> GetByIdAsync(Guid id)
        => await _context.Set<T>().FindAsync(id);

    public async Task AddAsync(T entity)
        => await _context.Set<T>().AddAsync(entity);

    public void Update(T entity)
        => _context.Set<T>().Update(entity);

    public void Delete(T entity)
        => _context.Set<T>().Remove(entity);

    public async Task SaveAsync()
        => await _context.SaveChangesAsync();
}