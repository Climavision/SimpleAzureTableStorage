namespace SimpleAzureTableStorage.Core;

public static class Functions
{
    public static object ReadValue(object value, Type? type) =>
        value switch
        {
            DateTimeOffset offset => offset.DateTime,
            string stringValue when type?.IsEnum ?? false => Enum.Parse(type, stringValue),
            _ => value
        };
}