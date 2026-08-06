#!/usr/bin/env python3
"""Convert a Unity scene export into a BattleForce region file.

The previous prototype exported its levels from Unity as a flat list of `bodies`
(scenery) and `markers` (anything with behaviour, identified by a comma-joined
list of Unity component names). This turns one of those into the game's own
region format.

Two things are recovered rather than copied, because the Unity export lost them:

  * the **quest a zone belongs to**, which was encoded in names like
    `Q1_START_ZONE` and is written out here as an explicit reference; and
  * the **radius**, which the export dropped entirely — its zones were
    `CircleCollider2D`s in the editor and bare points in the file. There is
    nothing to recover it from, so a default is applied per kind and the count
    is reported, because a guessed trigger distance is a thing someone has to
    know they need to check.

Bodies naming a sprite the content build does not have are dropped, and the
count is reported. A missing texture is a hard failure at load time rather than
a gap on screen, so shipping a region that names one turns a content oversight
into a crash — and the prototype's export names several that belonged to the
player's ship prefab and to editor gizmos rather than to the level.

What counts as "has" is read from `Content.mgcb`, not from the folder listing.
The two are different, and the difference has already cost us: `glow.png` sat in
the content folder and was never registered, so the folder said yes, the build
shipped nothing, and the game crashed on entering the world. Only the `/build:`
lines say what is actually produced.

Run:  python3 scripts/import-unity-scene.py <export.json> <region-id> <out.json> [content-dir]
"""

import json
import os
import re
import sys
from collections import Counter

# Which Unity component identifies which kind of marker. Order matters: the
# first match wins, so the more specific behaviours are listed before the
# scenery-ish ones they are often bundled with.
KINDS = [
    ("QuestStartZoneBehaviour", "QuestStart"),
    ("QuestCompleteZoneBehaviour", "QuestComplete"),
    ("QuestLabel", "QuestLabel"),
    ("SaveProgress", "Save"),
    ("CameraShakeZone", "CameraShake"),
    ("SplineContainer", "Path"),
    ("ParallaxEffect", "Parallax"),
    ("Light2D", "Light"),
    ("AudioSource", "Sound"),
    ("Player", "PlayerStart"),
]

# What radius to assume where the export gave none. These are the distances the
# prototype's colliders looked to be authored at; every one of them is a guess
# and is reported as such.
DEFAULT_RADIUS = {
    "QuestStart": 2.0,
    "QuestComplete": 2.0,
    "Save": 2.0,
    "CameraShake": 3.0,
}

# How many of this game's world units one Unity unit becomes.
#
# The two projects measure the world differently and neither is wrong. The Unity
# scene spans about 130 units end to end; this game flies the opening quest over
# 1000, and its camera draws one world unit as one pixel. Imported straight, the
# whole debris field lands in a patch a couple of hundred pixels across with the
# ship filling it.
#
# Applied here rather than at load, because it is a property of importing
# foreign content and not of the format: a region authored in this game's own
# editor will already be in this game's units and must not be scaled again.
WORLD_SCALE = 15.0

# Body sizes take the SAME factor as positions, and must keep doing so.
#
# This was briefly split — positions scaled one way and sizes another — to make
# the debris read smaller against the ship. It does not work: scaling where
# things are without scaling how big they are pulls the composition apart, so a
# cluster somebody authored as a cluster arrives as scattered specks with gaps
# between them that were never drawn. The spacing IS the content.
#
# The ship reading too large beside the scenery is a real problem, but it is the
# ship's problem: ShipView.LengthInWorldUnits and the camera's zoom both change
# how the ship sits in the scene without touching how the scene sits in itself.
BODY_SIZE_SCALE = WORLD_SCALE

# What the Unity content took one unit to be, in texture pixels, and how tall the
# scene's camera view was in those same pixels. Backdrops are laid out against
# the second: they are painted on the sky, so what matters is how much of the
# view they filled rather than where they stood in the world.
AUTHORED_PIXELS_PER_UNIT = 100.0

QUEST_NAME = re.compile(r"^Q(\d+)_(START|END)_ZONE$", re.IGNORECASE)

# Bodies that are backdrop rather than scenery: painted on the sky, taking none
# of the camera's movement. The Unity scene drew these as ordinary sprites and
# relied on a separate parallax component to hold them still, which the export
# did not carry — so they are named here.
FIXED_BODIES = {"SPACE", "PLANET", "ASTEROIDS"}

