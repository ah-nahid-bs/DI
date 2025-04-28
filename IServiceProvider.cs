namespace CustomDI;
public interface IServiceProvider : IDisposable
{
    T Resolve<T>() where T : class;
    object Resolve(Type serviceType);
    IServiceProvider CreateScope();
}