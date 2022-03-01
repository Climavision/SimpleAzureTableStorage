using System.Linq.Expressions;

namespace SimpleAzureTableStorage.Core.Services;

public interface IEntitySession
{
    Task Delete<T>(T entity, CancellationToken token = default) where T : class;
    Task Delete<T>(string id, CancellationToken token = default) where T : class;
    Task SaveChanges(CancellationToken token = default);

    void Store<T>(T entity, CancellationToken token = default) where T : class;
    
    Task<T?> Load<T>(string id, string? rowKeyValue = null, CancellationToken token = default) where T : class;
    Task<T?> Load<T, TKeyValue>(Expression<Func<T, TKeyValue>> keyProp, TKeyValue value, CancellationToken token = default) where T : class;
    IAsyncEnumerable<T> LoadAll<T, TPartitionValue>(Expression<Func<T, TPartitionValue>> partitionProp, TPartitionValue value, CancellationToken token = default) where T : class;
    Task<T?> Load<T, TKeyValue, TPartitionValue>(Expression<Func<T, TKeyValue>> prop, TKeyValue keyValue, Expression<Func<T, TPartitionValue>> partitionProp, TPartitionValue partitionValue, CancellationToken token = default) where T : class;
    Task<Dictionary<string, T?>> Load<T>(IEnumerable<string> ids, string? rowKeyValue = null, CancellationToken token = default) where T : class;
    IAsyncEnumerable<T> Query<T>(string query);
}

public interface IEntityStore
{
    IEntitySession OpenSession();
}