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

The engine's `ISaveProgressService` exposes `HasProgress`, `Load`, `Save` and `SetAside`, all in
terms of text. `SaveGame` and `SaveGameSerializer` in `BattleForce2249.Game` decide what that text
is.

Reading is deliberately forgiving: a missing, blank or damaged save deserialises to `null` and is
reported as "no save", so a corrupt file yields a new game rather than a crash. Quest states are
written by name, so reordering `QuestState` cannot silently change a save. `SaveGame`'s shape is
the compatibility boundary — changing it changes what older saves can be read back into.

`SaveGameSerializer` is the only place that decides whether a save can be read, and "can be read"
means more than "parsed". Content that is well-formed JSON but not a game is refused there too: a
quest state outside `QuestState`, a quest entry that is absent or names no quest, or a coordinate
that is not a finite number. All of these used to be handed on as valid and then throw or brick
further in, where the player's only symptom was a game that had quietly stopped. Deciding once, at
the edge, is what lets everything downstream be written against a save that makes sense.

A save the campaign has drifted from is tolerated in both directions: a quest the save knows but
this build no longer ships is ignored, and a quest added since the save was written starts from
the beginning.

**A refused save is kept, not destroyed.** `Continue` starts a new game when the serializer
refuses one, and `StartNewGame` writes a save immediately — so until the new game is written, the
refused file is the only copy there is. `Continue` calls `ISaveProgressService.SetAside` first,
which moves it beside the save as `save.json.unreadable` and leaves no save in its place.

This exists because every rule on that boundary is a judgement, and the ones above are drawn over
files real campaigns are stored in. Without it a rule a shade too strict does not merely decline
to resume a save — it destroys it on the next launch, and no later build can revisit the decision.
That is also why the reading rules can afford to be as exact as they are: a mistake is now
recoverable rather than final.

Moving the file lives in `LocalSaveProgressService` rather than in `BattleForce2249`, because
where the save lives is the storage's business and the game is not to reach at the file system.
Which content is unusable stays the game's judgement — the engine never learns what a save
contains. One generation is kept: a second refusal replaces the file kept for the first, which is
a real loss in the rare case both matter, and is pinned by a test that says so.

If the file cannot be moved — a folder that permits writes but not renames does exactly this —
the session gives the player a playable game but holds every write back and reports the failure
through `SaveError`, rather than overwriting a save it could not first rescue. That is the same
answer `Continue` already gives for a save it could not read, for the same reason: a game that
cannot be saved costs the player this session, and a save overwritten costs them every session
before it.

`GameSession.Continue` recovers from storage getting in the way — a file locked, missing or barred
— and nothing wider, because catching wider would bury real defects behind "could not save". That
narrowness is only safe because the serializer refuses a save the rest of the load could choke on.
`GameScreen` logs anything that still escapes, so a game that fails to start is heard rather than
merely stopped.

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

- Nothing moves the ship. Quest 1 is completable by test, not by playing — movement and physics
  are issue #3.
- Nothing displays a quest title. There is no HUD or quest log; that is a separate `ENGINE`
  issue.
- Nothing selects a language. Translations are reachable only through the machine's own culture.
- There is no persistent record (experience, credits, quest history) separate from the saved
  position. See pillar 4 in `docs/DESIGN.md`.