# Sprites the prototype named that this build ships under another name.
SPRITE_ALIASES = {
    "planet rusty blue": "planet14",
    "back_asteroids": "asteroid2",
}


def kind_of(components: str) -> str | None:
    for needle, kind in KINDS:
        if needle in components:
            return kind
    return None


def reference_of(name: str, kind: str) -> str:
    """The quest a zone belongs to, taken from names like `Q3_START_ZONE`.

    Encoding identity in a name is what this format exists to stop, so it is
    unpicked once here and written out explicitly from then on.
    """
    if kind not in ("QuestStart", "QuestComplete", "QuestLabel"):
        return ""

    match = QUEST_NAME.match(name)
    return f"quest-{int(match.group(1))}" if match else ""


def main() -> int:
    if len(sys.argv) not in (4, 5):
        print(__doc__)
        return 2

    source, region_id, destination = sys.argv[1:4]
    content = sys.argv[4] if len(sys.argv) == 5 else None
    scene = json.load(open(source))

    available = None
    if content:
        mgcb = os.path.join(content, "Content.mgcb")
        with open(mgcb) as handle:
            available = {
                os.path.splitext(line.strip()[len("/build:") :])[0]
                for line in handle
                if line.startswith("/build:")
            }

    dropped = Counter()
    kept = []
    for b in scene["bodies"]:
        b["sprite"] = SPRITE_ALIASES.get(b["sprite"], b["sprite"])
        if available is not None and b["sprite"] not in available:
            dropped[b["sprite"]] += 1
            continue
        kept.append(b)

    bodies = []
    for b in kept:
        fixed = b["name"] in FIXED_BODIES

        # A backdrop keeps its authored numbers: it is measured against the view
        # rather than the world, so scaling it into world units would be
        # converting it into a space it never occupied.
        position_scale = 1.0 if fixed else WORLD_SCALE
        size_scale = 1.0 if fixed else BODY_SIZE_SCALE

        bodies.append(
            {
                "name": b["name"],
                "sprite": b["sprite"],
                "x": round(b["x"] * position_scale, 4),
                "y": round(b["y"] * position_scale, 4),
                "rotationDegrees": round(b["rotationDegrees"], 4),
                "scaleX": round(b["scaleX"] * size_scale, 4),
                "scaleY": round(b["scaleY"] * size_scale, 4),
                "layer": b["sortingLayer"],
                "order": b["sortingOrder"],
                "parallax": 0 if fixed else 1,
            }
        )

    markers = []
    guessed = Counter()
    unreferenced = []

    for m in scene["markers"]:
        kind = kind_of(m.get("components", ""))
        if kind is None:
            continue

        radius = DEFAULT_RADIUS.get(kind, 0.0)
        if radius:
            guessed[kind] += 1

        reference = reference_of(m["name"], kind)
        if kind in ("QuestStart", "QuestComplete") and not reference:
            unreferenced.append(m["name"])

        markers.append(
            {
                "name": m["name"],
                "kind": kind,
                "reference": reference,
                "x": round(m["x"] * WORLD_SCALE, 4),
                "y": round(m["y"] * WORLD_SCALE, 4),
                "rotationDegrees": round(m["rotationDegrees"], 4),
                # Scaled with everything else, or a trigger authored to fire when
                # the player is close would fire when they are fifteen times too
                # far away — or never.
                "radius": round(radius * WORLD_SCALE, 4),
            }
        )

    json.dump(
        {"id": region_id, "bodies": bodies, "markers": markers},
        open(destination, "w"),
        indent=2,
    )

    print(f"{destination}: {len(bodies)} bodies, {len(markers)} markers")
    print(f"  kinds: {dict(Counter(m['kind'] for m in markers))}")
    print(f"  fixed to the screen: {[b['name'] for b in bodies if b['parallax'] == 0]}")
    print(f"  positions x{WORLD_SCALE:g}, body sizes x{BODY_SIZE_SCALE:g}, into this game's units")
    print(f"  radii GUESSED (the export carried none): {dict(guessed)}")
    if dropped:
        print(f"  bodies dropped, no such sprite in the content build: {dict(dropped)}")
    if unreferenced:
        print(f"  quest zones with no quest recoverable from their name: {unreferenced}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
