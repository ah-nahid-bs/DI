namespace CustomDI;
public class DependencyResolutionException : Exception
{
    public Type ImplementationType { get; }
    public Type DependencyType { get; }
    public string ParameterName { get; }

    public DependencyResolutionException(string message, Type implementationType, Type dependencyType, string parameterName) 
        : base(message)
    {
        ImplementationType = implementationType;
        DependencyType = dependencyType;
        ParameterName = parameterName;
    }
}
