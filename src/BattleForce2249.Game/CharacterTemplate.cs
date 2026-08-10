using OliveGameStudio;

namespace BattleForce2249;

/// <summary>
/// A character as content authors them: who they are, the hull they start in, where they start, and
/// what they start with.
/// </summary>
/// <remarks>
/// A template is authored and never changes at runtime; <see cref="Character"/> is the instance
/// played, and it is the instance that earns, spends and loses things. The pairing is the same one
/// <c>QuestDefinition</c> and <c>Quest</c> already use, for the same reason: content that can be
/// changed by play is content nothing can be checked against.
/// </remarks>
/// <param name="Id">
/// The stable identifier the template is known and saved by. It must outlive changes to the name,
/// so it is not derived from one, and it is never translated.
/// </param>
/// <param name="Name">The character's name as shown to the player, in their language.</param>
/// <param name="Ship">The hull they start in.</param>
/// <param name="StartLocation">
/// The identifier of the place a new game begins at. A place, not a coordinate: the world is what
/// turns this into a position, so several quests can mean something at the same named place and a
/// later visit can be told apart from the first.
/// </param>
/// <param name="StartingInventory">What they own before they have earned anything.</param>
public sealed record CharacterTemplate(
    string Id,
    string Name,
    ShipProfile Ship,
    string StartLocation,
    IReadOnlyList<Item> StartingInventory)
{
    /// <summary>
    /// Gets the stable identifier the template is known and saved by.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// The identifier is <c>null</c>. A template nothing can be named by is one a save cannot refer
    /// to, and the character it describes could never be loaded back.
    /// </exception>
    public string Id { get; } = Authored(Id, nameof(Id));

    /// <summary>
    /// Gets the character's name as shown to the player, in their language.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// The name is <c>null</c>. The one thing a template guarantees the player is shown is the
    /// name, so a missing one is content that is not finished rather than content that is sparse.
    /// </exception>
    public string Name { get; } = Authored(Name, nameof(Name));

    /// <summary>
    /// Gets the hull they start in.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// The hull is <c>null</c>. There is no character without a ship to begin in — a template
    /// missing one destroys <see cref="Character"/>'s constructor with a
    /// <see cref="NullReferenceException"/> that names nothing the author wrote.
    /// </exception>
    public ShipProfile Ship { get; } = Authored(Ship, nameof(Ship));

    /// <summary>
    /// Gets the identifier of the place a new game begins at.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// The place is <c>null</c>. A character has to start somewhere, and a template that names
    /// nowhere is one the world cannot put a ship into.
    /// </exception>
    public string StartLocation { get; } = Authored(StartLocation, nameof(StartLocation));

    /// <summary>
    /// Gets what they own before they have earned anything.
    /// </summary>
    /// <remarks>
    /// Empty is the answer every character gives today, and it is a different answer from
    /// <c>null</c>: owning nothing is a decision content made, while a list that is not there is a
    /// field nobody filled in.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// The list is <c>null</c>. It is read straight into an <see cref="Inventory"/> when a
    /// character is built, so the failure otherwise surfaces there rather than here.
    /// </exception>
    public IReadOnlyList<Item> StartingInventory { get; } =
        Authored(StartingInventory, nameof(StartingInventory));

    /// <summary>
    /// Checks an authored field is actually there, naming the field rather than whatever the value
    /// is later handed to.
    /// </summary>
    /// <remarks>
    /// This is <see cref="ShipProfile"/>'s rule applied to the template that carries one: the types
    /// downstream refuse these values anyway, but only once a <see cref="Character"/> is built from
    /// the template, and by then the complaint names a constructor parameter of a type the content
    /// author has never heard of — or, for the hull and the inventory, does not name anything at
    /// all, because a <see cref="NullReferenceException"/> has no parameter to name. Refusing here
    /// puts the refusal next to the content that caused it.
    ///
    /// Takes the name rather than relying on <see cref="System.Runtime.CompilerServices.CallerArgumentExpressionAttribute"/>,
    /// which from inside this helper would report <c>field</c> for every one of them.
    /// </remarks>
    static T Authored<T>(T field, string name)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(field, name);

        return field;
    }
}
