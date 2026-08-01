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
            // The file was read; nothing playable came out of it. Set it aside before starting
            // over, because StartNewGame writes immediately and would otherwise destroy the only
            // copy — and the refusal may be wrong. Every rule on that boundary is a judgement
            // about a file someone's campaign is in, and until now getting one slightly too
            // strict cost the player the campaign with no way back.
            if (await TrySetAside(content))
            {
                await StartNewGame();
                return;
            }

            // It could not be moved out of the way. Writing anyway would destroy it for exactly
            // the reason the copy was being made, so writes are held back instead — the same
            // answer given above for a save that could not be read. The player still gets a game;
            // it just does not persist over the one that could not be rescued.
            Reset();
            IsReady = true;
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
    /// Sets a refused save aside so it survives the new game about to be written over it,
    /// recording a storage failure rather than raising it.
    /// </summary>
    /// <param name="content">
    /// What was read. Nothing is set aside when there was no save or it was blank: a first launch
    /// is not a refusal, and keeping an empty file would suggest something was lost when nothing
    /// was.
    /// </param>
    /// <returns><c>true</c> when the new game is free to write.</returns>
    async Task<bool> TrySetAside(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return true;
        }

        try
        {
            await _saveProgressService.SetAside();
            SaveError = null;
            return true;
        }
        catch (Exception error) when (IsStorageFailure(error))
        {
            SaveError = error;
            return false;
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
            PendingSave = TrySave();
        }
    }
}
