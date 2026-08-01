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
| `OliveGameStudio.World` | `Position`, `Player`. The spatial model. |
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
  quest API when a trigger fires. It keeps no memory of what it has already fired; the quest
  model absorbs repeat calls.
- `GameSession` — the game in progress. Starts or resumes, and saves when a quest starts or
  completes rather than every frame.

Ids are never translated. A save written in one language has to load in another.

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
`ShipPose` — a position and a heading — and the view draws whatever it last said. A heading of
zero is straight forward, up the screen, and the angle increases to starboard, which matches the
artwork's own orientation and lets the heading pass through to the sprite untouched. The sense of
that angle is a convention rather than something the type can enforce; a model that measures its
heading the other way round negates it on the way in.

## Screen flow

`BattleForceHost` wires company screen → menu screen → game screen. `IFrameTimeController`
filters frame time before the screen director sees it. Entering `GameScreen` begins or resumes
the session; its `Update` drives the proximity watcher once the session is ready.

## Known gaps

- Nothing moves the ship. Quest 1 is completable by test, not by playing — movement and physics
  are issue #3.
- Nothing displays a quest title. There is no HUD or quest log; that is a separate `ENGINE`
  issue. There is no text rendering at all yet — no font is loaded and `IRenderer` draws sprites
  only.
- Nothing but the ship is drawn. The background is a flat clear, so with the camera following the
  ship there is no fixed reference to read motion against; a star field is issue #25.
- The company and menu screens draw nothing. They do not implement `IRenderable`, so they are
  still a black window with working logic behind it.
- Nothing selects a language. Translations are reachable only through the machine's own culture.
- There is no persistent record (experience, credits, quest history) separate from the saved
  position. See pillar 4 in `docs/DESIGN.md`.
