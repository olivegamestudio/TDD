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
    //SpriteBatch _spriteBatch;

    readonly IHost _host;

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
        //_spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        _host.Update(gameTime.ElapsedGameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // TODO: Add your drawing code here

        base.Draw(gameTime);
    }
}