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
    readonly IShipYard _ships;

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
    /// <param name="ships">The ships it can award, and can find again when a save names one.</param>
    public GameSession(
        ISaveProgressService saveProgressService,
        ICampaign campaign,
        IWorld world,
        IShipYard ships)
    {
        _saveProgressService = saveProgressService;
        _campaign = campaign;
        _world = world;
        _ships = ships;

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

        // a save that names a ship this build does not have — an older save from before ships were
        // recorded, or one from a build that shipped a hull this one dropped — leaves the player
        // in the starting ship rather than in nothing. A grounded player is a worse outcome than a
        // downgraded one, and pillar 4 says the record survives.
        Player.Award(_ships.Find(save.ShipId) ?? _ships.StartingShip);

        Quests.Restore(save.Quests);

        _autoSave = true;
        IsReady = true;
    }

    /// <inheritdoc />
    public Task Save() => _saveProgressService.Save(SaveGameSerializer.Serialize(Capture()));

    /// <summary>
    /// Returns the session to a freshly registered campaign with the player at the world's start,
    /// flying the ship a new game awards.
    /// </summary>
    /// <remarks>
    /// The award happens here rather than only in <see cref="StartNewGame"/> so that resuming a
    /// save always has a ship to fall back to, and so the player is never left flying nothing.
    /// </remarks>
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
        Player.Award(_ships.StartingShip);
    }

    /// <summary>
    /// Takes a snapshot of the session for persisting.
    /// </summary>
    SaveGame Capture() => new()
    {
        PlayerX = Player.Position.X,
        PlayerY = Player.Position.Y,

        // the identifier, not the ship: what the player owns is persistent record, what it is
        // worth is content the next build is free to change
        ShipId = Player.Ship?.Id ?? "",
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
