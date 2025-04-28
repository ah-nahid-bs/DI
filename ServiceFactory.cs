using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CustomDI;
internal class ServiceFactory
{
    private readonly ConcurrentDictionary<Type, Func<IServiceProvider, object>> _factories = new ConcurrentDictionary<Type, Func<IServiceProvider, object>>();

    public Func<IServiceProvider, object> CreateFactory(Type implementationType)
    {
        return _factories.GetOrAdd(implementationType, type =>
        {
            if (type.IsValueType || HasDefaultConstructor(type))
            {
                return _ => Activator.CreateInstance(type);
            }

            var constructor = type.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault();

            if (constructor == null)
            {
                throw new InvalidOperationException($"No public constructor found for type {type.Name}");
            }

            var serviceProviderParam = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
            var constructorParameters = constructor.GetParameters();
            var resolvedParameters = new Expression[constructorParameters.Length];

            for (int i = 0; i < constructorParameters.Length; i++)
            {
                var parameterType = constructorParameters[i].ParameterType;
                var getServiceCall = Expression.Call(
                    serviceProviderParam,
                    typeof(IServiceProvider).GetMethod("Resolve", new[] { typeof(Type) }),
                    Expression.Constant(parameterType));
                resolvedParameters[i] = Expression.Convert(getServiceCall, parameterType);
            }

            var newExpression = Expression.New(constructor, resolvedParameters);
            var lambda = Expression.Lambda<Func<IServiceProvider, object>>(
                newExpression,
                serviceProviderParam);

            return lambda.Compile();
        });
    }

    private bool HasDefaultConstructor(Type type)
    {
        return type.GetConstructor(Type.EmptyTypes) != null;
    }
}