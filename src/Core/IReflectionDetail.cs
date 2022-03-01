using System.Reflection;

namespace SimpleAzureTableStorage.Core;

public interface IReflectionDetail
{
    string SingularName { get; }
    string PluralName { get; }
    PropertyInfo[] Properties { get; }
    Dictionary<ConstructorInfo, ParameterInfo[]> Constructors { get; }
}