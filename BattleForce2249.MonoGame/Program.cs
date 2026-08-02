using System;
using BattleForce2249;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;

// Composition root. This is the one place that decides which implementations the game runs
// with and where its settings come from; everything downstream depends on interfaces only.

// Debug builds default to the Development overlay, so a developer gets the shortened splash
// without editing anything. Set BATTLEFORCE_ENVIRONMENT to override either way, which keeps
// release timing reproducible from a debug build and vice versa.
var environment = Environment.GetEnvironmentVariable("BATTLEFORCE_ENVIRONMENT")
#if DEBUG
                  ?? "Development";
#else
                  ?? "Production";
#endif

IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables("BATTLEFORCE_")
    .Build();

ServiceCollection services = new();

services.AddLogging();

services.AddBattleForce(configuration);

// Registered after AddBattleForce so it wins over the default, because the engine uses
// AddSingleton and the last registration is the one resolved. Registration chooses the
// implementation; configuration tunes it.
// services.AddSingleton<IFrameTimeController, ScaledFrameTimeController>();

// The keyboard and the gamepad, for the same reason and in the same place: the engine ships
// nobody at the controls, and the input device is the platform host's to own. Without this line
// the game launches, starts quest 1, and the ship sits on the start marker for ever.
services.AddDesktopPilot();

using ServiceProvider provider = services.BuildServiceProvider();

using BattleForceGame game = new(provider.GetRequiredService<IHost>());
game.Run();
