namespace CustomDI.Examples;
public interface IRepository
{
    List<string> GetAll();
    void Save(string item);
}