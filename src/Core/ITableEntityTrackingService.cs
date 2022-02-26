namespace SimpleAzureTableStorage.Core;

public interface ITableEntityTrackingService
{
    Task CommitDirtyEntities(AzureTableEntityStore store, CancellationToken token = default);
}

public interface ITableEntityTrackingService<T>
{
    T StartTracking(T entity, string partitionKey, string rowKey, string? eTag = null);
}