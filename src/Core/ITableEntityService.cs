using System.Linq.Expressions;

namespace SimpleAzureTableStorage.Core;

public interface ITableEntityService
{
    Task CommitChanges(bool failOnFirstException, CancellationToken token = default);
    Task Delete(string id, CancellationToken token = default);
}

public interface ITableEntityService<T> 
    where T : class
{
    Task Delete(T entity, CancellationToken token = default);
    Task<T?> Load(string id, CancellationToken token = default);
    Task<T?> Load<TKeyValue>(Expression<Func<T, TKeyValue>> keyProp, TKeyValue value, CancellationToken token = default);
    Task<T?> Load<TKeyValue, TPartitionValue>(Expression<Func<T, TKeyValue>> keyProp, TKeyValue keyValue, Expression<Func<T, TPartitionValue>> partitionProp, TPartitionValue partitionValue, CancellationToken token = default);
    void Store(T entity, CancellationToken token = default);

}