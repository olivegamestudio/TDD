namespace OliveGameStudio;

/// <summary>
/// Something a character can own and a ship can be fitted with.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately no more than an identifier. What an item <em>is</em> — whether it stacks, which
/// slot it occupies, what category it belongs to, and what happens when its durability bottoms out
/// — is an open design decision, and a shape invented ahead of that answer would be a shape to
/// migrate away from rather than one to build on.
/// </para>
/// <para>
/// The identifier is an identifier and is never translated: an item named in a save written in one
/// language has to be found again in another. Whatever the player reads about an item will be
/// looked up from this, not stored in it.
/// </para>
/// </remarks>
/// <param name="Id">The stable identifier the item is known and saved by.</param>
public sealed record Item(string Id)
{
    /// <summary>
    /// The stable identifier the item is known and saved by.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The identifier is missing or blank. An item nothing can name cannot be equipped, found in a
    /// save, or told apart from another one, so it is refused where it is built rather than
    /// wherever it is first looked for.
    /// </exception>
    public string Id { get; } = Validated(Id);

    static string Validated(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return id;
    }
}
