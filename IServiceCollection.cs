namespace CustomDI;
public interface IServiceCollection
{
    IServiceCollection Register<TInterface, TImplementation>() where TImplementation : TInterface;
    IServiceCollection RegisterSingleton<TInterface, TImplementation>() where TImplementation : TInterface;
    IServiceCollection RegisterSingleton<TInterface>(TInterface implementation);
    IServiceCollection RegisterScoped<TInterface, TImplementation>() where TImplementation : TInterface;
    IServiceCollection RegisterWithFactory<TService>(Func<IServiceProvider, TService> implementationFactory, ServiceLifetime lifetime);
    IServiceProvider BuildServiceProvider();
}