namespace CustomDI;
public class CircularDependencyException : Exception
{
    public string DependencyChain { get; }

    public CircularDependencyException(string message, string dependencyChain) 
        : base(message)
    {
        DependencyChain = dependencyChain;
    }
}