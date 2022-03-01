namespace SimpleAzureTableStorage.Core.Functions;

public static class Predicates
{
    public static bool In<T>(this T value, params T[] checkValues)
        => checkValues.Contains(value);
}