using Microsoft.Xna.Framework;
using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// The MonoGame platform host. It owns the window and the frame loop, and drives the game
/// purely through <see cref="IHost"/>. The host is resolved by the composition root in
/// <c>Program</c>, so this class decides nothing about which implementations are used.
/// </summary>
public class BattleForceGame : Game
{
    readonly GraphicsDeviceManager _graphics;

    readonly IHost _host;

    /// <summary>
    /// The frame's renderer. Created in <see cref="LoadContent"/> rather than in the
    /// constructor, because it needs a graphics device and MonoGame has not made one yet.
    /// </summary>
    MonoGameRenderer? _renderer;

    public BattleForceGame(IHost host)
    {
        _host = host;

        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _host.Start();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _renderer = new MonoGameRenderer(GraphicsDevice, new MonoGameTextureLoader(Content));
    }

    protected override void Update(GameTime gameTime)
    {
        _host.Update(gameTime.ElapsedGameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // Space, not cornflower blue. Everything the player sees is drawn over this.
        GraphicsDevice.Clear(Color.Black);

        if (_renderer is not null)
        {
            _renderer.BeginFrame();
            _host.Draw(_renderer);
            _renderer.EndFrame();
        }

        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _renderer?.Dispose();
        _renderer = null;

        base.UnloadContent();
    }
}
