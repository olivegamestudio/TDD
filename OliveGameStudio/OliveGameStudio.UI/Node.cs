namespace OliveGameStudio;

/// <summary>
/// Represents a node in a navigable user interface layout.
/// Each node is associated with a button and can have directional relationships with other nodes,
/// allowing for navigation in the UI using cardinal directions.
/// </summary>
sealed class Node(Button button)
{
    /// <summary>
    /// Represents a button in the user interface.
    /// </summary>
    /// <remarks>
    /// The <see cref="Button"/> class is a fundamental component of the UI system, used to represent
    /// individual interactive elements. It encapsulates the name of the button, which can be utilized
    /// for identification and display purposes within a navigable UI structure. This class is immutable
    /// and inherits from the <see cref="Element"/> type to integrate with the overall UI framework.
    /// </remarks>
    public Button Button { get; } = button;

    /// <summary>
    /// Indicates whether the node is enabled and can be interacted with in the UI.
    /// </summary>
    /// <remarks>
    /// The <see cref="Enabled"/> property determines the interactive state of a node in the user interface.
    /// When set to <c>true</c>, the node is considered active and can be navigated to or triggered by user input.
    /// If set to <c>false</c>, the node is disabled and will not respond to user interactions such as navigation
    /// or button presses.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Represents the action to be executed when the associated node's button is pressed.
    /// </summary>
    /// <remarks>
    /// The <see cref="PressedAction"/> property allows the definition of a custom action that executes
    /// when the associated button of a node is pressed. This facilitates dynamic behavior customization
    /// for UI interactions, enabling developers to assign or modify specific logic to handle button
    /// press events. The property is optional and can be left unset.
    /// </remarks>
    public Action? PressedAction { get; set; }

    /// <summary>
    /// Represents an action to be executed upon releasing a node's associated button.
    /// </summary>
    /// <remarks>
    /// The <see cref="ReleasedAction"/> property defines an optional delegate that specifies the
    /// behavior triggered when the button associated with the node is released. This provides a
    /// mechanism to execute custom logic or respond to release events in the user interface.
    /// The action can be assigned dynamically, allowing for flexible and context-specific functionality.
    /// </remarks>
    public Action? ReleasedAction { get; set; }

    /// <summary>
    /// Gets or sets the node positioned in the upward direction relative to the current node.
    /// </summary>
    /// <remarks>
    /// The <see cref="Up"/> property establishes a navigational relationship between the current node
    /// and another node in the upward direction within the user interface. This property allows
    /// for intuitive and seamless navigation between nodes when responding to user inputs, such as
    /// directional controls. If no node is assigned, the upward navigation relationship is considered absent.
    /// </remarks>
    public Node? Up { get; set; }

    /// <summary>
    /// Gets or sets the navigable node located in the downward direction relative to the current node.
    /// </summary>
    /// <remarks>
    /// The <see cref="Down"/> property defines a directional relationship between the current node and
    /// another node positioned below it in a navigable user interface layout. This association enables
    /// seamless downward navigation within the UI structure, typically through user input or controller
    /// commands. If set to <c>null</c>, the node does not have a valid downward connection.
    /// </remarks>
    public Node? Down { get; set; }

    /// <summary>
    /// Gets or sets the node linked to the left direction in a navigable UI structure.
    /// </summary>
    /// <remarks>
    /// The <see cref="Left"/> property defines a directional relationship from the current node
    /// to another node positioned to its left within the UI layout. This allows for seamless navigation
    /// between nodes using leftward movements, typically corresponding to user input or controller actions.
    /// </remarks>
    public Node? Left { get; set; }

    /// <summary>
    /// Gets or sets the node located to the right of the current node in the navigable UI layout.
    /// </summary>
    /// <remarks>
    /// The <see cref="Right"/> property represents the navigational relationship between the current node
    /// and the node positioned to its right within the user interface. Setting this property links the
    /// current node to a neighbor, facilitating directional navigation using the <see cref="Direction.Right"/>
    /// enumeration. If not explicitly set, this property defaults to <c>null</c>, indicating no connection
    /// in the rightward direction.
    /// </remarks>
    public Node? Right { get; set; }

    /// <summary>
    /// Retrieves the neighboring <see cref="Node"/> in the specified <see cref="Direction"/>.
    /// </summary>
    /// <param name="direction">The direction in which to find the neighboring node, represented by the <see cref="Direction"/> enum.</param>
    /// <returns>The neighboring <see cref="Node"/> in the specified direction, or <c>null</c> if no neighbor exists in that direction.</returns>
    public Node? Neighbour(Direction direction) => direction switch
    {
        Direction.Up => Up,
        Direction.Down => Down,
        Direction.Left => Left,
        Direction.Right => Right,
        _ => null,
    };
}