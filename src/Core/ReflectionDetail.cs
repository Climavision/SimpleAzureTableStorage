using System.Reflection;
using Humanizer;

namespace SimpleAzureTableStorage.Core;

public class ReflectionDetail<T> : IReflectionDetail
{
    private readonly string _schemaName;

    public ReflectionDetail(string schemaName)
    {
        _schemaName = schemaName;
        Type = typeof(T);
        SingularName = Type.Name;
        PluralName = Type.Name.Pluralize();
        Properties = Type.GetProperties();
        Constructors = Type.GetConstructors().ToDictionary(x => x, x => x.GetParameters());
        TableName = $"{_schemaName}{PluralName}";
    }

    public Type Type { get; }
    public string SingularName { get; }
    public string PluralName { get; }
    public string TableName { get; }
    public PropertyInfo[] Properties { get; }
    public Dictionary<ConstructorInfo, ParameterInfo[]> Constructors { get; }

    public string GenerateRowKey(string id) =>
        $"{SingularName}__{id}";

    public object? GetValueFromObject(object? value)
    {
        return value switch
        {
            DateTime date => date.ToLocalTime(),
            _ => value
        };
    }

    public T LoadFrom(IDictionary<string, object> valueSource)
    {
        (ConstructorInfo Constructor, object?[] Parameters)? ctr = null;
        var values = valueSource.ToDictionary(v => v.Key.ToLowerInvariant(), v =>
        {
            var propertyType = Properties.FirstOrDefault(x => x.Name.Equals(v.Key, StringComparison.OrdinalIgnoreCase))?.PropertyType;
            var value = Functions.ReadValue(v.Value, propertyType);

            return value;
        });

        foreach (var (constructor, parameters) in Constructors)
        {
            var valuesToGrab = parameters.IntersectBy(values.Keys, p => p.Name?.ToLowerInvariant());

            if (ctr == null || ctr.Value.Parameters.Length < valuesToGrab.Count())
            {
                var parameterValues = new List<object?>();

                foreach (var parameter in parameters)
                {
                    var name = parameter.Name;

                    if (name == null)
                        continue;

                    name = name.ToLowerInvariant();

                    parameterValues.Add(values.ContainsKey(name) ? values[name] : null);
                }

                ctr = (constructor, parameterValues.ToArray());
            }
        }

        if (ctr == null)
            throw new MissingMethodException($"Unable to load Entity of type {SingularName} with Id {valueSource["Id"]}. There is no empty constructor or constructor that can be satisfied by available values {string.Join(",", valueSource.Keys)}");

        var doc = (T) ctr.Value.Constructor.Invoke(ctr.Value.Parameters);
        var remaining = Properties.Where(x => x.SetMethod != null);

        foreach (var property in remaining)
        {
            var propertyName = property.Name.ToLowerInvariant();

            if (values.ContainsKey(propertyName))
                property.SetValue(doc, GetValueFromObject(values[propertyName]));
        }

        return doc;
    }

    public string PickId(T? value)
    {
        if (value == null)
            return "";

        var idProperty = Properties.FirstOrDefault(x => x.Name.Equals("Id", StringComparison.OrdinalIgnoreCase));

        if (idProperty == null)
            throw new InvalidOperationException($"Missing Id property on type {Type.FullName}");

        var idValue = idProperty.GetValue(value);

        if (idValue == null)
            throw new InvalidOperationException($"Id property cannot be null on {Type.FullName}");

        return idValue.ToString() ?? "";
    }

    public object? GetValueAsObject<TValue>(TValue value) =>
        value switch
        {
            DateTime date => date.ToUniversalTime(),
            Enum enumValue => Enum.GetName(value.GetType(), enumValue),
            _ => value
        };

    public Dictionary<string, object?> GetValues(T entity) =>
        Properties.ToDictionary(p => p.Name, p => GetValueAsObject(p.GetValue(entity)));
}