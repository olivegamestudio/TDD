using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the half of a conversation the engine deliberately does not own: whether there is
/// anybody here to talk to, and who holds the controls while the player talks to them.
/// </summary>
/// <remarks>
/// The interesting cases are the ones a player produces without meaning to — pressing interact
/// with nobody about, pressing it again during a conversation, and backing out on the first line
/// because the world they left running has started shooting at them.
/// </remarks>
public sealed class NpcInteractionWatcherTests
{
    /// <summary>
    /// A world holding whoever the test put in it and no quest markers: this is about people, not
    /// about places.
    /// </summary>
    sealed class TestWorld(params Npc[] npcs) : IWorld
    {
        public Position PlayerStart => Position.Origin;

        public IReadOnlyList<QuestMarkers> QuestMarkers { get; } = [];

        public IReadOnlyList<Npc> Npcs { get; } = npcs;

        public Position Introduce(Ship ship, string location) => PlayerStart;
    }

    static Conversation Conversation(string id, params string[] textKeys) =>
        new(id, textKeys.Select(key => new ConversationLine("SpeakerName", key)));

    static Npc NpcAt(Position position, string id = "miner", double range = 50) =>
        new(id, position, Conversation(id, "First", "Second"), range);

    readonly Conversations _conversations = new();

    readonly UIController _ui = new();

    NpcInteractionWatcher WatcherFor(params Npc[] npcs) =>
        new(new TestWorld(npcs), _conversations, _ui);

    [Fact]
    public void PressingInteractNextToSomebody_OpensTheirConversation()
    {
        NpcInteractionWatcher watcher = WatcherFor(NpcAt(new Position(0, 10)));

        watcher.Update(Position.Origin, interact: true, cancel: false);

        Assert.True(_conversations.IsOpen);
        Assert.Equal("First", _conversations.Current.TextKey);
    }

    [Fact]
    public void FlyingPastWithoutPressing_OpensNothing()
    {
        // a conversation is something the player asks for; nothing is triggered by arriving
        NpcInteractionWatcher watcher = WatcherFor(NpcAt(new Position(0, 10)));

        watcher.Update(Position.Origin, interact: false, cancel: false);

        Assert.False(_conversations.IsOpen);
    }

    [Fact]
    public void PressingInteractOutOfRange_OpensNothing()
    {
        NpcInteractionWatcher watcher = WatcherFor(NpcAt(new Position(0, 51), range: 50));

        watcher.Update(Position.Origin, interact: true, cancel: false);

        Assert.False(_conversations.IsOpen);
    }

    [Fact]
    public void PressingInteractWithNobodyInTheWorld_OpensNothing()
    {
        NpcInteractionWatcher watcher = WatcherFor();

        watcher.Update(Position.Origin, interact: true, cancel: false);

        Assert.False(_conversations.IsOpen);
    }

    [Fact]
    public void TheNearestOfTwo_IsTheOneTalkedTo()
    {
        // two NPCs standing near each other is a scene content is allowed to build, and the player
        // expects to talk to the one they flew up to
        NpcInteractionWatcher watcher = WatcherFor(
            NpcAt(new Position(0, 40), id: "far"),
            NpcAt(new Position(0, 5), id: "near"));

        watcher.Update(Position.Origin, interact: true, cancel: false);

        Assert.Equal("near", _conversations.Open?.Id);
    }

    [Fact]
    public void SomebodyNearerButOutOfTheirOwnRange_IsNotTheOneTalkedTo()
    {
        // how close you have to get is a fact about them, so the winner is the nearest of those in
        // reach rather than the nearest overall
        NpcInteractionWatcher watcher = WatcherFor(
            NpcAt(new Position(0, 5), id: "whispering", range: 1),
            NpcAt(new Position(0, 40), id: "shouting", range: 100));

        watcher.Update(Position.Origin, interact: true, cancel: false);

        Assert.Equal("shouting", _conversations.Open?.Id);
    }

    [Fact]
    public void AConversationTakesTheControls()
    {
        // the input router asks about focus, so a focused conversation is one the player cannot
        // fly during — no pause state, and no second way for the ship to be stationary
        NpcInteractionWatcher watcher = WatcherFor(NpcAt(new Position(0, 10)));

        watcher.Update(Position.Origin, interact: true, cancel: false);

        Assert.True(_ui.HasFocus);
    }

    [Fact]
    public void BeforeAnybodyHasBeenTalkedTo_TheControlsAreTheShips()
    {
        // the button a conversation holds the controls with is added on the first conversation:
        // adding one adopts the focus when nothing holds it, and this is built with the rest of
        // the game — long before the player is near anybody
        WatcherFor(NpcAt(new Position(0, 10)));

        Assert.False(_ui.HasFocus);
    }

