namespace SimpleAzureTableStorage.Core;

public interface ITableEntityService
{
    Task CommitChanges(bool failOnFirstException, CancellationToken token = default);
}

public interface ITableEntityService<T> 
    where T : class
{
    Task Delete(T entity, CancellationToken token = default);
    Task Delete(string id, CancellationToken token = default);
    Task<T?> Load(string id, CancellationToken token = default);
    void Store(T entity, string id, CancellationToken token = default);

}