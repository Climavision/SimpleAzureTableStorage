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

    public TableEntityService(AzureTableEntityStore store)
    {
        _store = store;
        _details = store.GetReflectionDetail<T>();
    }

    public async Task CommitChanges(bool failOnFirstException, CancellationToken token = default)
    {
        var exceptions = new List<Exception>();

        foreach (var cachedEntity in _entitiesMap.ToDictionary(x => x.Key, x => x.Value)) // TODO: Improve
        {
            var ((partitionKey, rowKey), (entity, eTag)) = cachedEntity;
            var id = _details.PickId(entity);
            var (tableEntity, currentEtag) = await GetItemFromTable(id, token);

            if (!string.Equals(eTag, currentEtag))
                throw new ConcurrencyException(_details.Type, _details.PickId(tableEntity), eTag, currentEtag);

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

                var (newEntity, newETag) = await GetItemFromTable(id, token);

                if (newEntity != null)
                    StartTracking(partitionKey, id, newEntity, newETag);
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

    public async Task Delete(T entity, CancellationToken token = default) =>
        await Delete(_details.PickId(entity), token);

    public async Task Delete(string id, CancellationToken token = default)
    {
        var tableClient = await _store.GetTableClient<T>();
        var rowKey = _details.GenerateRowKey(id);

        await tableClient.DeleteEntityAsync("Root", rowKey, cancellationToken: token);

        _entitiesMap.Remove(("Root", rowKey));
    }

    public async Task<T?> Load(string id, CancellationToken token = default)
    {
        // Check it exists in cache
        var exists = _entitiesMap.TryGetValue(("Root", _details.GenerateRowKey(id)), out var current);

        if (exists) return current?.Entity;

        // Load from table if not exists and store in cache
        var (entity, eTag) = await GetItemFromTable(id, token);

        if (entity == null) return null;

        StartTracking("Root", id, entity, eTag);

        return entity;
    }

    public void Store(T entity, string id, CancellationToken token = default)
    {
        StartTracking("Root", id, entity);
    }

    private bool Contains(string partitionKey, string id) =>
        _entitiesMap.ContainsKey((partitionKey, _details.GenerateRowKey(id)));

    private T StartTracking(string partitionKey, string id, T entity, string? eTag = null)
    {
        var rowKey = _details.GenerateRowKey(id);
        var exists = _entitiesMap.TryGetValue((partitionKey, rowKey), out var current);

        switch (exists)
        {
            case false:
                _entitiesMap.Add((partitionKey, rowKey), new CachedEntity<T>(entity, eTag));

                return entity;
            case true when entity == current?.Entity:
                return current.Entity;
            case true:
                _entitiesMap.Remove((partitionKey, rowKey));
                _entitiesMap.Add((partitionKey, rowKey), new CachedEntity<T>(entity, eTag));

                return entity;
        }
    }

    private async Task<(T? entity, string? ETag)> GetItemFromTable(string id, CancellationToken token = default)
    {
        var tableClient = await _store.GetTableClient<T>();

        try
        {
            var entityResponse = await tableClient.GetEntityAsync<TableEntity>("Root", _details.GenerateRowKey(id), cancellationToken: token);

            return (_details.LoadFrom(entityResponse.Value), entityResponse.Value.ETag.ToString());
        }
        catch (RequestFailedException)
        {
            return (default, null);
        }
    }
}