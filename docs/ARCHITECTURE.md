# Architecture

How the solution is laid out and which rules hold it in shape. Design intent lives in
`docs/DESIGN.md`; this describes the code.

## The engine / game split

`OliveGameStudio.*` is the engine and ships no game content. `BattleForce2249.*` is the game and
holds every piece of content. The seam is consistent across features:

> **The engine provides the service; the game provides the content.**

Saved progress follows it — the engine stores text and never learns what a save contains, while
the game decides its own save shape. Localised text follows it — the engine reads language files
and owns the fallback rules, while the game ships the strings. A new engine capability that needs
to know about ships, quests or credits is on the wrong side of this line.

`Pilgrimage` is a third party to that split: a standalone quest library that belongs to neither.

## Projects

| Project | Holds |
| ------- | ----- |
| `OliveGameStudio` | Engine composition and service registration. |
| `OliveGameStudio.Abstractions` | `IHost`, `IScreen`, `IScreenDirector`, `ISaveProgressService`. |
| `OliveGameStudio.Screen` | `ScreenDirector`, `LifecycleScreenDirector`. |
| `OliveGameStudio.UI`, `.UI.Abstractions` | Menu and button navigation. Not ship control. |
| `OliveGameStudio.FrameRate` | Frame time filtering, so a paused or slowed game holds still while frames keep arriving. |
| `OliveGameStudio.Rendering` | `Camera2D`. The world-to-screen transform, and nothing else. |
| `OliveGameStudio.MonoGame` | The MonoGame adapter: `MonoGameRenderer`, `MonoGameTextureLoader`, `MonoGameTexture`. The only engine project that names a MonoGame type. |
| `OliveGameStudio.Progress` | `LocalSaveProgressService`, persisting save text to a file. |
| `OliveGameStudio.World` | `Position`, `Player`, `Velocity`. The spatial model, and the ship physics that moves through it: `ShipMovement`, `ShipHandling`, `ShipControls`, `IShipInput`. |
| `OliveGameStudio.Localisation` | `ITextProvider`, `JsonTextProvider`, `MissingTextException`. |
| `Pilgrimage` | The quest system. No project references at all, by design. |
| `BattleForce2249` | Game host and DI registration. |
| `BattleForce2249.Abstractions` | Screen interfaces and options. |
| `BattleForce2249.Company`, `.Menu`, `.Game` | The three screens. |
| `BattleForce2249.MonoGame` | The MonoGame entry point. |

Tests live in `tests/` as `<project>.Tests`, xUnit, targeting `net10.0`.

## The quest system

`Pilgrimage` owns quest machinery and nothing else. It holds no coordinates, measures no
distances, references no other project, and never runs per frame. This is deliberate and was
corrected once already — a quest library that owns a player and a position is not a quest library.

**The model declares the rule; the presentation applies it.**

- `QuestDefinition` — authored content: a stable `Id`, a translated `Title`, a start trigger and
  an end trigger. Never changes at runtime.
- `QuestTrigger` — a `QuestTriggerKind` (today only `Proximity`) and a distance. Data, not
  behaviour.
- `Quest` — the lifecycle `NotStarted → Active → Completed`, driven only through `Start()` and
  `Complete()`. Both are safe to call every frame; `Started` and `Completed` are raised once per
  quest, not once per frame spent on a marker. `Restore` reinstates a saved state silently,
  because the player already lived those moments.
- `QuestLog` — the player's quests, republishing their events so subscribers listen in one place.
  `Capture`/`Restore` keep quest persistence a quest concern.
- `ICampaign` — the seam a game supplies quests through.

The game side supplies where things actually are:

- `IWorld` / `BattleForceWorld` — the player's start position and each quest's `QuestMarkers`.
  Forward travel is along the positive Y axis.
- `QuestProximityWatcher` — measures the player against the markers each frame and calls the
  quest API when a trigger fires. It measures the **ground the player covered** during the frame,
  not the point they finished it at, so a frame long enough to carry a fast ship from one side of
  a marker to the other still fires it. `GameScreen` passes both ends of the frame; the watcher
  keeps no memory of where the player was, or of what it has already fired — the quest model
  absorbs repeat calls.
- `GameSession` — the game in progress. Starts or resumes, and saves when a quest starts or
  completes rather than every frame.

Ids are never translated. A save written in one language has to load in another.

## Flying the ship

The engine owns the physics; the game owns the numbers. `ShipHandling` is a game's acceleration,
drag and turn rate — `DisgracedShip.Handling` supplies the shipping ones — and `ShipMovement`
is the physics they are flown through. A better ship is another `ShipHandling`, not another
physics.

- `ShipControls` — a thrust axis and a helm axis, each clamped to `[-1, 1]`. Analogue, because
  that is the shape a stick and a key both fit: a key is a 1, a stick is whatever it is pushed to.
- `IShipInput` — where the pilot's intent comes from. The platform host owns the real device, so
  the engine ships the seam and `NeutralShipInput`, which asks for nothing. A host registers a
  keyboard or gamepad after `AddOliveGameStudio` and wins under the engine's `AddSingleton`.
