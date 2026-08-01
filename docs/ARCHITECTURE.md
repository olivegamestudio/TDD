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
| `OliveGameStudio.Progress` | `LocalSaveProgressService`, persisting save text to a file. |
| `OliveGameStudio.World` | `Position`, `Player`, `Velocity`. The spatial model, and the ship physics that moves through it: `ShipMovement`, `ShipHandling`, `ShipControls`, `IShipInput`. |
| `OliveGameStudio.Localisation` | `ITextProvider`, `JsonTextProvider`, `MissingTextException`. |
| `Pilgrimage` | The quest system. No project references at all, by design. |
| `BattleForce2249` | Game host and DI registration. |
| `BattleForce2249.Abstractions` | Screen interfaces and options. |
| `BattleForce2249.Company`, `.Menu`, `.Game` | The three screens. |
| `BattleForce2249.MonoGame` | The MonoGame entry point, and the real keyboard and gamepad bound to `IShipInput`. |

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

## Flying the ship

The engine owns the physics; the game owns the numbers. `ShipHandling` is a game's acceleration,
drag and turn rate — `BattleForceShip.Handling` supplies the shipping ones — and `ShipMovement`
is the physics they are flown through. A better ship is another `ShipHandling`, not another
physics.

- `ShipControls` — a thrust axis and a helm axis, each clamped to `[-1, 1]`. Analogue, because
  that is the shape a stick and a key both fit: a key is a 1, a stick is whatever it is pushed to.
- `IShipInput` — where the pilot's intent comes from. The platform host owns the real device, so
  the engine ships the seam and `NeutralShipInput`, which asks for nothing. A host registers a
  keyboard or gamepad after `AddOliveGameStudio` and wins under the engine's `AddSingleton`.
- `FirstActiveShipInput` — several devices bound at once, reporting whichever is being used.
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
player is, never how fast they were going.

## At the controls

The bindings are split so that almost none of the work sits where it cannot be tested.

- **What a device means** is engine code and is tested. `ShipControls.FromKeys` turns four held
  keys into the two axes; opposite keys cancel rather than one winning, so the answer never
  depends on which key was checked first. `ShipControls.FromStick` applies a dead zone and
  stretches the travel past it back over the full range, so crossing the dead zone asks for a
  little rather than jumping to the dead zone's worth.
- **Which device answers** is `FirstActiveShipInput`: the devices are asked in order and the first
  one asking for anything wins outright. Two devices bound at once must never have their answers
  added together — a stick half over plus a held key would read as more than full thrust. The
  arbitration is per device rather than per axis, because splitting the axes is that same summing
  in a form that is harder to predict. It is stateless, so letting go of one device hands control
  straight to the other.
- **Which keys and which stick** is host wiring, in `BattleForce2249.MonoGame`.
  `KeyboardShipInput` binds W/A/S/D and the arrow keys as one set. `GamePadShipInput` binds the
  left stick for both axes, reading MonoGame with `GamePadDeadZone.None` so the dead zone is only
  applied once. A disconnected pad reports neutral and simply loses, so nothing has to check
  whether a controller turned up.

`AddDesktopPilot` **must be called after `AddBattleForce`** — the engine registers
`NeutralShipInput` with `AddSingleton`, so the last registration wins, and calling it first leaves
the shipping game with nobody flying it. A test pins that ordering in both directions.

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

## Screen flow

`BattleForceHost` wires company screen → menu screen → game screen. `IFrameTimeController`
filters frame time before the screen director sees it. Entering `GameScreen` begins or resumes
the session; its `Update` drives the proximity watcher once the session is ready.

## Known gaps

- No control scheme can be chosen. The keyboard and gamepad bindings are fixed in the host; there
  is no settings model or menu behind a chooser, and no strafing axis. That is issue #7.
- Reading the real devices is not covered by a test. `Keyboard.GetState` and `GamePad.GetState`
  are static calls into MonoGame with nothing to stand in front of them, so the few lines naming
  the keys and the stick axes are only confirmed by playing the game. Everything they feed is
  engine code and is tested.
- Quest triggers are sampled, not swept. `QuestProximityWatcher` measures the player once a frame,
  so a frame long enough to carry the ship further than a trigger's distance steps over it. Quest
  1's markers are sized clear of that at any playable frame rate, but the tolerance is the only
  thing preventing it.
- Nothing displays a quest title. There is no HUD or quest log; that is a separate `ENGINE`
  issue.
- Nothing selects a language. Translations are reachable only through the machine's own culture.
- There is no persistent record (experience, credits, quest history) separate from the saved
  position. See pillar 4 in `docs/DESIGN.md`.
