namespace SimpleAzureTableStorage.Core.Services;

public interface IEntitySession
{
    Task Delete<T>(T entity, CancellationToken token = default) where T : class;
    Task Delete<T>(string id, CancellationToken token = default) where T : class;

    Task SaveChanges(CancellationToken token = default);

    void Store<T>(T entity, CancellationToken token = default) where T : class;
    void Store<T>(T entity, string id, CancellationToken token = default) where T : class;

    Task<T?> Load<T>(string id, CancellationToken token = default) where T : class;
    Task<Dictionary<string, T?>> Load<T>(IEnumerable<string> ids, CancellationToken token = default) where T : class;
    IAsyncEnumerable<T> Query<T>(string query);
}

public interface IEntityStore
{
    IEntitySession OpenSession();
}