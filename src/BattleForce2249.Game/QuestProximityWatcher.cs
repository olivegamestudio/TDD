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
///
/// It applies the quest's <see cref="QuestCondition"/>s at the same time, through
/// <see cref="QuestConditions"/>, for the same reason it applies its triggers: conditions are data
/// the library states and the game answers. A trigger that fires while the gate is shut moves
/// nothing.
///
/// It measures the journey the player made this frame, not the point they finished it at. A
/// trigger a ship flies straight through is a bug against pillar 1 of <c>docs/DESIGN.md</c>
/// rather than a tuning detail, and sampling one point a frame makes that trap a property of the
/// frame rate: whether a marker fires depends on where the frames happened to fall. Sweeping the
/// journey takes the frame rate out of it, and leaves marker tolerance as content's business
/// rather than as the only thing preventing a missed quest.
///
/// Keeping no memory is why the journey is passed in rather than remembered here. A watcher that
/// held the previous position would sweep the line between where the player was and wherever they
/// are next — including across a resumed save, where the player does not travel between the two
/// but is put down somewhere else entirely, and every marker on the line between would fire.
/// </remarks>
/// <param name="world">The world holding the marker positions.</param>
public sealed class QuestProximityWatcher(IWorld world)
{
    /// <summary>
    /// Applies each quest's triggers for a player who did not move this frame.
    /// </summary>
    /// <remarks>
    /// A journey of no length, and the same measure the watcher has always applied: the player is
    /// only ever measured against where they are. Callers that fly the player should pass where
    /// the frame began as well, so a marker cannot be stepped over.
    /// </remarks>
    /// <param name="character">The character being played, whose quests these are.</param>
    /// <param name="playerPosition">Where the player is this frame.</param>
    public void Update(Character character, Position playerPosition) =>
        Update(character, playerPosition, playerPosition);

    /// <summary>
    /// Applies each quest's triggers for the journey the player made this frame. Called once per
    /// frame, after the ship has flown.
    /// </summary>
    /// <remarks>
    /// A trigger fires on the closest the player came to its marker at any point along the way,
    /// so a frame of any length fires the markers that frame passed. A proximity trigger has no
    /// sense of direction — it is a disc round a marker, and a player who reaches it flying
    /// backwards has still reached it — so the journey is measured, not the heading.
    /// </remarks>
    /// <param name="character">The character being played, whose quests these are.</param>
    /// <param name="from">Where the player was when the frame began.</param>
    /// <param name="to">Where the player is now the frame has ended.</param>
    /// <exception cref="ArgumentNullException"><paramref name="character"/> is <c>null</c>.</exception>
    public void Update(Character character, Position from, Position to)
    {
        ArgumentNullException.ThrowIfNull(character);

        QuestLog quests = character.Quests;

        foreach (QuestMarkers markers in world.QuestMarkers)
        {
            Quest? quest = quests.Find(markers.QuestId);
            if (quest is null)
            {
                // markers for a quest this campaign no longer ships; nothing to drive
                continue;
            }

            QuestDefinition definition = quest.Definition;

            // The conditions are asked before the distance is measured, and both have to be
            // satisfied on the same frame. A quest whose gate is shut is not "arrived at and
            // remembered": the player can fly over its marker at level three and nothing happens,
            // and it begins when they come back at level five — which is what makes a condition a
            // state that holds rather than a second kind of trigger that fired once.
            if (quest.State is QuestState.NotStarted
                && definition.AutoStarts
                && QuestConditions.Hold(definition.StartConditions, character)
                && HasFired(definition.Start, from, to, markers.Start))
            {
                quest.Start();
            }

            // checked after the start above, so a frame that swept both markers — arriving on the
            // end marker, or flying through the whole quest at once — still finishes
            if (quest.State is QuestState.Active
                && QuestConditions.Hold(definition.EndConditions, character)
                && HasFired(definition.End, from, to, markers.End))
            {
                quest.Complete();
            }
        }
    }

    /// <summary>
    /// Determines whether a trigger is satisfied by how close the player's journey came to its
    /// marker.
    /// </summary>
    static bool HasFired(QuestTrigger trigger, Position from, Position to, Position marker) =>
        trigger.Kind is QuestTriggerKind.Proximity
        && marker.DistanceToSegment(from, to) <= trigger.Distance;
}
