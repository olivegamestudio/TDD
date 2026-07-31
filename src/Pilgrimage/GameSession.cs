using OliveGameStudio;

namespace Pilgrimage;

/// <summary>
/// The default <see cref="IGameSession"/>. It holds the player and the quest log, drives quests
/// from the player's position each frame, and persists both through the engine's save progress
/// service whenever a quest starts or completes.
/// </summary>
public sealed class GameSession : IGameSession
{
    readonly ISaveProgressService _saveProgressService;
    readonly ICampaign _campaign;

    /// <summary>
    /// Whether a quest changing state should write a save. It is turned off while a game is being
    /// set up, so starting or resuming writes at most one save rather than one per quest.
    /// </summary>
    bool _autoSave;

    /// <summary>
    /// Creates a session for the given campaign, persisting to the given save progress service.
    /// </summary>
    /// <param name="saveProgressService">The service the save game is written to and read from.</param>
    /// <param name="campaign">The content the session plays: the start position and the quests.</param>
    public GameSession(ISaveProgressService saveProgressService, ICampaign campaign)
    {
        _saveProgressService = saveProgressService;
        _campaign = campaign;

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

        // one update at the start position, so a quest whose start marker is there begins
        // without the player having to do anything
        Quests.Update(Player.Position);

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

        foreach (QuestProgress progress in save.Quests)
        {
            // a quest the save knows about but this build no longer ships is simply dropped
            Quests.Find(progress.QuestId)?.Restore(progress.State);
        }

        _autoSave = true;
        IsReady = true;
    }

    /// <inheritdoc />
    public void Update(TimeSpan frameTime)
    {
        if (!IsReady)
        {
            return;
        }

        Quests.Update(Player.Position);
    }

    /// <inheritdoc />
    public Task Save() => _saveProgressService.Save(SaveGameSerializer.Serialize(Capture()));

    /// <summary>
    /// Returns the session to a freshly registered campaign with the player at its start position.
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

        Player.MoveTo(_campaign.PlayerStart);
    }

    /// <summary>
    /// Takes a snapshot of the session for persisting.
    /// </summary>
    SaveGame Capture() => new()
    {
        PlayerX = Player.Position.X,
        PlayerY = Player.Position.Y,
        Quests = [.. Quests.Quests.Select(quest => new QuestProgress(quest.Id, quest.State))],
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
