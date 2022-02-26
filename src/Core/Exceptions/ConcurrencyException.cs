namespace SimpleAzureTableStorage.Core.Exceptions;

public class ConcurrencyException : Exception
{
    public ConcurrencyException(Type type, string id, string? etagExpected, string? etagActual) :
        base($"An issue with concurrency has occurred. There has been changes made since the entity was last retrieved. Type: {type}, Id: {id}, ExpectedETag: {etagExpected ?? "(Empty)"}, ActualETag: {etagActual ?? "(Empty)"}")
    {
    }
}