using System.Numerics;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// A camera that enforces none of <see cref="ICamera"/>'s rules: it carries whatever zoom or
/// target it is given, including the ones <see cref="Camera2D"/> refuses.
/// </summary>
/// <remarks>
/// <see cref="StarField"/> takes an <see cref="ICamera"/> rather than a <see cref="Camera2D"/>,
/// so its own guard against a zoom that cannot be drawn from is not made redundant by #57 putting
/// a check on the camera — it is what stands between the field and an implementation that did not
/// keep the contract. This is that implementation, so the guard can still be exercised. The
/// transform is copied from <see cref="Camera2D"/> deliberately: a stub that transformed
/// differently would prove something about the stub.
/// </remarks>
sealed class UncheckedCamera : ICamera
{
    public Vector2 Target { get; set; }

    public float PixelsPerUnit { get; set; } = 1f;

    public Vector2 WorldToScreen(Vector2 world, Vector2 viewportSize)
    {
        Vector2 fromTarget = (world - Target) * PixelsPerUnit;

        return new Vector2(
            viewportSize.X / 2f + fromTarget.X,
            viewportSize.Y / 2f - fromTarget.Y);
    }
}
