using System.Numerics;
using Microsoft.Extensions.Options;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Represents the initial screen displayed in the application, typically shown for a fixed duration.
/// Implements the <see cref="IScreen"/> interface.
/// </summary>
public sealed class CompanyScreen(IOptions<CompanyScreenOptions> options)
    : ICompanyScreen, IActivatable, IRenderable
{
    /// <summary>
    /// The asset key the studio logo is drawn from. An identifier, not text: asset keys are never
    /// translated, because a build that renamed its assets per language could not load a screen
    /// written by another.
    /// </summary>
    public const string LogoAssetKey = "olive-bw-white";

    /// <summary>
    /// How much of the screen's width the logo is allowed to span.
    /// </summary>
    /// <remarks>
    /// A proportion rather than a pixel size, so the logo holds its place when the window is
    /// resized — the asset is square and far larger than any window, so it is always scaled down
    /// and the only question is by how much. A third of the width leaves it unmistakably a logo
    /// on a field rather than a texture filling a screen.
    /// </remarks>
    public const float LogoWidthFraction = 1f / 3f;

    /// <summary>
    /// How long the logo spends fading in, and again fading out.
    /// </summary>
    /// <remarks>
    /// Taken from each end of the screen's own duration rather than added to it, so the fade never
    /// makes the screen outstay the time it was configured for. A quarter each way leaves half the
    /// duration at full opacity, which is what stops the logo reading as a flash.
    /// </remarks>
    public const float FadeFraction = 0.25f;

    ITexture? _logo;

    /// <inheritdoc />
    /// <remarks>
    /// The logo is drawn centred and faded by <see cref="Opacity"/>. Nothing is drawn until the
    /// texture has been loaded, which happens on the first frame this screen draws — the graphics
    /// device does not exist when the container is built, so the load cannot happen earlier.
    /// </remarks>
    public void Render(IRenderer renderer)
    {
        _logo ??= renderer.Textures.Load(LogoAssetKey);

        Vector2 viewport = renderer.ViewportSize;
        float scale = viewport.X * LogoWidthFraction / _logo.Width;

        renderer.Draw(new Sprite(
            Texture: _logo,
            Position: viewport / 2f,
            Rotation: 0f,
            Origin: new Vector2(_logo.Width, _logo.Height) / 2f,
            Scale: scale)
        {
            Colour = Colour.White.WithAlpha(Opacity),
        });
    }

    /// <summary>
    /// Gets how opaque the logo is right now: nothing at the very start and the very end, fully
    /// opaque through the middle.
    /// </summary>
    /// <remarks>
    /// Derived from the countdown rather than timed separately, so there is one clock on this
    /// screen and the fade cannot disagree with when the screen actually ends. A screen configured
    /// with no duration at all is fully opaque rather than dividing by zero — there is no time to
    /// fade across, and a logo that flashed up invisible would be worse than one that simply
    /// appears.
    /// </remarks>
    public float Opacity
    {
        get
        {
            if (_countdown.Duration <= TimeSpan.Zero)
            {
                return 1f;
            }

            double total = _countdown.Duration.TotalSeconds;
            double remaining = _countdown.Remaining.TotalSeconds;
            double elapsed = total - remaining;
            double fade = total * FadeFraction;

            // Whichever end is nearer decides: how far in we have come, and how far from the end
            // we still are. Taking the smaller of the two makes one expression serve both fades
            // and guarantees they meet in the middle at full opacity.
            double into = fade <= 0 ? 1 : Math.Min(elapsed, remaining) / fade;

            return (float)Math.Clamp(into, 0, 1);
        }
    }

    /// <summary>
    /// Manages the countdown timer responsible for determining the duration of the current screen.
    /// Tracks the remaining time until the screen's completion and allows the application
    /// to advance to the next stage once the countdown elapses.
    /// </summary>
    readonly Countdown _countdown = new(options.Value.Duration);

    /// <summary>
    /// Indicates whether the associated screen has finished its operation or transition.
    /// This flag is set to true once the countdown duration elapses, signaling that
    /// the screen's lifecycle is complete and the <see cref="CompanyScreen.Completed"/> event is raised.
    /// </summary>
    bool _hasCompleted;

    /// <summary>
    /// Occurs when the associated process, operation, or screen transition is completed.
    /// The event signifies the end of a specific activity or timer within the application.
    /// </summary>
    public event EventHandler? Completed;

    /// <summary>
    /// Updates the state of the screen based on the time elapsed since the last frame.
    /// This method progresses the countdown timer. If the timer has elapsed, it marks the screen
    /// as completed and raises the <see cref="Completed"/> event.
    /// </summary>
    /// <param name="frameTime">The duration of time that has passed since the last update.</param>
    public void Update(TimeSpan frameTime)
    {
        if (_hasCompleted)
        {
            return;
        }
        
        _countdown.Tick(frameTime);
        if (!_countdown.IsElapsed)
        {
            return;
        }
        
        _hasCompleted = true;
        Completed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Activates and transitions into the company screen of the application.
    /// This method is responsible for preparing the initial state of the company screen,
    /// such as displaying logos or animations, and begins the timing for the screen's duration.
    /// Typically executed as part of the <see cref="IActivatable"/> interface implementation.
    /// </summary>
    public EnterResult Enter()
    {
        _hasCompleted = false;
        _countdown.Reset();
        return EnterResult.Stay;
    }

    /// <summary>
    /// Exits the current screen of the application.
    /// Handles cleanup, resource deallocation, or any required state transitions
    /// when the screen is no longer in use. Implements the <see cref="IActivatable"/> interface.
    /// </summary>
    public void Exit()
    {
    }
}
