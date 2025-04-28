using System;
using System.Collections.Generic;

namespace CustomDI.Examples;
public class Repository : IRepository
{
    private readonly IMyService _myService;
    private readonly List<string> _items = new List<string>();

    public Repository(IMyService myService)
    {
        _myService = myService ?? throw new ArgumentNullException(nameof(myService));
    }

    public List<string> GetAll()
    {
        Console.WriteLine("Repository is getting all items...");
        _myService.DoWork();
        return new List<string>(_items);
    }

    public void Save(string item)
    {
        if (string.IsNullOrEmpty(item))
        {
            throw new ArgumentException("Item cannot be null or empty", nameof(item));
        }

        var result = _myService.DoWork();
        Console.WriteLine($"Repository is saving item: {item}");
        Console.WriteLine($"Service result: {result}");
        _items.Add(item);
    }
}