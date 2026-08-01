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
    public Exception? SaveError { get; private set; }

    /// <inheritdoc />
    public bool IsSavingProgress => _autoSave;

    /// <inheritdoc />
    public async Task StartNewGame()
    {
        Reset();

        _autoSave = true;
        IsReady = true;

        await TrySave();
    }

    /// <inheritdoc />
    public async Task Continue()
    {
        SaveError = null;

        string? content;
        try
        {
            content = await _saveProgressService.Load();
        }
        catch (Exception error) when (IsStorageFailure(error))
        {
            // The save was not readable this time, which does not mean it is not there. Give the
            // player a game to play, but hold every write back: overwriting a save that was only
            // locked for a moment loses a game that was never actually lost.
            SaveError = error;

            Reset();
            IsReady = true;
            return;
        }

        SaveGame? save = SaveGameSerializer.Deserialize(content);
        if (save is null)
        {
            await StartNewGame();
            return;
        }

        Reset();
        Player.MoveTo(new Position(save.PlayerX, save.PlayerY));

        // after Reset, which awards the starting ship, so a save that names one this build does
        // not have — written before ships were recorded, or by a build that shipped a hull this
        // one dropped — leaves the player downgraded rather than grounded. Pillar 4 asks that the
        // record survive; a player who cannot fly has lost more than one who flies something worse.
        Player.Award(_ships.Find(save.ShipId) ?? _ships.StartingShip);

        Quests.Restore(save.Quests);

        _autoSave = true;
        IsReady = true;
    }

    /// <inheritdoc />
    public Task Save() => _saveProgressService.Save(SaveGameSerializer.Serialize(Capture()));

    /// <summary>
    /// Saves, recording a storage failure rather than raising it. Every automatic save runs through
    /// here, because the alternative is a write failing on a background task nobody is holding —
    /// which is how a game ends up stopped with nothing said. Callers who want the failure raised
    /// use <see cref="Save"/>.
    /// </summary>
    async Task TrySave()
    {
        try
        {
            await Save();
            SaveError = null;
        }
        catch (Exception error) when (IsStorageFailure(error))
        {
            // Left saving on: the file may be free again by the next quest, and a game that gives
            // up on saving after one bad moment loses far more than it protects.
            SaveError = error;
        }
    }

    /// <summary>
    /// Whether an exception is the storage getting in the way rather than a defect. These two are
    /// what a file that is missing, locked, on a full disk or barred by permissions throws; catching
    /// wider than this would bury real bugs behind a "could not save" message.
    /// </summary>
    static bool IsStorageFailure(Exception error) =>
        error is IOException or UnauthorizedAccessException;

    /// <summary>
    /// Returns the session to a freshly registered campaign with the player at the world's start,
    /// flying the ship a new game awards.
    /// </summary>
    /// <remarks>
    /// The award happens here rather than only in <see cref="StartNewGame"/> so that every path
    /// out of this class leaves the player in a ship: a resumed save has something to fall back to,
    /// and a save that could not be read at all still gives the player something to fly.
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
            PendingSave = TrySave();
        }
    }
}
