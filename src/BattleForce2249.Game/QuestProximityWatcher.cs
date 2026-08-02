using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249;

/// <summary>
/// Watches how close the player is to each quest's markers and drives the quest API accordingly.
/// </summary>
/// <remarks>
/// This is the half of a proximity quest that Pilgrimage deliberately does not own. The quest
/// model declares the rule — a <see cref="QuestTriggerKind.Proximity"/> trigger and a distance —
/// and this measures against the world's markers and calls <see cref="Quest.Start"/> or
/// <see cref="Quest.Complete"/> when it is satisfied. Both are safe to call every frame, so the
/// watcher keeps no memory of what it has already fired.
/// <para>
/// It measures the ground the player covered, not the point they finished a frame on. Sampling a
/// single point fires a trigger only when a frame happens to <em>land</em> inside it, so a frame
/// carrying the ship from just outside one side of a marker to just outside the other fires
/// nothing — the ship flies through a trigger it passed within metres of, and only marker
/// tolerance stands between the game and that happening. `docs/DESIGN.md` pillar 1 calls a trigger
/// a fast ship flies straight through a bug, not a tuning detail, so the ship's speed, the frame
/// length and the trigger distance are not allowed to be what decides it.
/// </para>
/// <para>
/// The watcher still remembers nothing between frames. The player is asked for the journey they
/// flew, because the player is the only thing that can tell flying somewhere apart from being put
/// there — see <see cref="Player.TakeJourney"/>.
/// </para>
/// </remarks>
/// <param name="world">The world holding the marker positions.</param>
public sealed class QuestProximityWatcher(IWorld world)
{
    /// <summary>
    /// Applies each quest's triggers for where the player is standing, with no ground covered.
    /// </summary>
    /// <remarks>
    /// A journey whose ends coincide, which is what standing still is. Kept as its own overload
    /// because a caller that has no journey to offer should not have to say the same position
    /// twice to say so.
    /// </remarks>
    /// <param name="quests">The player's quests.</param>
    /// <param name="playerPosition">Where the player is this frame.</param>
    public void Update(QuestLog quests, Position playerPosition) =>
        Update(quests, playerPosition, playerPosition);

    /// <summary>
    /// Applies each quest's triggers against the ground the player covered. Called once per frame.
    /// </summary>
    /// <param name="quests">The player's quests.</param>
    /// <param name="from">Where the player was when the frame began.</param>
    /// <param name="to">Where they are now.</param>
    public void Update(QuestLog quests, Position from, Position to)
    {
        foreach (QuestMarkers markers in world.QuestMarkers)
        {
            Quest? quest = quests.Find(markers.QuestId);
            if (quest is null)
            {
                // markers for a quest this campaign no longer ships; nothing to drive
                continue;
            }

            QuestDefinition definition = quest.Definition;
            bool startedOnThisJourney = false;

            if (quest.State is QuestState.NotStarted
                && definition.AutoStarts
                && HasFired(definition.Start, from, to, markers.Start))
            {
                quest.Start();
                startedOnThisJourney = true;
            }

            // checked after the start above, so arriving already on the end marker still finishes
            if (quest.State is QuestState.Active
                && HasFired(definition.End, from, to, markers.End)
                && (!startedOnThisJourney || ReachedInOrder(from, to, markers)))
            {
                quest.Complete();
            }
        }
    }

    /// <summary>
    /// Determines whether a trigger is satisfied by how close the journey brought the player to
    /// its marker.
    /// </summary>
    static bool HasFired(QuestTrigger trigger, Position from, Position to, Position marker) =>
        trigger.Kind is QuestTriggerKind.Proximity
        && marker.DistanceToSegment(from, to) <= trigger.Distance;

    /// <summary>
    /// Determines whether a journey reached a quest's end marker no earlier than its start marker.
    /// </summary>
    /// <remarks>
    /// Only asked of a quest that began on this very journey, and it is what keeps a swept trigger
    /// from losing the order the ground was covered in. One frame flown backwards across the whole
    /// field passes both markers, but the player was at the objective before they were at the
    /// beginning — they cannot have finished on that frame something they had not yet started. The
    /// quest starts and stays in progress, and finishes the next time they reach its exit.
    /// <para>
    /// A quest already under way is not asked: the player has been on it since an earlier frame,
    /// so arriving at the objective completes it and which way round they flew through it is not
    /// the quest's business. Markers that coincide, and a frame that covered no ground, both give
    /// every marker the same fraction, so neither is refused by the comparison being inclusive.
    /// </para>
    /// </remarks>
    static bool ReachedInOrder(Position from, Position to, QuestMarkers markers) =>
        markers.End.FractionAlongSegment(from, to) >= markers.Start.FractionAlongSegment(from, to);
}