- `ShipMovement` — carries a `Heading` and a `Velocity`, applies thrust along the heading, and
  moves the `Player` by the ground covered. Momentum survives a turn; a turn points the ship
  somewhere new rather than teleporting the velocity there.

Two decisions worth knowing before changing this:

**Drag is integrated, not stepped.** Velocity follows `v(t) = terminal + (v₀ - terminal)·e^(-drag·t)`,
the exact answer for a constant thrust against linear drag, and the position moves by that
velocity's integral over the frame. `e^(-k·a)·e^(-k·b)` is `e^(-k·(a+b))`, so two half frames land
exactly where one whole frame does. A ship whose reach depends on the frame rate is a bug against
pillar 1, not a tuning detail.

**Top speed is derived, not configured.** `ShipHandling.MaximumSpeed` is `Acceleration / Drag`,
the speed the two settle at. A separate top speed could be set to disagree with the physics that
produces it, leaving the ship either short of its stated maximum or walled off below it.

`GameScreen.Update` flies the ship **before** it measures the quests, so a trigger fires on the
frame it is reached rather than the frame after — at 200 units a second, a frame late is a marker
the player has already passed. Entering the screen resets the ship: the save carries where the
player is, never how fast they were going. What the frame produced then leaves for the drawing
side as an `IShipView.Pose` — see **Drawing** — which is the only thing the physics and the screen
agree about.

## Saved progress

The engine's `ISaveProgressService` exposes `HasProgress`, `Load` and `Save`, all in terms of
text. `SaveGame` and `SaveGameSerializer` in `BattleForce2249.Game` decide what that text is.

Reading is deliberately forgiving: a missing, blank or damaged save deserialises to `null` and is
reported as "no save", so a corrupt file yields a new game rather than a crash. Quest states are
written by name, so reordering `QuestState` cannot silently change a save. `SaveGame`'s shape is
the compatibility boundary — changing it changes what older saves can be read back into.

A save the campaign has drifted from is tolerated in both directions: a quest the save knows but
this build no longer ships is ignored, and a quest added since the save was written starts from
the beginning.

## Localised text

A language is a file named after its culture — `Text/en.json`, `Text/pt-BR.json` — holding a flat
object of key to text, copied next to the game as content. English is the source language every
other file falls back to. Comments and trailing commas are permitted so the source file can carry
translator notes above each string.

`JsonTextProvider` owns the fallback chain, the caching and the missing-key policy. Fallback is
per key: a language that translates only some of its keys shows English for the rest rather than
reverting wholesale. `fr-CA` falls back to `fr`, then to English. A key present in no language at
all throws `MissingTextException` — that is a mistake in the game, not a missing translation, so
it fails loudly instead of showing the player a key.

A new language is a file dropped into the folder. No rebuild, no satellite assembly. That was the
point of choosing JSON over resx.

`GameText` is the game's facade over the provider, reading the language folder once on first use.
Anything that *holds* a translated string, though, has to re-read it: `BattleForceCampaign.Quests`
builds its list on each read rather than in a field initialiser, because the campaign is a DI
singleton and a cached list would freeze the player's language at startup.

## Drawing

Update and draw are separate paths, and they meet only at the platform host. `IHost.Draw` and
`IScreenDirector.Draw` take an `IRenderer` supplied by the platform for that frame; a screen with
something to show implements `IRenderable` alongside `IScreen`, and a screen without one is
skipped. Nothing about drawing reaches into `Update`, which is what keeps logic landing tested
and headless while the drawing arrives as its own issue.

**The renderer describes; the platform draws.** `IRenderer.Draw` takes a `Sprite` — texture,
screen position, rotation, origin, scale — already in screen space. `MonoGameRenderer` translates
that into a sprite batch call and decides nothing, because nothing in it can be covered by a
test: a sprite batch needs a real graphics device. Everything that could be got wrong lives above
it, where a recording `IRenderer` sees exactly what was drawn.

Textures hang off the renderer rather than the container. Both belong to the same graphics
device, and that device does not exist until the platform host has a window — later than the
container is built — so a drawable loads on its first draw, by which time the device is certainly
there. Asset keys are identifiers and are never translated.

`Camera2D` is the one place the world's axes are reconciled with the screen's. The world's
positive Y axis is forward and the screen's positive Y axis is down, so `WorldToScreen` negates
it: fly forward, and the world moves down the screen. `PixelsPerUnit` converts world units to
pixels once, at the end, which is what stops a zoom or a resized window being a change to the
physics. The viewport size is passed per call rather than held, so a resized window needs no
notification to stay correct.

The camera follows the ship. `GameScreen` points it at `IShipView.Pose` before drawing, so the
ship holds the middle of the viewport and the world moves under it. At the speeds this ship
flies, a fixed viewport is left behind within seconds, and a ship that has flown off the edge of
the screen is indistinguishable from one that was never drawn.

