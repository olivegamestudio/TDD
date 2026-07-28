namespace OliveGameStudio;

public abstract record EnterResult
{
    public static readonly EnterResult Stay = new StayResult();
    
    public static EnterResult RedirectTo(IScreen screen) => new Redirect(screen);

    public sealed record StayResult : EnterResult;
    
    public sealed record Redirect(IScreen Screen) : EnterResult;
}
