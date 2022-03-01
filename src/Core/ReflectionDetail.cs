using System.Linq.Expressions;
using System.Reflection;
using Humanizer;
using SimpleAzureTableStorage.Core.Functions;

namespace SimpleAzureTableStorage.Core;

public class ReflectionDetail<T> : IReflectionDetail
{
    private readonly IStoreConfiguration _configuration;

    public ReflectionDetail(IStoreConfiguration configuration)
    {
        _configuration = configuration;
        Type = typeof(T);
        SingularName = Type.Name;
        PluralName = Type.Name.Pluralize();
        Properties = Type.GetProperties();
        Constructors = Type.GetConstructors().ToDictionary(x => x, x => x.GetParameters());
        TableName = $"{configuration.Schema}{PluralName}";

        var idProperty = Properties.FirstOrDefault(x => x.Name.In("Id", $"{SingularName}Id"));

        if (idProperty == null) return;

        var param = Expression.Parameter(Type, SingularName);
        var propertyExpression = Expression.Property(param, idProperty);
        var expression = Expression.Lambda(propertyExpression, param);
        var strategyType = typeof(PropertyKeyStrategy<,>).MakeGenericType(Type, idProperty.PropertyType);

        IdKeyStrategy = Activator.CreateInstance(strategyType, expression, true) as IKeyStrategy<T>;
    }

    public IKeyStrategy<T>? IdKeyStrategy { get; }

    public Type Type { get; }
    public string TableName { get; }
    public string SingularName { get; }
    public string PluralName { get; }
    public PropertyInfo[] Properties { get; }
    public Dictionary<ConstructorInfo, ParameterInfo[]> Constructors { get; }

    public object? GetValueFromObject(object? value)
    {
        return value switch
        {
            DateTime date => date.ToLocalTime(),
            _ => value
        };
    }

    public static object ReadValue(object value, Type? type) =>
        value switch
        {
            DateTimeOffset offset => offset.DateTime,
            string stringValue when type?.IsEnum ?? false => Enum.Parse(type, stringValue),
            _ => value
        };

    public T LoadFrom(IDictionary<string, object> valueSource)
    {
        (ConstructorInfo Constructor, object?[] Parameters)? ctr = null;
        var values = valueSource.ToDictionary(v => v.Key.ToLowerInvariant(), v =>
        {
            var propertyType = Properties.FirstOrDefault(x => x.Name.Equals(v.Key, StringComparison.OrdinalIgnoreCase))?.PropertyType;
            var value = ReadValue(v.Value, propertyType);

            return value;
        });

        foreach (var (constructor, parameters) in Constructors)
        {
            var valuesToGrab = parameters.IntersectBy(values.Keys, p => p.Name?.ToLowerInvariant());

            if (ctr != null && ctr.Value.Parameters.Length >= valuesToGrab.Count()) continue;

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