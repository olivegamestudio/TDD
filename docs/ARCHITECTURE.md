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
  because the player already lived those moments — but it refuses a state that is not one of the
  states a quest has, with `ArgumentOutOfRangeException`. A quest holding an undefined state can
  neither start nor complete and nothing downstream would report it, so a restore that would brick
  the quest fails where it happens rather than somewhere the player discovers it. Pilgrimage is a
  library other games will use; it should not quietly hold a state that does not exist.
- `QuestLog` — the player's quests, republishing their events so subscribers listen in one place.
  `Capture`/`Restore` keep quest persistence a quest concern.
- `ICampaign` — the seam a game supplies quests through.

The game side supplies where things actually are:

- `IWorld` / `BattleForceWorld` — the player's start position and each quest's `QuestMarkers`.
  Forward travel is along the positive Y axis.
- `QuestProximityWatcher` — measures the ground the player covered each frame against the markers
  and calls the quest API when a trigger fires. It keeps no memory of what it has already fired,
  nor of where the player was; the quest model absorbs repeat calls, and the caller has both ends
  of a frame's travel to hand.
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
  An axis that cannot be read at all — `NaN` — is neutral, not clamped: `Math.Clamp` returns `NaN`
  unchanged, and one `NaN` frame would otherwise reach the heading, the velocity and the position
  and never leave, so the ship would stop for good and every quest distance after it would be
  `NaN`. A pilot who cannot be read is a pilot asking for nothing.
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
`SecondsForAFullCircle` is `τ / TurnRate` for the same reason, and it is not hypothetical: the
turn rate is stored in radians a second but is chosen for how quickly the ship comes about, and
the number, a test asserting a time and a comment describing the feel drifted into three different
claims about the same ship. Ask for the derived figure rather than writing a second one down.

`GameScreen.Update` flies the ship **before** it measures the quests, so a trigger fires on the
frame it is reached rather than the frame after — at 200 units a second, a frame late is a marker
the player has already passed. Entering the screen resets the ship: the save carries where the
player is, never how fast they were going.

## Saved progress

The engine's `ISaveProgressService` exposes `HasProgress`, `Load` and `Save`, all in terms of
text. `SaveGame` and `SaveGameSerializer` in `BattleForce2249.Game` decide what that text is.

Reading is deliberately forgiving: a missing, blank or damaged save deserialises to `null` and is
reported as "no save", so a corrupt file yields a new game rather than a crash. Quest states are
written by name and read by name *only* — `JsonStringEnumConverter` is built with
`allowIntegerValues: false`, because left to itself it also reads numbers and does not check that
the number names a state at all. Reordering `QuestState` cannot silently change a save, and
`"State": 99` is treated as a save this build cannot read rather than loading as a quest stuck
outside its own lifecycle. `SaveGame`'s shape is the compatibility boundary — changing it changes
what older saves can be read back into.

A save the campaign has drifted from is tolerated in both directions: a quest the save knows but
this build no longer ships is ignored, and a quest added since the save was written starts from
the beginning.

### A save that cannot be read is not a save that is gone

These are two different failures and `GameSession` does not treat them the same. The distinction
matters because a save locked for a moment by cloud sync or antivirus is probably still perfectly
intact, and starting a new game on top of it is its own kind of data loss — the one pillar 4 in
`docs/DESIGN.md` calls a design failure rather than a robustness one.

| The save is… | What happens |
| ------------ | ------------ |
| absent | a new game, saved normally |
| **damaged** — unparseable, or a state that is not a state | a new game, saved normally: the content is already gone, so replacing it loses nothing |
| **unreadable** — locked, or barred by permissions | a playable game that **does not write over the save**, which may be intact |

The session reports which it is rather than leaving it to be guessed at:

- `IGameSession.SaveError` — the exception from the last read or write, or `null` when the save is
  healthy.
- `IGameSession.IsSavingProgress` — whether quest progress is actually being written. It is
  `false` for the stand-in game played over an unreadable save.

