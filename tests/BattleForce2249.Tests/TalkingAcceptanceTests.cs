using Microsoft.Extensions.DependencyInjection;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// Talks to somebody the way a person does: frames of device state pushed into
/// <see cref="IHost.Input"/>, and nothing else. No test opens a conversation by hand, focuses
/// anything, or reaches past the keys — flying up to somebody and pressing the key has to be the
/// whole of it.
/// </summary>
/// <remarks>
/// The engine's own tests prove each rule against parts a test assembled. What they cannot prove
/// is that the rules add up to something a person can do: that the space bar which started the
/// game also opens a conversation, that the ship stops without anything having paused it, that
/// the world carries on while the player reads, and that one press of escape gets them out.
/// <para>
/// The world here is the shipped one with somebody standing in it, because the shipped world has
/// nobody in it: who is in the mines and what they say is writing that has not been done. What is
/// under test is the machinery, and a test authoring its own NPC is the honest way to reach it.
/// </para>
/// </remarks>
public sealed class TalkingAcceptanceTests : HostTestBase
{
    /// <summary>
    /// Where the test stands its NPC — far enough ahead that the player has to fly there, and well
    /// short of quest 1's end marker at 1,000 so the two cannot interfere.
    /// </summary>
    static readonly Position StandingAt = new(0, 200);

    const double InteractionRange = 60;

    /// <summary>
    /// The shipped world with people in it: the quest markers, the start position and the placement
    /// are the real ones, so the game that is flown here is the game that ships.
    /// </summary>
    sealed class PopulatedWorld(params Npc[] npcs) : IWorld
    {
        readonly BattleForceWorld _world = new();

        public Position PlayerStart => _world.PlayerStart;

        public IReadOnlyList<QuestMarkers> QuestMarkers => _world.QuestMarkers;

        public IReadOnlyList<Npc> Npcs { get; } = npcs;

        public Position Introduce(Ship ship, string location) => _world.Introduce(ship, location);
    }

    static Conversation TwoLines() =>
        new("test-conversation", [
            new ConversationLine("SpeakerName", "First"),
            new ConversationLine("SpeakerName", "Second"),
        ]);

    static Npc Standing() => new("standing", StandingAt, TwoLines(), InteractionRange);

    static Npc Walking() =>
        new(
            "walking",
            new Position(500, 0),
            TwoLines(),
            InteractionRange,
            new NpcPatrol(10, [new Position(500, 1000)]));

    IGameSession Session => Resolve<IGameSession>();

    Conversations Conversations => Resolve<Conversations>();

    static InputFrame Keys(
        bool ahead = false,
        bool confirm = false,
        bool interact = false,
        bool cancel = false) =>
        new(new KeyboardFrame(ahead, false, false, false, confirm, interact, cancel), GamePadFrame.None);

    /// <summary>
    /// Composes the game as it ships apart from the world, which gains the people this test needs,
    /// and the two doubles the base class cannot do without.
    /// </summary>
    void ComposeWith(params Npc[] npcs) =>
        Configure(services: services =>
        {
            services
                .AddSingleton<IUIController, UIController>()
                .AddSingleton<IScreenDirector, LifecycleScreenDirector>()
                .AddSingleton<IWorld>(new PopulatedWorld(npcs));
        });

    IHost AtTheControls()
    {
        IHost host = CreateHost();
        host.Start();
        host.Input(InputFrame.None);
        host.Update(TimeSpan.FromDays(1));

        MenuScreen menu = (MenuScreen)Resolve<IMenuScreen>();
        for (int frame = 0; !menu.IsReadyForInput; frame++)
        {
            Assert.True(frame < 1000, "the menu never became ready for input");
            host.Input(InputFrame.None);
            host.Update(TimeSpan.Zero);
        }

        // the space bar starts the game, which is also the key that will open the conversation
        host.Input(Keys(confirm: true, interact: true));
        host.Update(TimeSpan.Zero);
        host.Input(Keys());
        host.Update(TimeSpan.Zero);

        return host;
    }

    static void Frames(IHost host, InputFrame holding, int frames, Func<bool>? until = null)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            if (until?.Invoke() is true)
            {
                return;
            }

