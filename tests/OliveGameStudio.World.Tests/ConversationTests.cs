namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers what a conversation is allowed to be. Everything here is a mistake an author makes once
/// and then hunts for: a line with nothing in it, or a conversation with no lines, reaches the
/// player as a panel that will not go away rather than as anything that looks like a bug.
/// </summary>
public sealed class ConversationTests
{
    static ConversationLine Line(string text = "MinerGreeting") => new("MinerName", text);

    [Fact]
    public void ALine_KeepsWhoSaysItAndWhatIsSaid()
    {
        ConversationLine line = new("MinerName", "MinerGreeting");

        Assert.Equal("MinerName", line.SpeakerKey);
        Assert.Equal("MinerGreeting", line.TextKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ALineRefusesASpeakerThatIsNobody(string? speakerKey)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ConversationLine(speakerKey!, "MinerGreeting"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ALineRefusesSayingNothing(string? textKey)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ConversationLine("MinerName", textKey!));
    }

    [Fact]
    public void AConversationKeepsItsLinesInOrder()
    {
        Conversation conversation = new("miner", [Line("First"), Line("Second")]);

        Assert.Equal("miner", conversation.Id);
        Assert.Equal(["First", "Second"], conversation.Lines.Select(line => line.TextKey));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AConversationRefusesNoIdentity(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Conversation(id!, [Line()]));
    }

    [Fact]
    public void AConversationRefusesNoLinesAtAll()
    {
        Assert.Throws<ArgumentNullException>(() => new Conversation("miner", null!));
    }

    [Fact]
    public void AConversationRefusesSayingNothing()
    {
        // it would open and close on the same press, which reads as an NPC that does not work
        Assert.Throws<ArgumentException>(() => new Conversation("miner", []));
    }

    [Fact]
    public void AConversationRefusesALineThatIsNotThere()
    {
        Assert.Throws<ArgumentException>(() => new Conversation("miner", [Line(), null!]));
    }

    [Fact]
    public void AConversationCannotGainALineAfterItWasChecked()
    {
        // the list the author passed stays in their hands; a conversation the player is part way
        // through must not be able to change under them
        List<ConversationLine> lines = [Line("First")];
        Conversation conversation = new("miner", lines);

        lines.Add(Line("Second"));

        Assert.Single(conversation.Lines);
    }
}