`IShipView` is the seam between the logic and engine stages for the ship: the logic sets a
`ShipPose` — a position and a heading — and the view draws whatever it last said. `GameScreen`
sets it at the end of `Update`, after the ship has flown and the quests have been measured, so
what is drawn is what this frame produced rather than what the last one did. A frame that arrives
while the save is still loading sets nothing, because there is no position to draw yet. A heading of
zero is straight forward, up the screen, and the angle increases to starboard, which matches the
artwork's own orientation and lets the heading pass through to the sprite untouched. `ShipMovement`
measures its heading the same way, so nothing corrects it on the way in. The sense of that angle
is still a convention rather than something the type can enforce, and a model that measured it
the other way round would negate it at the point it hands the pose over — not by redefining
`ShipPose`, because the quest markers are laid out along the same forward axis.

Which sprite is drawn is `IShipView.AssetKey`, set by whatever owns the player's ship from that
ship's own asset key rather than named at the draw site, so the picture follows the ship the
player was awarded. Changing it takes effect on the next draw.

`StarField` is what the ship flies through, drawn before it so the ship stacks over it. Because
the camera holds the ship in the middle of the viewport, the ship never moves on screen; the star
field is the fixed reference the player reads motion against, and without it full thrust looks
exactly like a standstill. It follows the *camera*, not the ship — nothing in it knows a ship
exists — so it stays correct the day something else is being followed.

The field is derived rather than stored. The world is unbounded, so a list of star positions would
run out; each `StarLayer` is instead sown on a grid of square tiles of unbounded extent, and where
a star stands is a function of the tile it falls in. Only the tiles the viewport covers are
visited, which is what makes the field continuous in every direction with no seam and no star
winking out on screen, and what keeps the cost of a frame from growing with how far the player has
flown. A layer's `Parallax` is the share of the camera's movement it takes: one is fixed in the
world, and smaller values lag, which is what reads as depth rather than as a texture sliding past.
Stars are sized in pixels rather than world units — a point of light at an unreachable distance
should not become a disc when the world is zoomed into.

"No seam" holds within a stated bound, and the bound is worth knowing because breaking it looks
like it worked. `StarField.MaxTilesPerAxis` caps the tiles a frame visits per axis, so that a
wound-out zoom cannot put the frame into a loop measured in millions; past the cap the field is
clipped to a band around the camera and leaves a blank border. That is reached whenever the
viewport spans that many tiles of a layer — a joint condition on the viewport, the zoom **and**
the layer, not on the zoom alone. `StarField.Layers` refuses the half of it that is a mistake in
the layer, holding each tile size against `SmallestUsableTileSize`, so a layer too finely sown to
fill `WidestSupportedViewportInPixels` fails where it is written rather than drawing tens of
thousands of stars into a band. The other half is a limit on the camera: no bound on tile size can
stop a low enough `PixelsPerUnit` from spanning the cap. Note that it does *not* stop mattering at
that zoom — stars are sized in pixels, so they stay fully visible beside the blank border.

A tile size can also be too *large*, and it goes blank for the opposite reason: tiles wider than
the screen sow their stars further apart than the viewport, so the viewport can fall between them.
`LargestUsableTileSize` is half `WidestSupportedViewportInPixels`, which is the point at which a
whole tile is guaranteed to fall inside the viewport wherever the camera stands. Both bounds are
held against the *widest* supported screen rather than the narrowest, which is the conservative
direction at both ends: a layer is refused only when it could work on no screen at all.

Every float on a layer is checked for being a finite number before any of those bounds are
applied. An ordered comparison against `NaN` is false, so without that check a `NaN` clears every
range guard in turn — each one reading as though it covered the case — and is then drawn at a
`NaN` position, which is a blank screen reached through validation that reported the layer as
sound. Checking it up front is also what lets each message name the field that is wrong rather
than whichever derived bound happened to trip first.

`StarField` is registered as itself rather than behind an interface. Nothing outside the drawing
sets anything on it, so an interface would be a name for the container's benefit and no one else's.

## Screen flow

`BattleForceHost` wires company screen → menu screen → game screen. `IFrameTimeController`
filters frame time before the screen director sees it. Entering `GameScreen` begins or resumes
the session; its `Update` flies the ship, drives the proximity watcher over the ground that frame
covered, and hands the result to `IShipView` as a pose, once the session is ready. Its `Render`
points the camera at that pose and draws the ship.

## Known gaps

- Nothing binds a real input device. The ship flies from `IShipInput`, and the MonoGame host still
  resolves the engine's `NeutralShipInput`, so the shipping game has nobody at the controls.
- Nothing displays a quest title. There is no HUD or quest log; that is a separate `ENGINE`
  issue. There is no text rendering at all yet — no font is loaded and `IRenderer` draws sprites
  only.
- The company and menu screens draw nothing. They do not implement `IRenderable`, so they are
  still a black window with working logic behind it.
- Nothing selects a language. Translations are reachable only through the machine's own culture.
- There is no persistent record (experience, credits, quest history) separate from the saved
  position. See pillar 4 in `docs/DESIGN.md`.
