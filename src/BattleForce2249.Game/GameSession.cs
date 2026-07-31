using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249;

/// <summary>
/// The default <see cref="IGameSession"/>. It holds the player and the quest log, and persists both
/// through the engine's save progress service whenever a quest starts or completes.
/// </summary>
public sealed class GameSession : IGameSession
{
    readonly ISaveProgressService _saveProgressService;
    readonly ICampaign _campaign;
    readonly IWorld _world;

    /// <summary>
    /// Whether a quest changing state should write a save. It is turned off while a game is being
    /// set up, so starting or resuming writes at most one save rather than one per quest.
    /// </summary>
    bool _autoSave;

    /// <summary>
    /// Creates a session for the given campaign and world.
    /// </summary>
    /// <param name="saveProgressService">The service the save game is written to and read from.</param>
    /// <param name="campaign">The quests the session plays.</param>
    /// <param name="world">The world it plays them in.</param>
    public GameSession(ISaveProgressService saveProgressService, ICampaign campaign, IWorld world)
    {
        _saveProgressService = saveProgressService;
        _campaign = campaign;
        _world = world;

        Quests.QuestStarted += OnQuestChanged;
        Quests.QuestCompleted += OnQuestChanged;
    }

    /// <inheritdoc />
    public Player Player { get; } = new();

    /// <inheritdoc />
    public QuestLog Quests { get; } = new();

    /// <inheritdoc />
    public bool IsReady { get; private set; }

    /// <inheritdoc />
    public Task PendingSave { get; private set; } = Task.CompletedTask;

    /// <inheritdoc />
    public async Task StartNewGame()
    {
        Reset();

        _autoSave = true;
        IsReady = true;

        await Save();
    }

    /// <inheritdoc />
    public async Task Continue()
    {
        SaveGame? save = SaveGameSerializer.Deserialize(await _saveProgressService.Load());
        if (save is null)
        {
            await StartNewGame();
            return;
        }

        Reset();
        Player.MoveTo(new Position(save.PlayerX, save.PlayerY));
        Quests.Restore(save.Quests);

        _autoSave = true;
        IsReady = true;
    }

    /// <inheritdoc />
    public Task Save() => _saveProgressService.Save(SaveGameSerializer.Serialize(Capture()));

    /// <summary>
    /// Returns the session to a freshly registered campaign with the player at the world's start.
    /// </summary>
    void Reset()
    {
        _autoSave = false;
        IsReady = false;

        Quests.Clear();
        foreach (QuestDefinition definition in _campaign.Quests)
        {
            Quests.Register(definition);
        }

        Player.MoveTo(_world.PlayerStart);
    }

    /// <summary>
    /// Takes a snapshot of the session for persisting.
    /// </summary>
    SaveGame Capture() => new()
    {
        PlayerX = Player.Position.X,
        PlayerY = Player.Position.Y,
        Quests = Quests.Capture(),
    };

    /// <summary>
    /// Saves in the background when a quest starts or completes, which is the only progress worth
    /// persisting; saving every frame the player moves would write constantly.
    /// </summary>
    void OnQuestChanged(object? sender, QuestEventArgs e)
    {
        if (_autoSave)
        {
            PendingSave = Save();
        }
    }
}
