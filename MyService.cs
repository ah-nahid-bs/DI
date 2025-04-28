namespace CustomDI.Examples;
public class MyService : IMyService
{
    public string DoWork()
    {
        Console.WriteLine("MyService is doing work...");
        return "Work completed successfully!";
    }
}