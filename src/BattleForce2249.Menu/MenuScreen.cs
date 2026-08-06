using System.Numerics;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Represents the main menu screen of the application.
/// This class provides the interface and behavior for managing the application’s
/// initial screen where users can interact with the menu options, such as starting the game.
/// Implements the <see cref="IScreen"/> interface.
/// </summary>
public sealed class MenuScreen : IMenuScreen, IActivatable, IRenderable
{
    /// <summary>
    /// The asset keys the menu is drawn from, in the order they stack. Identifiers, not text:
    /// asset keys are never translated.
    /// </summary>
    /// <remarks>
    /// The figures are drawn first and the button last, so each layer covers the one behind it.
    /// The lone figure stands behind the pair deliberately, and the horizon is drawn over both —
    /// so the planet cuts across their feet and they stand <em>in</em> the scene rather than in
    /// front of a picture of one.
    /// </remarks>
    public const string HorizonAssetKey = "title-earth";

    /// <inheritdoc cref="HorizonAssetKey" />
    public const string LoneCharacterAssetKey = "character";

    /// <inheritdoc cref="HorizonAssetKey" />
    public const string CharacterPairAssetKey = "characters";

    /// <inheritdoc cref="HorizonAssetKey" />
    public const string TitleAssetKey = "titlelogo";

    /// <inheritdoc cref="HorizonAssetKey" />
    public const string StartButtonAssetKey = "StartButton";

    /// <summary>
    /// How much of the screen each layer spans, and where it sits. Proportions rather than pixels,
    /// so the menu holds its composition at any window size — the assets are authored far larger
    /// than any window and are always scaled down.
    /// </summary>
    /// <remarks>
    /// The horizon is the exception: it is sized to the screen's <em>width</em> and pinned to the
    /// bottom edge, because it is a horizon band rather than a wallpaper. At 3.4:1 it covers only
    /// the lower part of a 16:9 window, and the space above it is meant to stay black — the band's
    /// own top edge is already near-black, so the two meet without a seam.
    /// </remarks>
    public const float LoneCharacterHeightFraction = 1.13f;

    /// <inheritdoc cref="LoneCharacterHeightFraction" />
    public const float CharacterPairHeightFraction = 1.00f;

    /// <summary>
    /// Where the bottom of each character lands, as a fraction of the screen's height.
    /// </summary>
    /// <remarks>
    /// Past 1 on purpose: the figures are taller than the window and their feet land below its
    /// bottom edge, so they are cropped rather than shrunk to fit. That is what fills the frame —
    /// a figure scaled until it fits entirely on screen is a small figure, whichever way it is
    /// anchored.
    ///
    /// Together with the height, this is what decides where a face lands: the lone figure is both
    /// the taller and the lower-anchored, so his head clears the pair's while his feet fall
    /// furthest off the bottom. The horizon is drawn over all of it.
    /// </remarks>
    public const float LoneCharacterBaselineFraction = 1.15f;

    /// <inheritdoc cref="LoneCharacterBaselineFraction" />
    public const float CharacterPairBaselineFraction = 1.13f;

    /// <summary>
    /// How tall the title logo is, and where its middle sits — both as fractions of the screen's
    /// height.
    /// </summary>
    /// <remarks>
    /// Sized by <em>height</em> rather than width, unlike everything else measured against the
    /// window. The logo shares the frame with the figures, and it is their scale it has to hold
    /// against — sizing it by width would swell it on a wide window and shrink it on a narrow one
    /// while the figures beside it stayed put.
    ///
    /// It sits over the middle of them rather than crowning the top, which is what stops the
    /// composition splitting into a band of art and a separate band of title.
    /// </remarks>
    public const float TitleHeightFraction = 0.23f;

    /// <inheritdoc cref="TitleHeightFraction" />
    public const float TitleCentreFraction = 0.63f;

    /// <inheritdoc cref="LoneCharacterHeightFraction" />
    public const float StartButtonWidthFraction = 0.13f;

    /// <summary>
    /// Where the middle of the start button sits, as a fraction of the screen's height.
    /// </summary>
    public const float StartButtonCentreFraction = 0.90f;

    /// <summary>
    /// How bright the start button is drawn when it holds focus, against
    /// <see cref="UnfocusedButtonBrightness"/> when it does not.
    /// </summary>
    /// <remarks>
    /// The focused state is a tint on the one asset rather than a second texture. The button is
    /// the only control on this screen, so a player who cannot tell it is selected has nothing
    /// else to compare it against — and dimming the unfocused state costs no art and works for
    /// every button added later.
    /// </remarks>
    public const float FocusedButtonBrightness = 1f;

    /// <inheritdoc cref="FocusedButtonBrightness" />
    public const float UnfocusedButtonBrightness = 0.55f;

