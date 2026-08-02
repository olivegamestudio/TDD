using System.Numerics;

namespace BattleForce2249;

/// <summary>
/// Where the ship is and which way it is pointing. This is the whole of what the drawing side
/// needs to know about the ship, and the whole of what the logic side has to supply.
/// </summary>
/// <param name="Position">
/// The ship's position in world units, with the positive Y axis pointing forward.
/// </param>
/// <param name="Heading">
/// Which way the ship's nose points, in radians. Zero is straight forward — along the positive
/// world Y axis, which is up the screen — and the angle increases as the ship turns to
/// starboard, which is clockwise as the player sees it.
/// </param>
/// <remarks>
/// The heading is carried as an angle rather than a direction vector because that is the shape
/// a helm produces and the shape the physics keeps. It does mean the sense of the angle is a
/// convention rather than something the type can enforce: a model that measures its heading the
/// other way round draws a ship that turns the wrong way, so a model handing a heading over
/// negates it rather than redefining what this means.
/// </remarks>
public readonly record struct ShipPose(Vector2 Position, float Heading);
