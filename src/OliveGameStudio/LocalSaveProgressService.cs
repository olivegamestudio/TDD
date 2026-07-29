namespace OliveGameStudio;

public class LocalSaveProgressService : ISaveProgressService
{
    public Task<bool> HasProgress()
    {
        return Task.FromResult(true);
    }
}
