using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace CustomDI;
public class DIContainer : IServiceCollection, IServiceProvider
{
    private readonly List<ServiceDescriptor> _descriptors = new List<ServiceDescriptor>();
    private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();
    private readonly Dictionary<Type, object> _scopedInstances;
    private readonly DIContainer _rootContainer;
    private bool _disposed;
    private readonly ThreadLocal<Stack<Stack<Type>>> _resolutionStack = new ThreadLocal<Stack<Stack<Type>>>(() => new Stack<Stack<Type>>());
    private readonly Dictionary<Type, int> _resolutionCounts = new Dictionary<Type, int>();
    private bool _throwOnMissingService = true;
    private Action<string> _logger;
    private bool _enableLogging;
    private readonly ServiceFactory _serviceFactory = new ServiceFactory();

    public DIContainer()
    {
        _rootContainer = this;
        _scopedInstances = new Dictionary<Type, object>();
        RegisterSingleton<IServiceProvider>(this);
    }

    private DIContainer(DIContainer rootContainer)
    {
        _rootContainer = rootContainer;
        _descriptors = rootContainer._descriptors;
        _singletons = rootContainer._singletons;
        _scopedInstances = new Dictionary<Type, object>();
    }

    public IServiceCollection Register<TInterface, TImplementation>() where TImplementation : TInterface
    {
        _descriptors.Add(new ServiceDescriptor(typeof(TInterface), typeof(TImplementation), ServiceLifetime.Transient));
        LogMessage($"Registered transient service: {typeof(TInterface).Name} → {typeof(TImplementation).Name}");
        return this;
    }

    public IServiceCollection RegisterSingleton<TInterface, TImplementation>() where TImplementation : TInterface
    {
        _descriptors.Add(new ServiceDescriptor(typeof(TInterface), typeof(TImplementation), ServiceLifetime.Singleton));
        LogMessage($"Registered singleton service: {typeof(TInterface).Name} → {typeof(TImplementation).Name}");
        return this;
    }

    public IServiceCollection RegisterSingleton<TInterface>(TInterface implementation)
    {
        _descriptors.Add(new ServiceDescriptor(typeof(TInterface), implementation));
        LogMessage($"Registered singleton instance of {typeof(TInterface).Name}");
        return this;
    }

    public IServiceCollection RegisterScoped<TInterface, TImplementation>() where TImplementation : TInterface
    {
        _descriptors.Add(new ServiceDescriptor(typeof(TInterface), typeof(TImplementation), ServiceLifetime.Scoped));
        LogMessage($"Registered scoped service: {typeof(TInterface).Name} → {typeof(TImplementation).Name}");
        return this;
    }

    public IServiceCollection RegisterWithFactory<TService>(Func<IServiceProvider, TService> implementationFactory, ServiceLifetime lifetime)
    {
        _descriptors.Add(new ServiceDescriptor(typeof(TService), provider => implementationFactory(provider), lifetime));
        LogMessage($"Registered service {typeof(TService).Name} with factory and lifetime {lifetime}");
        return this;
    }

    public IServiceProvider BuildServiceProvider()
    {
        ValidateServiceRegistrations();
        return this;
    }

    private void ValidateServiceRegistrations()
    {
        foreach (var descriptor in _descriptors.Where(d => d.Lifetime == ServiceLifetime.Singleton))
        {
            CheckForScopedDependencies(descriptor.ImplementationType, new HashSet<Type>());
        }
    }

    private void CheckForScopedDependencies(Type type, HashSet<Type> visited)
    {
        if (type == null || !visited.Add(type))
        {
            return;
        }

        var constructors = type.GetConstructors();
        if (constructors.Length == 0)
        {
            return;
        }

        var constructor = constructors.OrderByDescending(c => c.GetParameters().Length).First();
        foreach (var parameter in constructor.GetParameters())
        {
            var parameterType = parameter.ParameterType;
            var descriptor = _descriptors.FirstOrDefault(d => d.ServiceType == parameterType);
            if (descriptor != null && descriptor.Lifetime == ServiceLifetime.Scoped)
            {
                throw new InvalidOperationException(
                    $"Cannot consume scoped service '{parameterType.Name}' from singleton '{type.Name}'.");
            }
            if (descriptor?.ImplementationType != null)
            {
                CheckForScopedDependencies(descriptor.ImplementationType, visited);
            }
        }
    }

    public T Resolve<T>() where T : class
    {
        return (T)Resolve(typeof(T));
    }

