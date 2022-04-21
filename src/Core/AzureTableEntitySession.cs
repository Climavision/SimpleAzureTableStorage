using System.Linq.Expressions;
using Azure.Data.Tables;
using SimpleAzureTableStorage.Core.Services;

namespace SimpleAzureTableStorage.Core;

public class AzureTableEntitySession : IEntitySession
{
    private readonly Dictionary<Type, ITableEntityService> _entityServices = new();
    private readonly AzureTableEntityStore _store;

    public AzureTableEntitySession(AzureTableEntityStore store) => _store = store;

    public Task Delete<T>(T entity, CancellationToken token = default) where T : class =>
        GetEntityService<T>().Delete(entity, token);

    public Task Delete<T>(string id, CancellationToken token = default) where T : class =>
        GetEntityService<T>().Delete(id, token);

    public async IAsyncEnumerable<T> Query<T>(string query)
    {
        var details = _store.GetReflectionDetail<T>();
        var client = await _store.GetTableClient<T>();
        var queryResults = client.Query<TableEntity>(query).OrderByDescending(x => x.Timestamp);

        foreach (var result in queryResults)
            yield return details.LoadFrom(result);
    }

    public async Task SaveChanges(CancellationToken token = default)
    {
        var entityServices = _entityServices.Values.ToList();

        foreach (var service in entityServices)
            await service.CommitChanges(false, token).ConfigureAwait(false);
    }

    public void Store<T>(T entity, CancellationToken token = default) where T : class =>
        GetEntityService<T>().Store(entity, token);

    public async Task<T?> Load<T>(string id, string? rowKeyValue = null, CancellationToken token = default) where T : class =>
        await GetEntityService<T>().Load(id, token);

    public async Task<T?> Load<T, TKeyValue, TPartitionValue>(Expression<Func<T, TKeyValue>> keyProp, TKeyValue keyValue, Expression<Func<T, TPartitionValue>> partitionProp, TPartitionValue partitionValue, CancellationToken token = default) where T : class 
        => await GetEntityService<T>().Load(keyProp, keyValue, partitionProp, partitionValue, token);

    public async Task<Dictionary<string, T?>> Load<T>(IEnumerable<string> ids, string? rowKeyValue = null, CancellationToken token = default) where T : class
    {
        var entities = new Dictionary<string, T?>();

        foreach (var id in ids.Distinct())
            entities.Add(id, await Load<T>(id, rowKeyValue, token));

        return entities;
    }

    public async Task<T?> Load<T, TKeyValue>(Expression<Func<T, TKeyValue>> keyProp, TKeyValue value, CancellationToken token = default) where T : class => 
        await GetEntityService<T>().Load(keyProp, value, token);

    public IAsyncEnumerable<T> LoadAll<T, TPartitionValue>(Expression<Func<T, TPartitionValue>> partitionProp, TPartitionValue value, CancellationToken token = default) where T : class
    {
        var expression = new PropertyKeyStrategy<T, TPartitionValue>(partitionProp, false);

        return Query<T>($"PartitionKey eq '{expression.BuildKey(value)}'").Distinct();
    }

    private TableEntityService<T> GetEntityService<T>() where T : class
    {
        var type = typeof(T);

        if (_entityServices.ContainsKey(type)) return (TableEntityService<T>) _entityServices[type];

        var newService = new TableEntityService<T>(_store);

        _entityServices.Add(type, newService);

        return newService;
    }
}