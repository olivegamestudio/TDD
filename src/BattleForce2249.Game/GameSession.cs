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
    readonly IShipyard _shipyard;

    /// <summary>
    /// Whether a quest changing state should write a save. It is turned off while a game is being
    /// set up, so starting or resuming writes at most one save rather than one per quest.
    /// </summary>
    bool _autoSave;

    /// <summary>
    /// Creates a session for the given campaign, world and ships.
    /// </summary>
    /// <param name="saveProgressService">The service the save game is written to and read from.</param>
    /// <param name="campaign">The quests the session plays.</param>
    /// <param name="world">The world it plays them in.</param>
    /// <param name="shipyard">The ships it can put the player in.</param>
    public GameSession(
        ISaveProgressService saveProgressService,
        ICampaign campaign,
        IWorld world,
        IShipyard shipyard)
    {
        _saveProgressService = saveProgressService;
        _campaign = campaign;
        _world = world;
        _shipyard = shipyard;

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

        // a save naming a ship this build no longer has puts the player back in the starting one
        // rather than failing to load: the same tolerance the save's quest list already gets, and
        // being left shipless is a worse outcome than being demoted a hull
        Player.GiveShip(_shipyard.Find(save.ShipId) ?? _shipyard.StartingShip);

        Quests.Restore(save.Quests);

        _autoSave = true;
        IsReady = true;
    }

    /// <inheritdoc />
    public Task Save() => _saveProgressService.Save(SaveGameSerializer.Serialize(Capture()));

    /// <summary>
    /// Returns the session to a freshly registered campaign, with the player at the world's start
    /// in the ship a new game awards.
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
        Player.GiveShip(_shipyard.StartingShip);
    }

    /// <summary>
    /// Takes a snapshot of the session for persisting.
    /// </summary>
    SaveGame Capture() => new()
    {
        PlayerX = Player.Position.X,
        PlayerY = Player.Position.Y,

        // blank only if something saved before a game was started or resumed, which reads back as
        // "no ship recorded" and awards the starting one — the same as an older save
        ShipId = Player.Ship?.Id ?? string.Empty,
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