            host.Input(holding);
            host.Update(TimeSpan.FromSeconds(1 / 60.0));
        }
    }

    /// <summary>
    /// Flies until the player is within earshot of whoever is standing at
    /// <see cref="StandingAt"/>, and fails rather than carrying on if they never get there.
    /// </summary>
    void FlyWithinEarshot(IHost host)
    {
        Frames(
            host,
            Keys(ahead: true),
            frames: 60 * 60,
            until: () => Session.Player.Position.DistanceTo(StandingAt) <= InteractionRange / 2);

        Assert.True(
            Session.Player.Position.DistanceTo(StandingAt) <= InteractionRange,
            "the ship never reached the NPC");
    }

    /// <summary>
    /// Presses and releases the interact key over one frame each, which is how a person presses it.
    /// </summary>
    static void Press(IHost host, InputFrame down)
    {
        host.Input(down);
        host.Update(TimeSpan.FromSeconds(1 / 60.0));
        host.Input(Keys());
        host.Update(TimeSpan.FromSeconds(1 / 60.0));
    }

    [Fact]
    public void FlyingUpToSomebodyAndPressingInteract_OpensTheConversation()
    {
        ComposeWith(Standing());
        IHost host = AtTheControls();
        FlyWithinEarshot(host);

        host.Input(Keys(interact: true));
        host.Update(TimeSpan.FromSeconds(1 / 60.0));

        Assert.True(Conversations.IsOpen);
        Assert.Equal("First", Conversations.Current.TextKey);
    }

    [Fact]
    public void StartingTheGame_DoesNotOpenAConversationWithSomebodyStandingThere()
    {
        // the start press is still held on the first frame of the game, and interact is the same
        // key. If the edge were not tracked across the change of focus, the player would arrive in
        // a conversation they never asked for.
        ComposeWith(new Npc("standing", Position.Origin, TwoLines(), InteractionRange));

        AtTheControls();

        Assert.False(Conversations.IsOpen);
    }

    [Fact]
    public void PressingInteractWithNobodyAbout_OpensNothing()
    {
        ComposeWith(Standing());
        IHost host = AtTheControls();

        host.Input(Keys(interact: true));
        host.Update(TimeSpan.FromSeconds(1 / 60.0));

        Assert.False(Conversations.IsOpen);
    }

    [Fact]
    public void WhileTalking_TheShipComesToRestOnItsOwn()
    {
        // no pause state and no second way for the ship to be stationary: the conversation takes
        // the input, the ship is given nothing, and drag does the rest
        ComposeWith(Standing());
        IHost host = AtTheControls();
        FlyWithinEarshot(host);
        Press(host, Keys(interact: true));

        double whenTheTalkOpened = Session.Ship.Movement.Velocity.Speed;
        Frames(host, Keys(ahead: true), frames: 120);

        Assert.True(whenTheTalkOpened > 0, "the ship was already stationary, so this proves nothing");
        Assert.True(
            Session.Ship.Movement.Velocity.Speed < whenTheTalkOpened,
            "the ship did not slow down while the player was talking");
    }

    [Fact]
    public void WhileTalking_TheAheadKeyDoesNotFlyTheShip()
    {
        ComposeWith(Standing());
        IHost host = AtTheControls();
        FlyWithinEarshot(host);
        Press(host, Keys(interact: true));

        // long enough for drag to have taken everything the ship had, if nothing is adding to it
        Frames(host, Keys(ahead: true), frames: 60 * 20);

        Assert.True(
            Session.Ship.Movement.Velocity.Speed < 1,
            "the ahead key was still flying the ship during a conversation");
    }

    [Fact]
    public void WhileTalking_TheWorldKeepsRunning()
    {
        // pillar 4 at its smallest: a conversation is not a safe pocket to hide in
        Npc walking = Walking();
        ComposeWith(Standing(), walking);
        IHost host = AtTheControls();
        FlyWithinEarshot(host);
        Press(host, Keys(interact: true));

        Position whenTheTalkOpened = walking.Position;
        Frames(host, Keys(), frames: 120);

        Assert.True(Conversations.IsOpen, "the conversation closed before the frames that matter");
        Assert.True(
            walking.Position.Y > whenTheTalkOpened.Y,
            "the world stopped while the player was talking");
    }

    [Fact]
    public void PressingConfirm_TurnsThePage()
    {
        ComposeWith(Standing());
        IHost host = AtTheControls();
        FlyWithinEarshot(host);
        Press(host, Keys(interact: true));

        Press(host, Keys(confirm: true));

        Assert.Equal("Second", Conversations.Current.TextKey);
    }

    [Fact]
    public void ReadingToTheEnd_HandsTheShipBack()
    {
        ComposeWith(Standing());
        IHost host = AtTheControls();
        FlyWithinEarshot(host);
        Press(host, Keys(interact: true));

        Press(host, Keys(confirm: true));
        Press(host, Keys(confirm: true));

        Assert.False(Conversations.IsOpen);

        double before = Session.Player.Position.Y;
        Frames(host, Keys(ahead: true), frames: 60);
        Assert.True(Session.Player.Position.Y > before, "the ship was not flyable again");
    }

    [Fact]
    public void OnePressOfEscape_GetsThePlayerOut()
    {
        // the world is armed and the ship is stationary, so backing out has to be immediate and
        // always available — one press, on any line, with nothing to sit through
        ComposeWith(Standing());
        IHost host = AtTheControls();
        FlyWithinEarshot(host);
        Press(host, Keys(interact: true));

        host.Input(Keys(cancel: true));
        host.Update(TimeSpan.FromSeconds(1 / 60.0));

        Assert.False(Conversations.IsOpen);
    }

    [Fact]
    public void AfterBackingOut_TheShipFliesAgain()
    {
        ComposeWith(Standing());
        IHost host = AtTheControls();
        FlyWithinEarshot(host);
        Press(host, Keys(interact: true));
        Press(host, Keys(cancel: true));

        double before = Session.Player.Position.Y;
        Frames(host, Keys(ahead: true), frames: 60);

        Assert.True(Session.Player.Position.Y > before, "the ship was not flyable again");
    }

    [Fact]
    public void SomebodyCanBeTalkedToTwice()
    {
        ComposeWith(Standing());
        IHost host = AtTheControls();
        FlyWithinEarshot(host);
        Press(host, Keys(interact: true));
        Press(host, Keys(cancel: true));

        Press(host, Keys(interact: true));

        Assert.True(Conversations.IsOpen);
        Assert.Equal("First", Conversations.Current.TextKey);
    }
}
