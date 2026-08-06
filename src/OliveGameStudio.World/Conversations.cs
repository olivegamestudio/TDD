namespace OliveGameStudio;

/// <summary>
/// The conversation the player is in, if they are in one: which one, and how far through it they
/// have read.
/// </summary>
/// <remarks>
/// <para>
/// There is one of these and it holds at most one open conversation, because the player has one
/// pair of ears. A <see cref="Conversation"/> is content and can be opened again; this is the
/// state of reading one.
/// </para>
/// <para>
/// <b>It never pauses anything.</b> Nothing here stops the world, and that is
/// <c>docs/DESIGN.md</c> pillar 4 rather than an omission — the world runs without the player and
/// does not pause for their absence, so enemies still fire and hazards still move while they talk.
/// The ship stops because a conversation takes the input, not because time did. What that costs
/// the player is paid for by <see cref="Close"/> being available on any line and taking effect on
/// the press that asked for it.
/// </para>
/// </remarks>
public sealed class Conversations
{
    Conversation? _open;

    int _line;

    /// <summary>
    /// Raised when a conversation opens, so whatever holds the controls can take them.
    /// </summary>
    public event EventHandler? Opened;

    /// <summary>
    /// Raised when a conversation closes, however it closed — read to the end, or backed out of.
    /// Whatever took the controls gives them back here, so the two routes out cannot be handled
    /// differently by accident.
    /// </summary>
    public event EventHandler? Closed;

    /// <summary>
    /// Gets the conversation being read, or <c>null</c> when the player is not talking to anybody.
    /// </summary>
    public Conversation? Open => _open;

    /// <summary>
    /// Gets a value indicating whether the player is in a conversation.
    /// </summary>
    public bool IsOpen => _open is not null;

    /// <summary>
    /// Gets the line on screen now.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Nobody is talking. There is no sensible line to answer with, and answering the last one read
    /// would let a display carry on showing a conversation that has ended.
    /// </exception>
    public ConversationLine Current =>
        _open?.Lines[_line] ?? throw new InvalidOperationException("No conversation is open.");

    /// <summary>
    /// Opens <paramref name="conversation"/> at its first line.
    /// </summary>
    /// <param name="conversation">What is to be said.</param>
    /// <exception cref="ArgumentNullException"><paramref name="conversation"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// A conversation is already open. Beginning a second one would silently abandon the first
    /// half-read, and a player pressing interact during a conversation is asking for nothing —
    /// whoever routes input already knows which of the two states they are in.
    /// </exception>
    public void Begin(Conversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        if (_open is not null)
        {
            throw new InvalidOperationException(
                "A conversation is already open; close it before beginning another.");
        }

        _open = conversation;
        _line = 0;

        Opened?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Moves on to the next line, closing the conversation once the last one has been read.
    /// </summary>
    /// <remarks>
    /// Reading past the last line is how a conversation ends normally, so it is
    /// <see cref="Close"/> rather than a state where the player is stuck on the last thing said
    /// with nothing to press.
    /// <para>
    /// Advancing when nothing is open does nothing rather than throwing. The press that advances a
    /// line and the press that closes a conversation are both real input, and a player can close
    /// one with the advance key already held — the release then arrives after the conversation has
    /// gone. That is a player being quick, not a caller being wrong.
    /// </para>
    /// </remarks>
    public void Advance()
    {
        if (_open is null)
        {
            return;
        }

        if (_line + 1 >= _open.Lines.Count)
        {
            Close();
            return;
        }

        _line++;
    }

    /// <summary>
    /// Closes the conversation immediately, wherever the player had got to.
    /// </summary>
    /// <remarks>
    /// One call, no confirmation, and nothing to sit through: the world kept running while the
    /// player talked, so somebody who opened a conversation at the wrong moment has to be able to
    /// get straight back to the controls. Closing when nothing is open does nothing, so the way out
    /// can be offered on every frame without a caller having to ask first.
    /// </remarks>
    public void Close()
    {
        if (_open is null)
        {
            return;
        }

        _open = null;
        _line = 0;

        Closed?.Invoke(this, EventArgs.Empty);
    }
}
