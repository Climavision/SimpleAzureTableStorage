using System.Linq;
using System.Linq.Expressions;
using Azure;
using Azure.Data.Tables;
using SimpleAzureTableStorage.Core.Exceptions;

namespace SimpleAzureTableStorage.Core;

internal class TableEntityService<T> : ITableEntityService<T>, ITableEntityService
    where T : class
{
    private readonly ReflectionDetail<T> _details;
    private readonly Dictionary<(string partitionKey, string rowKey), CachedEntity<T>> _entitiesMap = new();
    private readonly AzureTableEntityStore _store;
    private readonly IKeyStrategy<T>[] _uniqueStrategies;
    private readonly IKeyStrategy<T>[] _nonUniqueStrategies;
    private readonly IKeyStrategy<T> _defaultNonUniqueStrategy;

    public TableEntityService(AzureTableEntityStore store)
    {
        _store = store;
        _details = store.GetReflectionDetail<T>();
        
        var strategies = store.GetStrategies<T>().ToArray();

        _uniqueStrategies = strategies.Where(x => x.IsUniqueValue).ToArray();
        _nonUniqueStrategies = strategies.Where(x => !x.IsUniqueValue).ToArray();

        if (!_uniqueStrategies.Any() && _details.IdKeyStrategy != null)
            _uniqueStrategies = new[] { _details.IdKeyStrategy };

        if (!_nonUniqueStrategies.Any())
            _nonUniqueStrategies = new IKeyStrategy<T>[] { new ConstantKeyStrategy<T>(store.Configuration) };

        _defaultNonUniqueStrategy = _nonUniqueStrategies.First();
    }

    public async Task CommitChanges(bool failOnFirstException, CancellationToken token = default)
    {
        var exceptions = new List<Exception>();

        foreach (var cachedEntity in _entitiesMap.ToDictionary(x => x.Key, x => x.Value)) // TODO: Improve
        {
            var ((partitionKey, rowKey), (entity, eTag)) = cachedEntity;
            var (tableEntity, currentEtag) = await GetItemFromTable(partitionKey, rowKey, token);

            if (!string.Equals(eTag, currentEtag))
                throw new ConcurrencyException(_details.Type, rowKey, eTag, currentEtag);

            var values = _details.GetValues(entity);

            // Check for changes
            if (tableEntity != null)
            {
                var storedValues = _details.GetValues(tableEntity);

                if (!values.Except(storedValues).Any()) // No changes detected. TODO: improve
                    continue;
            }

            var tableClient = await _store.GetTableClient<T>();

            values.Add("PartitionKey", partitionKey);
            values.Add("RowKey", rowKey);

            try
            {
                var response = await tableClient.UpsertEntityAsync(new TableEntity(values), cancellationToken: token);

                _entitiesMap.Remove((partitionKey, rowKey));

                var (newEntity, newETag) = await GetItemFromTable(partitionKey, rowKey, token);

                if (newEntity != null)
                    StartTracking(newEntity, rowKey, partitionKey, newETag);
            }
            catch (RequestFailedException failed)
            {
                var exception = new EntityCommitFailureException(failed.ErrorCode);

                if (failOnFirstException) throw exception;

                exceptions.Add(exception);
            }
            catch (Exception e)
            {
                var exception = new Exception("An unhandled exception occurred while committing entity", e);

                if (failOnFirstException) throw exception;

                exceptions.Add(exception);
            }

            if (exceptions.Any()) throw new AggregateException(exceptions);
        }
    }

    public async Task Delete(string id, CancellationToken token = default)
    {
        var entity = await Load(id, token);

        if (entity != null) 
            await Delete(entity, token);
    }

    public async Task Delete(T entity, CancellationToken token = default)
    {
        var tableClient = await _store.GetTableClient<T>();

        foreach (var nonUniqueStrategy in _nonUniqueStrategies)
        {
            foreach (var uniqueStrategy in _uniqueStrategies)
            {
                var rowKey = uniqueStrategy.GetKey(entity);
                var partitionKey = nonUniqueStrategy.GetKey(entity);

                await tableClient.DeleteEntityAsync(partitionKey, rowKey, cancellationToken: token);

                _entitiesMap.Remove((partitionKey, rowKey));
            }
        }
    }

    // TODO: Work on the thought behind this logic
    public async Task<T?> Load(string id, CancellationToken token = default)
    {
        var rowKey = $"{_details.IdKeyStrategy?.KeyPrefix}::{id}";
        var client = await _store.GetTableClient<T>();
        var query = $"RowKey eq '{rowKey}'";

        if (_defaultNonUniqueStrategy is ConstantKeyStrategy<T> strategy)
            query = $"PartitionKey eq '{strategy.GetKey(default)}' and {query}";
        else
            query = $"(PartitionKey ge '{_defaultNonUniqueStrategy.KeyPrefix}' and PartitionKey le '{_defaultNonUniqueStrategy.KeyPrefix}zzzzzzzzzz') and {query}";
        
        var result = client.Query<TableEntity>(query).DistinctBy(x => x.RowKey).ToList();

        if (result.Count != 1)
            throw new InvalidOperationException("Inconsistent state across records with the same RowKey");

        return await Load(result.First().PartitionKey, result.First().RowKey, token);
    }

    private async Task<T?> Load(string partitionKey, string rowKey, CancellationToken token = default)
    {
        // Check it exists in cache
        var exists = _entitiesMap.TryGetValue((partitionKey, rowKey), out var current);

        if (exists) return current?.Entity;

        // Load from table if not exists and store in cache
        var (entity, eTag) = await GetItemFromTable(partitionKey, rowKey, token);

        if (entity == null) return null;

        StartTracking(entity, rowKey, partitionKey, eTag);

        return entity;
    }

    public Task<T?> Load<TKeyValue>(Expression<Func<T, TKeyValue>> keyProp, TKeyValue value, CancellationToken token = default)
    {
        var expression = new PropertyKeyStrategy<T, TKeyValue>(keyProp, true);

        return Load(_defaultNonUniqueStrategy.GetKey(), expression.BuildKey(value), token);
    }

    public Task<T?> Load<TKeyValue, TPartitionValue>(Expression<Func<T, TKeyValue>> keyProp, TKeyValue keyValue, Expression<Func<T, TPartitionValue>> partitionProp, TPartitionValue partitionValue, CancellationToken token = default)
    {
        var keyStrategy = new PropertyKeyStrategy<T, TKeyValue>(keyProp, true);
        var partitionStrategy = new PropertyKeyStrategy<T, TPartitionValue>(partitionProp, false);

        return Load(partitionStrategy.BuildKey(partitionValue), keyStrategy.BuildKey(keyValue), token);
    }

    public void Store(T entity, CancellationToken token = default)
    {
        StartTracking(entity);
    }

    private T StartTracking(T entity, string? eTag = null)
    {
        foreach (var nonUniqueStrategy in _nonUniqueStrategies)
        {
            foreach (var uniqueStrategy in _uniqueStrategies)
            {
                var rowKey = uniqueStrategy.GetKey(entity);
                var partitionKey = nonUniqueStrategy.GetKey(entity);

                StartTracking(entity, rowKey, partitionKey, eTag);
            }
        }

        return entity;
    }

    private T StartTracking(T entity, string rowKey, string partitionKey, string? eTag = null)
    {
        var exists = _entitiesMap.TryGetValue((partitionKey, rowKey), out var current);

        switch (exists)
        {
            case false:
                _entitiesMap.Add((partitionKey, rowKey), new CachedEntity<T>(entity, eTag));

                break;
            case true when entity == current?.Entity:
                break;
            case true:
                _entitiesMap.Remove((partitionKey, rowKey));
                _entitiesMap.Add((partitionKey, rowKey), new CachedEntity<T>(entity, eTag));
                break;
        }

        return entity;
    }

    private async Task<(T? entity, string? ETag)> GetItemFromTable(string partitionKey, string rowKey, CancellationToken token = default)
    {
        var tableClient = await _store.GetTableClient<T>();

        try
        {
            var entityResponse = await tableClient.GetEntityAsync<TableEntity>(partitionKey, rowKey, cancellationToken: token);

            return (_details.LoadFrom(entityResponse.Value), entityResponse.Value.ETag.ToString());
        }
        catch (RequestFailedException)
        {
            return (default, null);
        }
    }
}