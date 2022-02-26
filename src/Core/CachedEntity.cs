namespace SimpleAzureTableStorage.Core;

public record CachedEntity<T>(T Entity, string? ETag = null);