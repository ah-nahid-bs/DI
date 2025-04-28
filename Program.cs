using System;
using CustomDI.Examples;

namespace CustomDI;
public interface ILogger
{
    void Log(string message);
}

public class ConsoleLogger : ILogger
{
    public void Log(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[LOG] {message}");
        Console.ResetColor();
    }
}

public interface ITransientService
{
    Guid Id { get; }
    void DoSomething();
}

public class TransientService : ITransientService
{
    private readonly ILogger _logger;
    public Guid Id { get; } = Guid.NewGuid();

    public TransientService(ILogger logger)
    {
        _logger = logger;
    }

    public void DoSomething()
    {
        _logger.Log($"TransientService {Id} doing something");
    }
}

public class ServiceWithPropertyInjection
{
    [Inject]
    public ILogger Logger { get; set; }

    public void DoWork()
    {
        if (Logger != null)
        {
            Logger.Log("Property injection worked!");
        }
        else
        {
            Console.WriteLine("Property injection failed - Logger is null");
        }
    }
}

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Advanced Dependency Injection Example");
        Console.WriteLine("-------------------------------------");

        var container = new DIContainer()
            .ConfigureLogging(true)
            .ConfigureUnregisteredServiceBehavior(false)
            .ConfigureMissingDependencyBehavior(true);

        container.RegisterSingleton<ILogger, ConsoleLogger>()
                .RegisterSingleton<IMyService, MyService>()
                .Register<ITransientService, TransientService>()
                .RegisterScoped<IRepository, Repository>()
                .Register<ServiceWithPropertyInjection, ServiceWithPropertyInjection>();

        Console.WriteLine("\n=== Testing Singleton Services ===");
        var logger1 = container.Resolve<ILogger>();
        var logger2 = container.Resolve<ILogger>();
        logger1.Log($"Logger1 and Logger2 are the same instance: {ReferenceEquals(logger1, logger2)}");

        Console.WriteLine("\n=== Testing Transient Services ===");
        var transient1 = container.Resolve<ITransientService>();
        var transient2 = container.Resolve<ITransientService>();
        transient1.DoSomething();
        transient2.DoSomething();
        Console.WriteLine($"Transient1 and Transient2 are different instances: {!ReferenceEquals(transient1, transient2)}");
        Console.WriteLine($"Transient1 ID: {transient1.Id}");
        Console.WriteLine($"Transient2 ID: {transient2.Id}");

        Console.WriteLine("\n=== Testing Constructor Injection ===");
        var repository = container.Resolve<IRepository>();
        repository.Save("Item 1");
        repository.Save("Item 2");

        var items = repository.GetAll();
        Console.WriteLine("\nItems in repository:");
        foreach (var item in items)
        {
            Console.WriteLine($"- {item}");
        }

        Console.WriteLine("\n=== Testing Property Injection ===");
        var serviceWithPropertyInjection = container.Resolve<ServiceWithPropertyInjection>();
        serviceWithPropertyInjection.DoWork();

        Console.WriteLine("\n=== Testing Scoped Services ===");
        Console.WriteLine("Root scope repository items:");
        foreach (var item in repository.GetAll())
        {
            Console.WriteLine($"- {item}");
        }

        Console.WriteLine("\nCreating a new scope...");
        using (var scope = (DIContainer)container.CreateScope())
        {
            var scopedRepository = scope.Resolve<IRepository>();
            scopedRepository.Save("Item 3 (from scoped repository)");

            Console.WriteLine("\nScoped repository items:");
            foreach (var item in scopedRepository.GetAll())
            {
                Console.WriteLine($"- {item}");
            }

            var scopedLogger = scope.Resolve<ILogger>();
            scopedLogger.Log($"Logger in root and scope are the same instance: {ReferenceEquals(logger1, scopedLogger)}");

            var scopedTransient = scope.Resolve<ITransientService>();
            scopedTransient.DoSomething();
            Console.WriteLine($"Transient in scope has different ID: {scopedTransient.Id}");
        }

        Console.WriteLine("\nRoot repository items after scope is disposed:");
        foreach (var item in repository.GetAll())
        {
            Console.WriteLine($"- {item}");
        }

        Console.WriteLine("\n=== Service Resolution Statistics ===");
        var stats = container.GetResolutionStatistics();
        foreach (var stat in stats.OrderByDescending(s => s.Value))
        {
            Console.WriteLine($"- {stat.Key.Name}: {stat.Value} time(s)");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}