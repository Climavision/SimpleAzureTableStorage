namespace SimpleAzureTableStorage.Core.Exceptions;

public class EntityCommitFailureException : Exception
{
    public EntityCommitFailureException(string? errorCode) : base($"An error occurred while commiting with ErrorCode: {errorCode}")
    {

    }
}