    /// <summary>
    /// How opaque the start button is while it is disabled — which it is until the save state is
    /// known, per the rule that Start only becomes pressable once there is an answer.
    /// </summary>
    public const float DisabledButtonOpacity = 0.35f;

    ITexture? _horizon;
    ITexture? _loneCharacter;
    ITexture? _characterPair;
    ITexture? _title;
    ITexture? _startButtonTexture;

    /// <inheritdoc />
    /// <remarks>
    /// Textures are loaded on the first frame this screen draws rather than up front: the graphics
    /// device does not exist when the container is built, so there is nothing to load them with
    /// until a frame is actually being drawn.
    /// </remarks>
    public void Render(IRenderer renderer)
    {
        _horizon ??= renderer.Textures.Load(HorizonAssetKey);
        _loneCharacter ??= renderer.Textures.Load(LoneCharacterAssetKey);
        _characterPair ??= renderer.Textures.Load(CharacterPairAssetKey);
        _title ??= renderer.Textures.Load(TitleAssetKey);
        _startButtonTexture ??= renderer.Textures.Load(StartButtonAssetKey);

        Vector2 viewport = renderer.ViewportSize;

        // The characters go down before the horizon, so the planet is drawn over their feet and
        // they stand behind it rather than on top of it. It is what puts them in the scene instead
        // of in front of a picture of one.
        DrawCharacter(renderer, viewport, _loneCharacter, LoneCharacterHeightFraction, LoneCharacterBaselineFraction);
        DrawCharacter(renderer, viewport, _characterPair, CharacterPairHeightFraction, CharacterPairBaselineFraction);
        DrawHorizon(renderer, viewport);
        DrawTitle(renderer, viewport);
        DrawStartButton(renderer, viewport);
    }

    /// <summary>
    /// Draws the horizon band across the bottom of the screen, sized to the screen's width and
    /// pinned to its bottom edge.
    /// </summary>
    void DrawHorizon(IRenderer renderer, Vector2 viewport)
    {
        float scale = viewport.X / _horizon!.Width;

        renderer.Draw(new Sprite(
            Texture: _horizon,
            Position: new Vector2(viewport.X / 2f, viewport.Y),
            Rotation: 0f,
            // Bottom centre of the texture, so the band's own bottom edge lands on the screen's.
            Origin: new Vector2(_horizon.Width / 2f, _horizon.Height),
            Scale: scale));
    }

    /// <summary>
    /// Draws a character centred horizontally, standing on the given baseline.
    /// </summary>
    static void DrawCharacter(
        IRenderer renderer,
        Vector2 viewport,
        ITexture texture,
        float heightFraction,
        float baselineFraction)
    {
        float scale = viewport.Y * heightFraction / texture.Height;

        renderer.Draw(new Sprite(
            Texture: texture,
            Position: new Vector2(viewport.X / 2f, viewport.Y * baselineFraction),
            Rotation: 0f,
            // The texture's bottom centre, so the character stands on the baseline rather than
            // hanging from it.
            Origin: new Vector2(texture.Width / 2f, texture.Height),
            Scale: scale));
    }

    /// <summary>
    /// Draws the title logo across the upper part of the screen.
    /// </summary>
    void DrawTitle(IRenderer renderer, Vector2 viewport)
    {
        float scale = viewport.Y * TitleHeightFraction / _title!.Height;

        renderer.Draw(new Sprite(
            Texture: _title,
            Position: new Vector2(viewport.X / 2f, viewport.Y * TitleCentreFraction),
            Rotation: 0f,
            Origin: new Vector2(_title.Width, _title.Height) / 2f,
            Scale: scale));
    }

    /// <summary>
    /// Draws the start button, dimmed while it cannot be pressed and brightened while it holds
    /// focus.
    /// </summary>
    void DrawStartButton(IRenderer renderer, Vector2 viewport)
    {
        float scale = viewport.X * StartButtonWidthFraction / _startButtonTexture!.Width;

        float brightness = _controller.HasFocus ? FocusedButtonBrightness : UnfocusedButtonBrightness;
        float opacity = IsReadyForInput ? 1f : DisabledButtonOpacity;

        renderer.Draw(new Sprite(
            Texture: _startButtonTexture,
            Position: new Vector2(viewport.X / 2f, viewport.Y * StartButtonCentreFraction),
            Rotation: 0f,
            Origin: new Vector2(_startButtonTexture.Width, _startButtonTexture.Height) / 2f,
            Scale: scale)
        {
            Colour = new Colour(brightness, brightness, brightness, opacity),
        });
    }

    readonly IUIController _controller;
    readonly ISaveProgressService _saveProgressService;
    readonly Image _background = new("BACKGROUND");
    readonly Button _startButton = new("START");
    
