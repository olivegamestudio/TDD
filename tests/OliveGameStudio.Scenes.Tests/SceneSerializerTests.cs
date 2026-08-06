using OliveGameStudio;

namespace OliveGameStudio.Scenes.Tests;

/// <summary>
/// The contract between the game that streams regions and the editor that writes them. What these
/// hold is that a region survives the round trip intact, and that a file which does not survive it
/// leaves the player somewhere empty rather than stopping the game.
/// </summary>
public sealed class SceneSerializerTests
{
    static SceneDefinition Sample() => new(
        "debris-field",
        [
            new SceneBody("rock1", "rock1", 1.5, -2.5, 30, 1, 1, "Environment", 0),
            new SceneBody("glow", "glow", 0, 0, 0, 3, 4, "Default", 1),
        ],
        [
            new SceneMarker("Quest 1 start", "QuestStart", "quest-1", 0, 0, 0, 25),
            new SceneMarker("Save point", "Save", "", 0, 1, 0, 8),
        ]);

    [Fact]
    public void ARegion_SurvivesTheRoundTrip()
    {
        SceneDefinition scene = Sample();

        SceneDefinition read = SceneSerializer.Deserialize(SceneSerializer.Serialize(scene));

        Assert.Equal(scene, read);
    }

    [Fact]
    public void TheRadius_SurvivesTheRoundTrip()
    {
        // The number the previous build's export lost. Its zones were circles in the editor and
        // bare points in the file, so the distance someone had authored existed nowhere afterwards
        // and every trigger silently fell back to whatever the code assumed.
        SceneDefinition read = SceneSerializer.Deserialize(SceneSerializer.Serialize(Sample()));

        Assert.Equal(25, read.Markers[0].Radius);
    }

    [Fact]
    public void TheQuestReference_SurvivesTheRoundTrip()
    {
        // Stated outright rather than parsed back out of a name like "Q1_START_ZONE", where a typo
        // detaches a zone from its quest and nothing reports it.
        SceneDefinition read = SceneSerializer.Deserialize(SceneSerializer.Serialize(Sample()));

        Assert.Equal("quest-1", read.Markers[0].Reference);
        Assert.Equal("QuestStart", read.Markers[0].Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{ \"id\": ")]
    [InlineData("[]")]
    [InlineData("null")]
    public void AnUnreadableRegion_ComesBackEmpty(string? json)
    {
        // A region that cannot be understood should leave the player somewhere empty rather than
        // stop the game, which is the same answer a damaged save already gets.
        SceneDefinition read = SceneSerializer.Deserialize(json);

        Assert.Equal(SceneDefinition.Empty, read);
    }

    [Fact]
    public void Serialize_RefusesNothingToWrite()
    {
        // Writing is the editor's side of the contract. A file written wrong is one every later
        // read has to cope with, so this end is not forgiving.
        Assert.Throws<ArgumentNullException>(() => SceneSerializer.Serialize(null!));
    }

    [Fact]
    public void PropertyNames_AreCamelCase_SoAnAuthoredFileReadsAsOneDocument()
    {
        // The rest of this project's JSON is camel case — the save game, the language files. A
        // second convention in a third format is a thing for everyone to remember.
        string json = SceneSerializer.Serialize(Sample());

        Assert.Contains("\"rotationDegrees\"", json);
        Assert.Contains("\"sprite\"", json);
        Assert.DoesNotContain("\"RotationDegrees\"", json);
    }

    [Fact]
    public void TwoRegionsHoldingTheSameThing_AreEqual()
    {
        // A record compares its members with object.Equals, and a list's is reference equality — so
        // without saying otherwise, a region read back from its own file compares unequal to the
        // one it came from. That is precisely the comparison an editor makes to decide whether
        // anything has changed since it last saved.
        Assert.Equal(Sample(), Sample());
        Assert.Equal(Sample().GetHashCode(), Sample().GetHashCode());
    }

    [Fact]
    public void TwoRegionsDifferingInOneEntry_AreNotEqual()
    {
        SceneDefinition moved = Sample() with
        {
            Markers = [Sample().Markers[0] with { Radius = 26 }, Sample().Markers[1]],
        };

        Assert.NotEqual(Sample(), moved);
    }

    [Fact]
    public void AnEmptyRegion_IsStillARegion()
    {
        SceneDefinition read = SceneSerializer.Deserialize(SceneSerializer.Serialize(SceneDefinition.Empty));

        Assert.Empty(read.Bodies);
        Assert.Empty(read.Markers);
    }
}
