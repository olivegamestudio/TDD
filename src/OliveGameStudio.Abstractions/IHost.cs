namespace OliveGameStudio;

public interface IHost
{
    void Start();
    
    void Update(TimeSpan frameTime);
}
