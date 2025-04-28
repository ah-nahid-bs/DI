namespace CustomDI;
internal class ServiceDescriptor
{
    public Type ServiceType { get; }
    public Type ImplementationType { get; }
    public Func<IServiceProvider, object> ImplementationFactory { get; }
    public object ImplementationInstance { get; set; }
    public ServiceLifetime Lifetime { get; }

    public ServiceDescriptor(Type serviceType, Type implementationType, ServiceLifetime lifetime)
    {
        ServiceType = serviceType;
        ImplementationType = implementationType;
        Lifetime = lifetime;
    }

    public ServiceDescriptor(Type serviceType, Func<IServiceProvider, object> implementationFactory, ServiceLifetime lifetime)
    {
        ServiceType = serviceType;
        ImplementationFactory = implementationFactory;
        Lifetime = lifetime;
    }

    public ServiceDescriptor(Type serviceType, object implementationInstance)
    {
        ServiceType = serviceType;
        ImplementationInstance = implementationInstance;
        Lifetime = ServiceLifetime.Singleton;
    }
}