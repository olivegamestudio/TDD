using System.Numerics;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// A camera that enforces none of <see cref="ICamera"/>'s rules, transforming exactly as
/// <see cref="Camera2D"/> does.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Camera2D"/> refuses a zoom or a target that is not finite where it is written, which
/// is the fix for those numbers reaching the transform at all. But everything drawn in the world
/// takes an <see cref="ICamera"/> rather than a <see cref="Camera2D"/>, so a renderable's own
/// guard against an undrawable zoom is a statement about the interface, not about the one
/// implementation that keeps it. This is what makes that statement testable: with the real camera
/// refusing the assignment, there is otherwise no way to hand a renderable the value it guards
/// against, and the guard would only ever be exercised by the code that made it unnecessary.
/// </para>
/// <para>
/// It is not a stand-in for a camera that has gone wrong — it is a stand-in for the next
/// implementation of the interface, which has not been written yet.
/// </para>
/// </remarks>
public sealed class UncheckedCamera : ICamera
{
    /// <inheritdoc />
    public Vector2 Target { get; set; }

    /// <inheritdoc />
    public float PixelsPerUnit { get; set; } = 1f;

    /// <inheritdoc />
    public float Orientation { get; set; }

    /// <inheritdoc />
    public Vector2 WorldToScreen(Vector2 world, Vector2 viewportSize)
    {
        Vector2 fromTarget = (world - Target) * PixelsPerUnit;

        float sin = MathF.Sin(Orientation);
        float cos = MathF.Cos(Orientation);

        Vector2 turned = new(
            (fromTarget.X * cos) - (fromTarget.Y * sin),
            (fromTarget.X * sin) + (fromTarget.Y * cos));

        return new Vector2(
            viewportSize.X / 2f + turned.X,
            viewportSize.Y / 2f - turned.Y);
    }
}