    public object Resolve(Type serviceType)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DIContainer));
        }

        var descriptor = _descriptors.FirstOrDefault(d => d.ServiceType == serviceType);
        if (descriptor == null)
        {
            LogMessage($"Error: Service of type {serviceType.Name} is not registered.");
            if (_throwOnMissingService)
            {
                throw new InvalidOperationException($"No service of type {serviceType.FullName} has been registered.");
            }
            return null;
        }

        TrackResolution(serviceType);
        LogMessage($"Resolving service of type {serviceType.Name} with lifetime {descriptor.Lifetime}");
        return GetServiceInstance(descriptor);
    }

    private void TrackResolution(Type serviceType)
    {
        lock (_resolutionCounts)
        {
            _resolutionCounts[serviceType] = _resolutionCounts.GetValueOrDefault(serviceType, 0) + 1;
        }
    }

    public IReadOnlyDictionary<Type, int> GetResolutionStatistics()
    {
        return _resolutionCounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public DIContainer ConfigureUnregisteredServiceBehavior(bool throwOnMissingService)
    {
        _throwOnMissingService = throwOnMissingService;
        return this;
    }

    public DIContainer ConfigureMissingDependencyBehavior(bool throwOnMissingDependency)
    {
        // Implementation can be added if needed
        return this;
    }

    public IServiceProvider CreateScope()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DIContainer));
        }

        LogMessage("Creating new scope");
        return new DIContainer(_rootContainer);
    }

    private object GetServiceInstance(ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance != null)
        {
            return descriptor.ImplementationInstance;
        }

        switch (descriptor.Lifetime)
        {
            case ServiceLifetime.Singleton:
                return GetSingletonInstance(descriptor);
            case ServiceLifetime.Scoped:
                return GetScopedInstance(descriptor);
            case ServiceLifetime.Transient:
            default:
                return CreateInstance(descriptor);
        }
    }

    private object GetSingletonInstance(ServiceDescriptor descriptor)
    {
        var serviceType = descriptor.ServiceType;
        lock (_rootContainer._singletons)
        {
            if (_rootContainer._singletons.TryGetValue(serviceType, out var instance))
            {
                LogMessage($"Retrieved existing singleton instance of {serviceType.Name}");
                return instance;
            }

            LogMessage($"Creating new singleton instance of {serviceType.Name}");
            instance = CreateInstance(descriptor);
            _rootContainer._singletons[serviceType] = instance;
            return instance;
        }
    }

    private object GetScopedInstance(ServiceDescriptor descriptor)
    {
        var serviceType = descriptor.ServiceType;
        lock (_scopedInstances)
        {
            if (_scopedInstances.TryGetValue(serviceType, out var instance))
            {
                LogMessage($"Retrieved existing scoped instance of {serviceType.Name}");
                return instance;
            }

            LogMessage($"Creating new scoped instance of {serviceType.Name}");
            instance = CreateInstance(descriptor);
            _scopedInstances[serviceType] = instance;
            return instance;
        }
    }

    private object CreateInstance(ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationFactory != null)
        {
            return descriptor.ImplementationFactory(this);
        }

        var factory = _serviceFactory.CreateFactory(descriptor.ImplementationType);
        var instance = factory(this);

        // Handle property injection
        var properties = descriptor.ImplementationType.GetProperties()
            .Where(p => p.GetCustomAttribute<InjectAttribute>() != null);
        foreach (var property in properties)
        {
            var service = Resolve(property.PropertyType);
            if (service != null)
            {
                property.SetValue(instance, service);
            }
        }

        return instance;
    }

    public DIContainer ConfigureLogging(bool enableLogging, Action<string> logger = null)
    {
        _enableLogging = enableLogging;
        _logger = logger ?? Console.WriteLine;
        return this;
    }

    private void LogMessage(string message)
    {
        if (_enableLogging && _logger != null)
        {
            _logger($"[DIContainer] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        LogMessage("Disposing container");
        _disposed = true;

        foreach (var instance in _scopedInstances.Values.OfType<IDisposable>())
        {
            LogMessage($"Disposing scoped instance of {instance.GetType().Name}");
            instance.Dispose();
        }

        if (this == _rootContainer)
        {
            foreach (var instance in _singletons.Values.OfType<IDisposable>())
            {
                LogMessage($"Disposing singleton instance of {instance.GetType().Name}");
                instance.Dispose();
            }
        }

        _scopedInstances.Clear();
    }
}