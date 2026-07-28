namespace OliveGameStudio;

/// <summary>
/// Represents a node in a navigable user interface layout.
/// Each node is associated with a button and can have directional relationships with other nodes,
/// allowing for navigation in the UI using cardinal directions.
/// </summary>
sealed class Node(Button button)
{
    public Button Button { get; } = button;
        
    public bool Enabled { get; set; } = true;
    
    public Action? PressedAction { get; set; }

    public Node? Up { get; set; }
        
    public Node? Down { get; set; }
        
    public Node? Left { get; set; }
        
    public Node? Right { get; set; }

    public Node? Neighbour(Direction direction) => direction switch
    {
        Direction.Up    => Up,
        Direction.Down  => Down,
        Direction.Left  => Left,
        Direction.Right => Right,
        _               => null,
    };
}