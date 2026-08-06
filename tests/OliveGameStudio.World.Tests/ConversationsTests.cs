namespace OliveGameStudio.World.Tests;

/// <summary>
/// Covers reading a conversation: where the player has got to, how it ends, and the two events
/// whatever holds the controls hangs off.
/// </summary>
/// <remarks>
/// The cases worth having are at the ends. Reading past the last line is how a conversation
/// normally finishes, so it has to close rather than leave the player on the last thing said with
/// nothing to press; and backing out has to work on any line, including the first, because the
/// world went on running while they read it.
/// </remarks>
public sealed class ConversationsTests
{
    static Conversation Conversation(params string[] textKeys) =>
        new("miner", textKeys.Select(key => new ConversationLine("MinerName", key)));

    readonly Conversations _conversations = new();

    [Fact]
    public void NobodyIsTalking_ToBeginWith()
    {
        Assert.False(_conversations.IsOpen);
        Assert.Null(_conversations.Open);
    }

    [Fact]
    public void TheLineOfAConversationNobodyIsIn_IsNotAnswered()
    {
        // answering the last line read would let a display go on showing a conversation that ended
        Assert.Throws<InvalidOperationException>(() => _conversations.Current);
    }

    [Fact]
    public void Beginning_OpensAtTheFirstLine()
    {
        Conversation conversation = Conversation("First", "Second");

        _conversations.Begin(conversation);

        Assert.True(_conversations.IsOpen);
        Assert.Same(conversation, _conversations.Open);
        Assert.Equal("First", _conversations.Current.TextKey);
    }

    [Fact]
    public void BeginningRefusesNothingToSay()
    {
        Assert.Throws<ArgumentNullException>(() => _conversations.Begin(null!));
    }

    [Fact]
    public void BeginningRefusesASecondConversation()
    {
        // it would abandon the first half-read, silently. Whoever routes input already knows which
        // of the two states the player is in, so this can only be a mistake.
        _conversations.Begin(Conversation("First"));

        Assert.Throws<InvalidOperationException>(() => _conversations.Begin(Conversation("Other")));
    }

    [Fact]
    public void Advancing_ReadsTheNextLine()
    {
        _conversations.Begin(Conversation("First", "Second"));

        _conversations.Advance();

        Assert.Equal("Second", _conversations.Current.TextKey);
    }

    [Fact]
    public void AdvancingPastTheLastLine_Closes()
    {
        _conversations.Begin(Conversation("First", "Second"));

        _conversations.Advance();
        _conversations.Advance();

        Assert.False(_conversations.IsOpen);
    }

    [Fact]
    public void AdvancingWhenNobodyIsTalking_DoesNothing()
    {
        // a player can close a conversation with the advance key already held; the release then
        // arrives after the conversation has gone. That is being quick, not being wrong.
        _conversations.Advance();

        Assert.False(_conversations.IsOpen);
    }

    [Fact]
    public void Closing_EndsItWhereverThePlayerHadGotTo()
    {
        _conversations.Begin(Conversation("First", "Second", "Third"));
        _conversations.Advance();

        _conversations.Close();

        Assert.False(_conversations.IsOpen);
    }

    [Fact]
    public void ClosingWhenNobodyIsTalking_DoesNothing()
    {
        // so the way out can be offered every frame without a caller having to ask first
        _conversations.Close();

        Assert.False(_conversations.IsOpen);
    }

    [Fact]
    public void AConversationReadBefore_OpensAtTheFirstLineAgain()
    {
        Conversation conversation = Conversation("First", "Second");
        _conversations.Begin(conversation);
        _conversations.Advance();
        _conversations.Close();

        _conversations.Begin(conversation);

        Assert.Equal("First", _conversations.Current.TextKey);
    }

    [Fact]
    public void Beginning_SaysSo()
    {
        int opened = 0;
        _conversations.Opened += (_, _) => opened++;

        _conversations.Begin(Conversation("First"));

        Assert.Equal(1, opened);
    }

    [Fact]
    public void BothWaysOut_SaySoTheSameWay()
    {
        // whatever took the controls gives them back on this one event, so being read to the end
        // and being backed out of cannot be handled differently by accident
        int closed = 0;
        _conversations.Closed += (_, _) => closed++;

        _conversations.Begin(Conversation("Only"));
        _conversations.Advance();

        _conversations.Begin(Conversation("First", "Second"));
        _conversations.Close();

        Assert.Equal(2, closed);
    }

    [Fact]
    public void ClosingWhenNobodyIsTalking_SaysNothing()
    {
        int closed = 0;
        _conversations.Closed += (_, _) => closed++;

        _conversations.Close();

        Assert.Equal(0, closed);
    }

    [Fact]
    public void TheConversationIsAlreadyGone_WhenItSaysItClosed()
    {
        // whatever hands the controls back reads this, and a listener told "closed" while the
        // conversation still reported itself open would hand them back to a screen still in one
        bool openWhenTold = true;
        _conversations.Closed += (_, _) => openWhenTold = _conversations.IsOpen;

        _conversations.Begin(Conversation("Only"));
        _conversations.Close();

        Assert.False(openWhenTold);
    }
}