    /// <summary>
    /// Event triggered when the player initiates a request to start the game.
    /// </summary>
    /// <remarks>
    /// This event is raised when the "Start Game" button is pressed on the main menu screen,
    /// signaling that the game should transition from the menu state to the gameplay state.
    /// Subscribers to this event should handle the necessary tasks to begin the game,
    /// such as loading game assets, initializing the game environment, or setting up the game state.
    /// </remarks>
    public event EventHandler? StartGameRequested;

    /// <summary>
    /// Represents the main menu screen of the application.
    /// This class provides the interface and behavior for managing the application’s
    /// initial screen where users can interact with the menu options, such as starting the game.
    /// Implements the <see cref="IScreen"/> interface.
    /// </summary>
    public MenuScreen(IUIController controller, ISaveProgressService saveProgressService)
    {
        _controller = controller;
        _saveProgressService = saveProgressService;

        // button is initially disabled until the save progress service has determined the player state.
        controller.Add(_startButton);
        controller.Disable(_startButton);
        
        // begin game if start button is released
        controller.OnReleased(_startButton, OnStartReleased);
    }

    /// <summary>
    /// Simulates a button press action in the main menu screen.
    /// This method triggers the associated behavior of the currently focused button,
    /// or the specified button, using the provided <see cref="IUIController"/>.
    /// </summary>
    public void Press() => _controller.Press();

    /// <summary>
    /// Releases the currently pressed button associated with the main menu screen.
    /// This method interacts with the <see cref="IUIController"/> to stop the press action
    /// and execute the associated functionality of the button if it is enabled.
    /// </summary>
    public void Release() => _controller.Release();
    
    /// <summary>
    /// Handles the event when the start button is pressed in the menu screen.
    /// Invokes the <see cref="StartGameRequested"/> event to signal that the game
    /// should transition from the menu to the gameplay state.
    /// </summary>
    void OnStartReleased()
    {
        // immediately disable to start button to prevent further presses
        // whilst we wait for screen transition/progressing
        _controller.Disable(_startButton);
        
        // request to start game - new one or continuation
        StartGameRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Updates the state of the menu screen. This method is called every frame and
    /// is responsible for processing time-dependent or frame-based logic tied to the menu,
    /// such as animations, logic transitions, or input handling.
    /// </summary>
    /// <param name="frameTime">
    /// The amount of time that has elapsed since the last frame. This is used to
    /// perform time-based updates and ensure consistent behavior across varying frame rates.
    /// </param>
    public void Update(TimeSpan frameTime)
    {
    }

    /// <summary>
    /// Activates and transitions into the main menu screen of the application.
    /// This method is responsible for setting up the initial state of the menu,
    /// including focusing on the primary UI elements such as the start button.
    /// Implements the <see cref="IActivatable"/> interface.
    /// </summary>
    public EnterResult Enter()
    {
        IsReadyForInput = false;
        _controller.Disable(_startButton);
        _saveProgressTask = HasSaveProgress();
        return EnterResult.Stay;
    }

    /// <summary>
    /// Indicates whether the menu screen is ready to accept user input.
    /// </summary>
    /// <remarks>
    /// This property becomes <c>true</c> once the necessary prerequisites, such as verifying
    /// the player's save progress, are completed and the user interface elements (e.g., buttons)
    /// are enabled. It remains <c>false</c> when the menu screen is initializing or transitioning
    /// into its active state. This flag helps ensure user interactions are processed only when
    /// the menu screen is fully prepared.
    /// </remarks>
    public bool IsReadyForInput { get; private set; }

    /// <summary>
    /// Represents a task responsible for managing the progress of saving the player's game state.
    /// </summary>
    /// <remarks>
    /// This task is utilized to handle the asynchronous operation of saving player data, initiated during
    /// specific interactions within the main menu screen. The task ensures that the save process
    /// is performed without blocking the user interface, maintaining a seamless user experience.
    /// </remarks>
    Task _saveProgressTask;

    /// <summary>
    /// Determines whether the save progress exists in the system.
    /// This method utilises the <see cref="ISaveProgressService"/> to check for the presence
    /// of saved progress data asynchronously. Once the check is complete, the method enables
    /// the start button for user interaction and sets the menu state to ready for input.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation. The task's result is true if save
    /// progress exists; otherwise, false.
    /// </returns>
    async Task HasSaveProgress()
    {
        bool result = await _saveProgressService.HasProgress();
        _controller.Enable(_startButton);
        IsReadyForInput = true;
    }

    /// <summary>
    /// Exits the main menu screen of the application.
    /// This method is responsible for handling the necessary cleanup and state transitions
    /// when leaving the main menu. Implements the <see cref="IActivatable"/> interface.
    /// </summary>
    public void Exit()
    {
    }
}
