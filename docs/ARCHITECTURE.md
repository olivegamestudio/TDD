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
- `QuestProximityWatcher` — measures the player against the markers each frame and calls the
  quest API when a trigger fires. It keeps no memory of what it has already fired; the quest
  model absorbs repeat calls.

**Quest triggers are swept, not sampled.** The watcher measures each marker against the *segment*
the player covered this frame, not the point the frame ended on. Sampling a point fires a trigger
only when a frame happens to land inside it, so a frame carrying the ship from just outside one
side of a marker to just outside the other fires nothing — a trigger flown straight through, which
`docs/DESIGN.md` pillar 1 calls a bug rather than a tuning detail. At today's numbers marker
tolerance hid it; a stalled frame, a faster ship or a tighter trigger authored for a small object
each brought it back. `Position.DistanceToSegment` does the measuring, clamped to the ends so a
sweep brings a marker nearer but never widens a trigger sideways.

**The sweep states no bound on how long a journey may be.** It never forms the journey's length,
because a length is a square and a double squares to infinity somewhere above 1.34e154 — and a
sweep that overflows does not report an error, it reports the ground it covered as a single point
at the start, which is exactly the point sampling the sweep replaced. The fraction along a journey
is a ratio of two quantities that both grow with the square of it, so it is measured at half size
and in units of the journey's own longer axis; the answer is the same and nothing formed along the
way leaves a double's range. Two finite ends and a finite marker therefore always give a fraction
between 0 and 1, never `NaN`, which the ordering rule above depends on: an ordered comparison
against `NaN` is false whichever way round it is written, so a `NaN` fraction would report that
neither marker came first and quietly finish nothing. How far out a position is allowed to be is a
separate rule kept at the save boundary, and the geometry does not lean on it.

The sweep keeps the order the ground was covered in. `Position.FractionAlongSegment` says how far
along a journey each marker was reached, and a quest that *starts* on a given frame only completes
on that same frame if its end marker was reached no earlier than its start marker. One frame flown
backwards across the whole field therefore starts the quest and leaves it in progress, rather than
finishing something the player reached the objective of before they reached its beginning. A quest
already under way is not asked — arriving at the objective completes it whichever way round the
player flew through.

Nothing remembers a previous position for this. `Player.TakeJourney` hands over the ground flown
since it was last called and begins a new journey from where the player is now, and `MoveTo` — a
new game, or a save resumed — ends the journey rather than extending it. That distinction has to
live on the player, because the player is the only thing that can tell having flown somewhere
apart from having been put there; a remembered position anywhere else would sweep a resumed game
across every marker between the origin and the save.
- `GameSession` — the game in progress. Starts or resumes, and saves when a quest starts or
  completes rather than every frame.

Ids are never translated. A save written in one language has to load in another.

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
| **damaged** — unparseable, or a state that is not a state | the file is **set aside**, then a new game saved normally |
| damaged, and it cannot be set aside | a playable game that **does not write over the save** |
| **unreadable** — locked, or barred by permissions | a playable game that **does not write over the save**, which may be intact |

#### A refused save is set aside, not written over

"Damaged" is a judgement, not a measurement. `SaveGameSerializer` decides what this build will
take, and a shape it refuses may be one a later build reads perfectly well — so while the new game
wrote straight over the file, every one of those judgements was final. A player who pressed
Continue lost the game they were trying to continue, and no later fix could give it back. That is
also what made each boundary in the serializer so expensive to get wrong.

So `ISaveProgressService.SetAside` moves the refused content out of the way first, and only then
does the new game save. `LocalSaveProgressService` puts it beside the save with `.corrupt` before
the extension — `save.json` becomes `save.corrupt.json`, one generation kept — so the player has
something a later build could read and support has the file that broke. The engine still learns
nothing about what a save contains: whether the content is worth keeping is the game's call, and
this only provides somewhere to put it.

If the file cannot be moved, the new game is played with saving held back rather than written over
the top. At that point preserving the old save and persisting the new one are in direct conflict,
and the irreversible one wins — the same answer, for the same reason, that a save which could not
be *read* already gets. Nothing is set aside when there was no save or a blank one, so a
first-time player is not left a file that looks recoverable and is not.

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

- Nothing moves the ship. Quest 1 is completable by test, not by playing — movement and physics
  are issue #3.
- Nothing displays a quest title. There is no HUD or quest log; that is a separate `ENGINE`
  issue. `IGameSession.SaveError` is unread for the same reason — there is nowhere to say it.
- Nothing selects a language. Translations are reachable only through the machine's own culture.
- There is no persistent record (experience, credits, quest history) separate from the saved
  position. See pillar 4 in `docs/DESIGN.md`.
