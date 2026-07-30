using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OliveGameStudio;

namespace BattleForce2249.Tests;

public abstract class HostTestBase
{
    readonly IScreenDirector _screenDirector = new ScreenDirector(NullLogger<ScreenDirector>.Instance);
    readonly IUIController _controller = Mock.Of<IUIController>();
    readonly ISaveProgressService _saveProgress = Mock.Of<ISaveProgressService>();

    protected IScreenDirector ScreenDirector => _screenDirector;
    
    public IHost CreateHost()
    {
        return new BattleForceHost(_screenDirector, _controller, _saveProgress);
    }
}
