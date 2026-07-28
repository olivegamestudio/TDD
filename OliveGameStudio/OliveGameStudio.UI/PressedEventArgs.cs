namespace OliveGameStudio;

public sealed class PressedEventArgs(Button button) : EventArgs
{
    public Button Button { get; } = button;
}
