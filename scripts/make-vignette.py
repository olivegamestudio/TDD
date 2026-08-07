#!/usr/bin/env python3
"""Draws the vignette overlay the game screen is framed with.

The vignette is a mask rather than artwork: black everywhere, with an alpha ramp that is clear
across the middle of the frame and opaque at the corners. There is nothing in it an artist would
draw, so it is generated from the numbers below and the numbers are the thing to review — a
committed PNG nobody can read is otherwise a magic asset.

Run from the repository root:

    python3 scripts/make-vignette.py

which rewrites BattleForce2249.MonoGame/Content/vignette.png in place.

The texture is square and small. It is stretched across whatever viewport it is drawn into, so its
own aspect means nothing, and it is a smooth gradient, so the graphics device's filtering carries
it up to any window without banding.
"""

import math
import struct
import zlib
from pathlib import Path

SIZE = 256
"""How many pixels across the mask is authored. Small on purpose: see the module docstring."""

CLEAR_RADIUS = 0.50
"""How far out the frame stays completely clear, in half-viewports from the middle.

One is the middle of an edge; the square root of two is a corner. Holding the middle of the frame
clear is the whole point — a vignette that starts darkening at the centre is a dimmer.
"""

OPAQUE_RADIUS = 1.45
"""Where the ramp reaches full darkness, in the same units.

Past the edge midpoints, so an edge is shaded rather than black and only the corners close fully.
That is what reads as framing rather than as looking down a tube.
"""


def alpha_at(x: int, y: int) -> int:
    """The mask's opacity at one pixel, 0 to 255."""
    # -1 to 1 across the texture, so the distance below is measured in half-viewports.
    u = (x + 0.5) / SIZE * 2 - 1
    v = (y + 0.5) / SIZE * 2 - 1

    radius = math.hypot(u, v)

    t = (radius - CLEAR_RADIUS) / (OPAQUE_RADIUS - CLEAR_RADIUS)
    t = min(max(t, 0.0), 1.0)

    # Smoothstep rather than a straight line: a linear ramp leaves a visible edge where it starts,
    # because the eye reads the discontinuity in the rate of change, not in the value.
    eased = t * t * (3 - 2 * t)

    return round(eased * 255)


def png_bytes() -> bytes:
    """The mask as an 8-bit RGBA PNG."""
    raw = bytearray()

    for y in range(SIZE):
        raw.append(0)  # filter type 0 (none) for the scanline
        for x in range(SIZE):
            raw += bytes((0, 0, 0, alpha_at(x, y)))

    def chunk(kind: bytes, payload: bytes) -> bytes:
        return (
            struct.pack(">I", len(payload))
            + kind
            + payload
            + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)
        )

    header = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0)

    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", header)
        + chunk(b"IDAT", zlib.compress(bytes(raw), 9))
        + chunk(b"IEND", b"")
    )


def main() -> None:
    target = Path(__file__).resolve().parent.parent / "BattleForce2249.MonoGame" / "Content" / "vignette.png"
    target.write_bytes(png_bytes())
    print(f"wrote {target} ({SIZE}x{SIZE})")


if __name__ == "__main__":
    main()
