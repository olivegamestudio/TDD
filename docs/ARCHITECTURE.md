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
- `QuestState` / `QuestStateExtensions` — the lifecycle as a set of values and as an order. The
  members are declared in the order a quest passes through them, and `IsBehind` is the one place
  that order is read, so "further on" is a stated rule rather than an enum's declaration order
  relied on by accident. A value outside the lifecycle is behind nothing — it is not a point on
  the lifecycle at all, so it is refused rather than quietly dropped for standing behind
  something.
- `QuestLog` — the player's quests, republishing their events so subscribers listen in one place.
  `Capture`/`Restore` keep quest persistence a quest concern. `Capture` writes one entry per quest;
  `Restore` tolerates a file that does not, and says which entry wins.
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
written by name and read by name *only* — `JsonStringEnumConverter` is built with
`allowIntegerValues: false`, because left to itself it also reads numbers and does not check that
the number names a state at all. Reordering `QuestState` cannot silently change a save, and
`"State": 99` is treated as a save this build cannot read rather than loading as a quest stuck
outside its own lifecycle. `SaveGame`'s shape is the compatibility boundary — changing it changes
what older saves can be read back into.

A save the campaign has drifted from is tolerated in both directions: a quest the save knows but
this build no longer ships is ignored, and a quest added since the save was written starts from
the beginning. A quest entry naming *no* registered quest — a blank identifier as much as a dropped
one — is drift of the same kind and is skipped for the same reason: there is no quest to apply it
to, so refusing the file over it would discard the progress saved beside it. Refusal is for a save
this build cannot read, which is a state outside a quest's lifecycle or JSON that will not parse.

**A position is only progress beside the quest progress it was taken with.** Tolerating drift means
a readable save can restore no progress at all — every entry naming a quest the campaign dropped,
or naming no quest, or no entries. Its coordinates then place a player inside a campaign nobody has
begun, and quest 1's start trigger is 25 units wide around the marker a new game spawns on: a player
set down 700 units out has nothing active, nothing to fly towards, and gets further from the only
trigger that could help with every frame of flying forward. So `GameSession.Continue` uses the saved
position only when at least one registered quest came back started or completed; otherwise the
player begins where a new game begins. The file itself is still accepted, and deliberately —
declining the position leaves it on disk, where refusing it would have it set aside and replaced.
The line costs a save taken after the player has travelled but before any quest has begun; none can
be written while the first quest starts where the player spawns, and a campaign that changes that
has to revisit this.

**A save that names one quest twice restores it to the furthest of the states it names.** A later
entry is applied only if it carries the quest further on; one that would hand progress back is
dropped, so a file saying a quest is both completed and never started comes back completed
whichever line is first. `QuestState`'s members are declared in the order a quest passes through
them and `QuestStateExtensions.IsBehind` reads that order, so it is a stated rule rather than a
consequence of which entry `Restore` happened to apply last.

That rule is chosen over first-wins or last-wins because it is the only one whose answer does not
depend on the order the entries are in. `QuestLog.Capture` emits one entry per registered quest, so
no build writes a duplicate — a duplicate means a hand-edited or merged file, and the order two
entries ended up in after a merge says nothing about which is right. It is also the only reading
that cannot lose progress the player really made, which is what pillar 4 in `docs/DESIGN.md` asks
of anything holding the record.

`QuestLog.Register` refuses a duplicate identifier outright and that is not the same answer, on
purpose. `Register` reads a campaign the build is authoring, where two quests under one identifier
is a mistake caught where it is made; `Restore` reads a file the build did not necessarily write,
where two entries under one identifier are one quest described twice. Refusing there would cost the
player everything saved beside it. The rule holds within a single `Restore`; a later call is a
different save being read, and it is authoritative.

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

### A redirect chain ends, one way or the other

`IActivatable.Enter` returns either `EnterResult.Stay` or `EnterResult.RedirectTo(other)`, and
`LifecycleScreenDirector` keeps entering until a screen stays — so one `NavigateTo` can pass
through several screens before it settles. (`ScreenDirector` does not follow redirects at all;
none of this applies to it.)

**A screen may not be entered twice during one navigation.** Redirect targets come from game code
and each screen's `Enter` is written in isolation, so a cycle is authored by accident rather than
on purpose: a title screen redirecting to the menu when a save exists, a menu redirecting back when
the save turns out to be unreadable. Neither screen is wrong on its own; the cycle exists only in
the pair. Followed without a bound, that call never returns — the update loop stops ticking and the
game hangs on a black screen with no exception and no log, which is the worst shape a failure can
take. `NavigateTo` instead throws `InvalidOperationException` naming the path in entry order
(`A -> B -> A`) the moment a screen would be entered a second time.

The bound is per navigation, not for the life of the director: navigating back to a screen visited
by an earlier call is ordinary and unrestricted, and a redirect back to the screen the navigation
*came from* is not a cycle either, because that screen was exited rather than entered on this pass.
A chain that would have settled on the second entry is rejected along with the ones that would not —
deliberately, since at the moment of the repeat the two cannot be told apart, and whether it settles
depends on state the screens mutate inside `Enter`.

Screens are compared by reference. Two instances of one screen type are two different screens, and
a screen written as a record does not collide with an equal-valued sibling — value equality would
report a cycle that is not there.

When the cycle is detected the navigation is abandoned rather than half-applied: the screen entered
last is exited, and `Current` is left `null`. That keeps every `Enter` paired with an `Exit`, and
`Current == null` is a state `Update` already handles, so nothing goes on ticking a screen that had
asked to be redirected away from. The director is still usable afterwards — the next `NavigateTo`
starts clean.

## Menu input

`OliveGameStudio.UI` drives buttons, not the ship. A button press is two events with a gap between
them: `Press` arms the hold, `Release` commits it, and `Cancel` abandons it.

**The press belongs to the button that was pressed.** `Held` follows that button and not the focused
one, because a pressed action is free to move focus while the player is still holding the key —
`FocusOn` directly, or `Disable`, which re-homes focus as a side effect. Disabling a button as it
activates, so it cannot fire twice, is an idiom the menu already uses, so this is the common case
rather than an exotic one. `IUIController` is registered as a singleton and every screen's buttons
share one node list, so a press that drifted off its button could commit one belonging to a screen
that is not even current.

`Press` also arms the hold *before* running the pressed action, so an action that calls `Cancel`
abandons the press it was invoked by; arming afterwards would overwrite that decision.

**Enablement is read at release, not at press.** Disabling a held button therefore suppresses its
commit without cancelling the press, and re-enabling it before the player lets go restores it.
`Disable` governs whether a button may act; `Cancel` abandons a press. Conflating them would leave
no way to say the first without also saying the second.

## Known gaps

- Nothing moves the ship. Quest 1 is completable by test, not by playing — movement and physics
  are issue #3.
- Nothing displays a quest title. There is no HUD or quest log; that is a separate `ENGINE`
  issue. `IGameSession.SaveError` is unread for the same reason — there is nowhere to say it.
- Nothing selects a language. Translations are reachable only through the machine's own culture.
- There is no persistent record (experience, credits, quest history) separate from the saved
  position. See pillar 4 in `docs/DESIGN.md`.
