using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// The region that ships with the game, held to being readable and to still holding what it was
/// imported with.
/// </summary>
/// <remarks>
/// A region is authored content edited by a tool, not code, so nothing else would notice it going
/// wrong: <see cref="SceneSerializer"/> is deliberately forgiving and hands back an empty region
/// rather than failing, which is right for the player and silent for everybody else. These are
/// what turn "the debris field is empty" from something discovered by flying around in it into a
/// failing test.
/// </remarks>
public sealed class DebrisFieldRegionTests
{
    static SceneDefinition Region()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Regions", "debris-field.json");
        return SceneSerializer.Deserialize(File.ReadAllText(path));
    }

    [Fact]
    public void TheDebrisField_Ships_AndCanBeRead()
    {
        SceneDefinition region = Region();

        Assert.Equal("debris-field", region.Id);
        Assert.NotEmpty(region.Bodies);
        Assert.NotEmpty(region.Markers);
    }

    [Fact]
    public void EveryBody_NamesASprite()
    {
        // A body with no sprite draws nothing and says nothing about why.
        SceneDefinition region = Region();

        Assert.All(region.Bodies, body => Assert.False(string.IsNullOrWhiteSpace(body.Sprite)));
    }

    [Fact]
    public void EveryQuestZone_SaysWhichQuestItBelongsTo()
    {
        // The whole reason the format carries a reference. The import recovered these from names
        // like "Q3_START_ZONE"; if a later edit drops one, the zone still loads, still triggers
        // nothing, and looks perfectly fine in the editor.
        SceneDefinition region = Region();

        SceneMarker[] zones = [.. region.Markers.Where(m => m.Kind is "QuestStart" or "QuestComplete")];

        Assert.NotEmpty(zones);
        Assert.All(zones, zone => Assert.StartsWith("quest-", zone.Reference, StringComparison.Ordinal));
    }

    [Fact]
    public void EveryTriggerZone_HasADistanceToTriggerAt()
    {
        // The number the Unity export lost. A zone with no radius is a zone the player can fly
        // straight through, and the only symptom is a quest that never starts.
        SceneDefinition region = Region();

        SceneMarker[] zones =
            [.. region.Markers.Where(m => m.Kind is "QuestStart" or "QuestComplete" or "Save")];

        Assert.All(zones, zone => Assert.True(zone.Radius > 0, $"{zone.Name} has no radius"));
    }

    [Fact]
    public void EveryStartZone_HasAMatchingCompleteZone()
    {
        // A quest that can be started and never finished is a soft lock, and it is invisible in the
        // data — both halves look correct on their own.
        SceneDefinition region = Region();

        HashSet<string> started =
            [.. region.Markers.Where(m => m.Kind == "QuestStart").Select(m => m.Reference)];
        HashSet<string> completed =
            [.. region.Markers.Where(m => m.Kind == "QuestComplete").Select(m => m.Reference)];

        Assert.Equal(started, completed);
    }

    [Fact]
    public void NothingInTheField_IsPartOfTheShip()
    {
        // The ship's engine glow was authored into the scenery, on the layer the ship itself
        // occupies and at the world origin — which is where the ship starts, so it looked correct
        // until the player flew and left its own engines burning at the spawn point. The ship
        // draws its own glow now (see ShipEngineGlowTests); the field must not draw a second one,
        // and a re-import that brought the bodies back would put it there again with nothing else
        // noticing.
        SceneDefinition region = Region();

        Assert.DoesNotContain(region.Bodies, body => body.Layer == "Characters");
    }

    [Fact]
    public void TheRocksAndAsteroidsAreSolid_AndNothingElseIs()
    {
        // Only what the ship should actually collide with — a rock, or an asteroid, wherever it
        // stands in the field. Every asteroid1 counts: an earlier pass solid-marked only the ones
        // on the Environment layer, reasoning the rest were a background picture — a play session
        // found one sitting in the open, drawn exactly like a real obstacle, with nothing behind
        // it to make it read as backdrop. asteroid2 is the same story a level up: it was not in
        // RegionObstacles' known sprites at all, and its one instance sits at the origin — the
        // ship's own spawn point — where a play session found it drawn solid with nothing to stop
        // the ship flying straight through. Glow and hull plating stay pure scenery.
        SceneDefinition region = Region();

        foreach (SceneBody body in region.Bodies)
        {
            bool expectedSolid = body.Sprite is "rock1" or "rock2" or "rock3" or "asteroid1" or "asteroid2";

            Assert.True(
                body.Solid == expectedSolid,
                $"{body.Name} ({body.Sprite}, layer {body.Layer}): expected Solid={expectedSolid}, was {body.Solid}");
        }

        Assert.Equal(119, region.Bodies.Count(body => body.Solid));
    }
}
