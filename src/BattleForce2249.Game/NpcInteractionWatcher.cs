using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// Watches for the player asking to talk to somebody, and for them asking to stop.
/// </summary>
/// <remarks>
/// <para>
/// The same half of the job that <see cref="QuestProximityWatcher"/> does for quests. The engine
/// owns what a conversation <em>is</em> and how far through one the player has read; this knows
/// where everybody is standing, so it is the side that can answer "is there anybody here to talk
/// to". Nothing in <see cref="Conversations"/> measures a distance, exactly as nothing in
/// Pilgrimage does.
/// </para>
/// <para>
/// It also holds the controls. A conversation on screen takes the input because the engine's
/// routing rule is already <em>UI first, ship second</em> — so a talk that focuses something is a
/// talk the player cannot fly during, with no pause state and no second way for the ship to be
/// stationary. Drag brings the ship to rest by itself, which is the physics doing what it always
/// does rather than anything this had to arrange.
/// </para>
/// </remarks>
public sealed class NpcInteractionWatcher
{
    /// <summary>
    /// The button a conversation holds the controls with.
    /// </summary>
    /// <remarks>
    /// Not a button the player is shown. It exists because focus is what routing asks about, and
    /// a focused element is how anything on screen says the input is currently its own. Pressing
    /// and releasing it — which is what the confirm key does while it holds focus — is how the
    /// player turns the page.
    /// </remarks>
    public const string TalkButtonName = "TALK";

    readonly IWorld _world;

    readonly Conversations _conversations;

    readonly IUIController _ui;

    readonly Button _talk = new(TalkButtonName);

    bool _talkIsKnown;

    /// <summary>
    /// Watches <paramref name="world"/>'s NPCs on behalf of <paramref name="conversations"/>,
    /// holding the controls through <paramref name="ui"/> while one of them is talking.
    /// </summary>
    /// <param name="world">The world the NPCs stand in.</param>
    /// <param name="conversations">The conversation the player is in, if they are in one.</param>
    /// <param name="ui">The user interface a conversation takes the controls through.</param>
    /// <exception cref="ArgumentNullException">Any of them is <c>null</c>.</exception>
    public NpcInteractionWatcher(IWorld world, Conversations conversations, IUIController ui)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(conversations);
        ArgumentNullException.ThrowIfNull(ui);

        _world = world;
        _conversations = conversations;
        _ui = ui;

        // Subscribed rather than done beside each call, because a conversation ends by two routes
        // — read to the end, or backed out of — and only one of them passes through this class.
        // Giving the controls back has to happen on both, so it is hung off the one event both
        // routes raise.
        _conversations.Opened += (_, _) => Take();
        _conversations.Closed += (_, _) => _ui.UnFocus();
    }

    /// <summary>
    /// Applies what the player asked for this frame. Called once a frame, after the ship has flown.
    /// </summary>
    /// <param name="playerPosition">Where the player is now the frame has ended.</param>
    /// <param name="interact">Whether they asked to talk to somebody on this frame.</param>
    /// <param name="cancel">Whether they asked to be out of the conversation on this frame.</param>
    /// <remarks>
    /// Backing out is answered before anything else and on the frame it was asked, because the
    /// world did not stop for the conversation: a player who opened one at the wrong moment is
    /// sitting still in an armed world, and the way out cannot be one frame behind them.
    /// </remarks>
    public void Update(Position playerPosition, bool interact, bool cancel)
    {
        if (_conversations.IsOpen)
        {
            if (cancel)
            {
                _conversations.Close();
            }

            return;
        }

        if (!interact)
        {
            return;
        }

        if (NearestWithinReach(playerPosition) is { } npc)
        {
            _conversations.Begin(npc.Conversation);
        }
    }

    /// <summary>
    /// The closest NPC the player is near enough to talk to, or <c>null</c> if there is nobody.
    /// </summary>
    /// <remarks>
    /// Nearest rather than first, because two NPCs standing near each other is a scene content is
    /// allowed to build and the player expects to talk to the one they flew up to. Each is measured
    /// against its own reach — how close you have to get is a fact about them — so the winner is
    /// the nearest of those in reach rather than the nearest overall, which could be somebody
    /// standing just outside their own.
    /// </remarks>
    Npc? NearestWithinReach(Position playerPosition)
    {
        Npc? nearest = null;
        double nearestDistance = double.PositiveInfinity;

        foreach (Npc npc in _world.Npcs)
        {
            if (!npc.IsWithinReach(playerPosition))
            {
                continue;
            }

            double distance = npc.Position.DistanceTo(playerPosition);

            // strictly nearer, so two NPCs at exactly the same distance resolve to the one the
            // world lists first rather than to whichever the loop happened to see last
            if (distance < nearestDistance)
            {
                nearest = npc;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Takes the controls for the conversation that has just opened.
    /// </summary>
    /// <remarks>
    /// The button is added on the first conversation rather than in the constructor. Adding a
    /// button adopts the focus when nothing else holds it, and this class is built with the rest of
    /// the game — long before the player is anywhere near somebody to talk to — so a button added
    /// then would take the controls away from a ship nobody had finished starting. There is no way
    /// to remove one, so it is added once and focused from then on.
    /// </remarks>
    void Take()
    {
        if (!_talkIsKnown)
        {
            _ui.Add(_talk);
            _ui.OnReleased(_talk, _conversations.Advance);
            _talkIsKnown = true;
        }

        _ui.FocusOn(_talk);
    }
}
