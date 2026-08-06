namespace OliveGameStudio;

/// <summary>
/// One thing said in a conversation: who says it, and what they say.
/// </summary>
/// <remarks>
/// Both are <em>keys</em> rather than words. The engine ships no text — what an NPC says is the
/// game's content, in the game's language files — and a line that carried the words themselves
/// would be a sentence baked into a build in whichever language the author happened to type. The
/// same rule the quest model already follows: the identifier is not the text.
/// </remarks>
public sealed record ConversationLine
{
    /// <summary>
    /// Declares a line spoken by <paramref name="speakerKey"/> reading
    /// <paramref name="textKey"/>.
    /// </summary>
    /// <param name="speakerKey">The key of the speaker's name, as the player reads it.</param>
    /// <param name="textKey">The key of what is said.</param>
    /// <exception cref="ArgumentException">
    /// Either key is missing or blank. A line nobody says, or one that says nothing, is a line the
    /// author meant something else by — and it would reach the player as a blank panel they have
    /// to press through rather than as anything that looks like a mistake.
    /// </exception>
    public ConversationLine(string speakerKey, string textKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(speakerKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(textKey);

        SpeakerKey = speakerKey;
        TextKey = textKey;
    }

    /// <summary>
    /// Gets the key of the speaker's name.
    /// </summary>
    public string SpeakerKey { get; }

    /// <summary>
    /// Gets the key of what is said.
    /// </summary>
    public string TextKey { get; }
}

/// <summary>
/// What somebody has to say: an ordered run of lines, read one at a time.
/// </summary>
/// <remarks>
/// <para>
/// Simple text, in order, with no choices in it. Whether a choice can change what happens is
/// undecided in <c>docs/design-capture.md</c> §12, and a branch authored before that decision
/// would be a guess at the answer rather than a feature.
/// </para>
/// <para>
/// A conversation is content and holds no state: several NPCs may share one, and the same one may
/// be opened again after it has been read. Where the player has got to is
/// <see cref="Conversations"/>, which is the thing there can only be one of.
/// </para>
/// </remarks>
public sealed class Conversation
{
    readonly ConversationLine[] _lines;

    /// <summary>
    /// Declares a conversation of the given lines, said in the order they are given.
    /// </summary>
    /// <param name="id">
    /// What identifies this conversation. An identifier, never translated — a save written in one
    /// language has to be read in another.
    /// </param>
    /// <param name="lines">What is said, in order.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is missing or blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="lines"/> is empty or contains a <c>null</c>. A conversation in which nothing
    /// is said would open and close on the same press, which reads to the player as an NPC that
    /// does not work rather than as one with nothing to say.
    /// </exception>
    /// <remarks>
    /// The lines are copied here rather than held by reference, so a list left in the caller's
    /// hands cannot gain an entry after it was checked — the same rule the quest definitions
    /// follow. It also means a conversation the player is part way through cannot change under
    /// them.
    /// </remarks>
    public Conversation(string id, IEnumerable<ConversationLine> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(lines);

        _lines = [.. lines];

        if (_lines.Length == 0)
        {
            throw new ArgumentException("A conversation must have at least one line.", nameof(lines));
        }

        if (Array.IndexOf(_lines, null) >= 0)
        {
            throw new ArgumentException("A conversation cannot hold a line that is not there.", nameof(lines));
        }

        Id = id;
    }

    /// <summary>
    /// Gets what identifies this conversation.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets what is said, in the order it is said.
    /// </summary>
    public IReadOnlyList<ConversationLine> Lines => _lines;
}
