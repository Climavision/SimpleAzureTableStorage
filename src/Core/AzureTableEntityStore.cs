using System.Collections.Concurrent;
using System.Linq.Expressions;
using Azure.Data.Tables;
using SimpleAzureTableStorage.Core.Services;

namespace SimpleAzureTableStorage.Core;

public interface IKeyStrategy
{
    bool IsUniqueValue { get; }
    string KeyPrefix { get; }
}

public interface IKeyStrategy<in T> : IKeyStrategy
{
    string GetKey(T? entity = default);
}

class ConstantKeyStrategy<T> : IKeyStrategy<T>
{
    private readonly IStoreConfiguration _configuration;

    public ConstantKeyStrategy(IStoreConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetKey(T entity) => 
        _configuration.DefaultPartitionKey;

    public bool IsUniqueValue => false;
    public string KeyPrefix => "";
}

public interface IExpressionKeyStrategy<in T, in TValue> : IKeyStrategy<T>
{
    string BuildKey(TValue value);
}

public static class PropertyKeyStrategy
{
    public static PropertyKeyStrategy<TSubject, TProp> Of<TSubject, TProp>(Expression<Func<TSubject, TProp>> expression, bool isUniqueValue) => 
        new(expression, isUniqueValue);
}

public class PropertyKeyStrategy<T, TValue> : IExpressionKeyStrategy<T, TValue>
{
    private readonly Expression<Func<T, TValue>> _expression;
    private readonly Func<T, TValue> _func;

    public PropertyKeyStrategy(Expression<Func<T, TValue>> expression, bool isUniqueValue)
    {
        IsUniqueValue = isUniqueValue;
        _expression = expression;
        _func = expression.Compile();
        KeyPrefix = ((MemberExpression) expression.Body).Member.Name;
    }

    public bool IsUniqueValue { get; }
    public string KeyPrefix { get; }

    public string GetKey(T? entity)
    {
        var value = _func(entity);

        if (value == null)
            throw new InvalidOperationException(); // TODO: Improve

        return BuildKey(value);
    }

    public string BuildKey(TValue value) =>
        $"{KeyPrefix}::{value}"; // TODO: Set up specific type conversions to string to improve query performance
}

public interface IStoreConfiguration
{
    string ConnectionString { get; init; }
    string Schema { get; init; }
    string DefaultPartitionKey { get; init; }
}

public class StoreConfiguration : IStoreConfiguration
{
    public StoreConfiguration()
    {
    }

    public StoreConfiguration(string connectionString, string schema, string defaultPartitionKey = "")
    {
        ConnectionString = connectionString;
        Schema = schema;
        DefaultPartitionKey = defaultPartitionKey;
    }

    public string ConnectionString { get; init; } = "";
    public string Schema { get; init; } = "";
    public string DefaultPartitionKey { get; init; } = "";
}

public class AzureTableEntityStore : IEntityStore
{
    private readonly TableServiceClient _client;
    private readonly IEnumerable<IKeyStrategy> _strategies;
    private readonly ConcurrentDictionary<Type, IReflectionDetail> _reflectionCache = new();
    private readonly ConcurrentDictionary<Type, TableClient> _tableClients = new();

    public AzureTableEntityStore(string connectionString, IEnumerable<IKeyStrategy>? strategies = null, string schemaName = "") : this(new StoreConfiguration(connectionString, schemaName), strategies)
    {
    }

    public AzureTableEntityStore(IStoreConfiguration configuration, IEnumerable<IKeyStrategy>? strategies = null)
    {
        Configuration = configuration;
        _strategies = strategies ?? Array.Empty<IKeyStrategy>();
        _client = new TableServiceClient(Configuration.ConnectionString);
    }

    public IStoreConfiguration Configuration { get; }

    public IEntitySession OpenSession() =>
        new AzureTableEntitySession(this);

    public ReflectionDetail<T> GetReflectionDetail<T>()
    {
        var type = typeof(T);

        if (_reflectionCache.ContainsKey(type))
            return (ReflectionDetail<T>)_reflectionCache[type];

        var detail = new ReflectionDetail<T>(Configuration);

        _reflectionCache.TryAdd(type, detail);

        return detail;
    }
    public IEnumerable<IKeyStrategy<T>> GetStrategies<T>()
    {
        foreach (var strategy in _strategies)
            if (strategy is IKeyStrategy<T> keyStrategy)
                yield return keyStrategy;
    }

    public async Task<TableClient> GetTableClient<T>()
    {
        var detail = GetReflectionDetail<T>();

        if (_tableClients.ContainsKey(detail.Type)) return _tableClients[detail.Type];

        var current = _client.Query($"TableName eq '{detail.TableName}'").FirstOrDefault();

        if (current == null)
            await _client.CreateTableIfNotExistsAsync(detail.TableName);

        var tableClient = _client.GetTableClient(detail.TableName);

        _tableClients.TryAdd(detail.Type, tableClient);

        return _tableClients[detail.Type];
    }
}