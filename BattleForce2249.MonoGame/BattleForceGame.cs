using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xna.Framework;
using OliveGameStudio;

namespace BattleForce2249;

public class BattleForceGame : Game
{
    GraphicsDeviceManager _graphics;
    //SpriteBatch _spriteBatch;

    readonly LifecycleScreenDirector _screenDirector = new(NullLogger<LifecycleScreenDirector>.Instance);
    readonly IUIController _controller = new UIController();
    readonly ISaveProgressService _saveProgress = new LocalSaveProgressService();
    readonly BattleForceHost _host;

    public BattleForceGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // start game host
        _host = new BattleForceHost(_screenDirector, _controller, _saveProgress);
    }

    protected override void Initialize()
    {
        // TODO: Add your initialization logic here

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