    [Fact]
    public void ReadingToTheEnd_GivesTheControlsBack()
    {
        NpcInteractionWatcher watcher = WatcherFor(NpcAt(new Position(0, 10)));
        watcher.Update(Position.Origin, interact: true, cancel: false);

        _conversations.Advance();
        _conversations.Advance();

        Assert.False(_conversations.IsOpen);
        Assert.False(_ui.HasFocus);
    }

    [Fact]
    public void BackingOut_ClosesItOnTheFrameItWasAskedFor()
    {
        // the world kept running while they read, so the way out cannot be a frame behind them
        NpcInteractionWatcher watcher = WatcherFor(NpcAt(new Position(0, 10)));
        watcher.Update(Position.Origin, interact: true, cancel: false);

        watcher.Update(Position.Origin, interact: false, cancel: true);

        Assert.False(_conversations.IsOpen);
        Assert.False(_ui.HasFocus);
    }

    [Fact]
    public void BackingOutOnTheFirstLine_Works()
    {
        NpcInteractionWatcher watcher = WatcherFor(
            new Npc("miner", new Position(0, 10), Conversation("miner", "A", "B", "C"), 50));
        watcher.Update(Position.Origin, interact: true, cancel: false);

        watcher.Update(Position.Origin, interact: false, cancel: true);

        Assert.False(_conversations.IsOpen);
    }

    [Fact]
    public void PressingInteractDuringAConversation_DoesNotOpenASecond()
    {
        NpcInteractionWatcher watcher = WatcherFor(NpcAt(new Position(0, 10), id: "first"));
        watcher.Update(Position.Origin, interact: true, cancel: false);

        watcher.Update(Position.Origin, interact: true, cancel: false);

        Assert.Equal("first", _conversations.Open?.Id);
    }

    [Fact]
    public void PressingTheConfirmButton_TurnsThePage()
    {
        // this is what the confirm key does while a conversation holds the focus: the router
        // presses and releases the focused button, and releasing it advances the line
        NpcInteractionWatcher watcher = WatcherFor(NpcAt(new Position(0, 10)));
        watcher.Update(Position.Origin, interact: true, cancel: false);

        _ui.Press();
        _ui.Release();

        Assert.Equal("Second", _conversations.Current.TextKey);
    }

    [Fact]
    public void PressingThroughTheLastLine_EndsTheConversation()
    {
        NpcInteractionWatcher watcher = WatcherFor(NpcAt(new Position(0, 10)));
        watcher.Update(Position.Origin, interact: true, cancel: false);

        _ui.Press();
        _ui.Release();
        _ui.Press();
        _ui.Release();

        Assert.False(_conversations.IsOpen);
        Assert.False(_ui.HasFocus);
    }

    [Fact]
    public void TalkingToSomebodyTwice_StartsTheirConversationAgain()
    {
        // and the second one takes the controls the same way, through the button added the first
        // time — there is no way to remove one, so it is added once and focused from then on
        NpcInteractionWatcher watcher = WatcherFor(NpcAt(new Position(0, 10)));
        watcher.Update(Position.Origin, interact: true, cancel: false);
        watcher.Update(Position.Origin, interact: false, cancel: true);

        watcher.Update(Position.Origin, interact: true, cancel: false);

        Assert.True(_conversations.IsOpen);
        Assert.True(_ui.HasFocus);
        Assert.Equal("First", _conversations.Current.TextKey);
    }

    [Fact]
    public void CancellingWithNobodyTalking_DoesNothing()
    {
        NpcInteractionWatcher watcher = WatcherFor(NpcAt(new Position(0, 10)));

        watcher.Update(Position.Origin, interact: false, cancel: true);

        Assert.False(_conversations.IsOpen);
        Assert.False(_ui.HasFocus);
    }

    [Fact]
    public void TalkingToSomebodyWhoWalked_IsMeasuredFromWhereTheyAreNow()
    {
        Npc walker = new(
            "walker",
            Position.Origin,
            Conversation("walker", "First"),
            50,
            new NpcPatrol(10, [new Position(0, 100)]));
        NpcInteractionWatcher watcher = WatcherFor(walker);

        walker.Update(TimeSpan.FromSeconds(10));
        watcher.Update(Position.Origin, interact: true, cancel: false);

        Assert.False(_conversations.IsOpen);
    }

    [Fact]
    public void WatchingRefusesNoWorld()
    {
        Assert.Throws<ArgumentNullException>(
            () => new NpcInteractionWatcher(null!, _conversations, _ui));
    }

    [Fact]
    public void WatchingRefusesNoConversations()
    {
        Assert.Throws<ArgumentNullException>(
            () => new NpcInteractionWatcher(new TestWorld(), null!, _ui));
    }

    [Fact]
    public void WatchingRefusesNoUserInterface()
    {
        Assert.Throws<ArgumentNullException>(
            () => new NpcInteractionWatcher(new TestWorld(), _conversations, null!));
    }
}
