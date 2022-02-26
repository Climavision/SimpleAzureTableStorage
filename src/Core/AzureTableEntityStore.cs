using System.Collections.Concurrent;
using Azure.Data.Tables;
using SimpleAzureTableStorage.Core.Services;

namespace SimpleAzureTableStorage.Core;

public class AzureTableEntityStore : IEntityStore
{
    private readonly TableServiceClient _client;
    private readonly string _connectionString;
    private readonly ConcurrentDictionary<Type, IReflectionDetail> _reflectionCache = new();
    private readonly string _schemaName;
    private readonly ConcurrentDictionary<Type, TableClient> _tableClients = new();

    public AzureTableEntityStore(string connectionString, string schemaName = "")
    {
        _client = new TableServiceClient(connectionString);
        _connectionString = connectionString;
        _schemaName = schemaName;
    }

    public IEntitySession OpenSession() =>
        new AzureTableEntitySession(this);

    public ReflectionDetail<T> GetReflectionDetail<T>()
    {
        var type = typeof(T);

        if (_reflectionCache.ContainsKey(type))
            return (ReflectionDetail<T>) _reflectionCache[type];

        var detail = new ReflectionDetail<T>(_schemaName);

        _reflectionCache.TryAdd(type, detail);

        return detail;
    }

    public async Task<TableClient> GetTableClient<T>()
    {
        var detail = GetReflectionDetail<T>();

        if (_tableClients.ContainsKey(detail.Type)) return _tableClients[detail.Type];

        var current = _client.Query($"TableName eq '{detail.TableName}'").FirstOrDefault();

        if (current == null)
            await _client.CreateTableIfNotExistsAsync(detail.TableName);

        var tableClient = new TableClient(_connectionString, detail.TableName);

        _tableClients.TryAdd(detail.Type, tableClient);

        return _tableClients[detail.Type];
    }
}