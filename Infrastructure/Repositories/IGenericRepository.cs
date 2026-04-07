namespace WorkManagementSystem.Infrastructure.Repositories
{
    public interface IGenericRepository<T>
    {
        IQueryable<T> Query();
        Task<T> GetByIdAsync(Guid id);
        Task AddAsync(T entity);
        void Update(T entity);
        void Delete(T entity);
        Task SaveAsync();
    }
}