A save that cannot be *written* does not stop the game either. Automatic saves go through an
internal `TrySave` that records the failure and **leaves saving on**, because the file may well be
free again by the next quest; giving up for the rest of the session would cost the player more
than the one write that failed. The public `Save()` still raises, for callers that want to know.

Only `IOException` and `UnauthorizedAccessException` count as the storage getting in the way.
Anything else is a defect and is still thrown — catching wider would bury real bugs behind a
"could not save" message.

Nothing reads `SaveError` yet. Telling the player their save is locked needs a HUD, which does not
exist; the information is recorded so that whatever gains the ability to say it has something to
read.

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

## Quest triggers are swept, not sampled

A proximity trigger is measured against the whole of the ground a frame covered, not the point the
frame finished on. `Position.ClosestApproachTo` measures a marker against the *segment* the player
travelled — clamped to its ends, so a marker beyond either one is measured to that end and the
trigger is never widened sideways into the line beyond the journey.

Sampling the end point alone fired a trigger only when a frame happened to *land* inside it, so a
frame carrying the ship from just outside one side of a marker to just outside the other flew
straight through it. Pillar 1 in `docs/DESIGN.md` calls that a bug rather than a tuning detail, and
until this landed the only thing preventing it was how generous the authored distances happened to
be — a stalled frame, a faster ship or a tighter trigger authored for a small object each brought
it back, and each would have been diagnosed as a quest that mysteriously sometimes does not start.

Two things follow from sweeping that sampling never had to answer:

- **Both of a quest's markers can be passed in one frame.** A frame long enough to fly the whole
  debris field starts quest 1 *and* completes it, rather than skipping it. That is a quest played
  in one frame, not a quest given away.
- **So the markers are applied in the order they were reached.** `ClosestApproachTo` reports how
  far into the journey the closest approach happened, and a quest started within a frame is only
  completed by an exit marker passed no earlier in that journey than its start marker was.
  Otherwise one long frame flown *backwards* across the field would finish a quest on the strength
  of a leg flown before it began. A quest already running when the frame opened has been running
  for all of it, so its exit marker counts wherever along the journey it was passed — reversing
  onto a marker is still reaching it.

Nothing remembers anything. `GameScreen.Update` reads the position before it flies the ship and
passes both ends to the watcher, so neither the screen nor the watcher carries state between
frames. A remembered previous position would be wrong the moment the player is *placed* rather
than flown — entering the screen, or resuming a save — because the next frame would then sweep a
phantom journey from wherever the last game ended and fire every trigger along a line nobody
travelled.

`Pilgrimage` is untouched by all of this. The quest model declares the rule and holds no
coordinates; measuring is the presentation's job.

## Screen flow

`BattleForceHost` wires company screen → menu screen → game screen. `IFrameTimeController`
filters frame time before the screen director sees it. Entering `GameScreen` begins or resumes
the session; its `Update` drives the proximity watcher once the session is ready.

Loading happens off the frame loop, and nothing in the shipping game holds the task `Enter()`
starts — so `GameScreen` logs a failure through `ILogger` before rethrowing rather than dropping
it. The session already recovers from the save failures that are expected, so anything reaching
the log is a defect; the point is that it is a defect somebody can see. A failure swallowed here
leaves the session never ready, every frame turning straight back, and the player in front of a
game that has quietly stopped.

## Known gaps

- Nothing binds a real input device. The ship flies from `IShipInput`, and the MonoGame host still
  resolves the engine's `NeutralShipInput`, so the shipping game has nobody at the controls.
- Nothing displays a quest title. There is no HUD or quest log; that is a separate `ENGINE`
  issue. `IGameSession.SaveError` is unread for the same reason — there is nowhere to say it.
- Nothing selects a language. Translations are reachable only through the machine's own culture.
- There is no persistent record (experience, credits, quest history) separate from the saved
  position. See pillar 4 in `docs/DESIGN.md`.
