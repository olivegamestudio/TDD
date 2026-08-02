# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **The `Pilgrimage` quest system.** A standalone quest library with no project references:
  `QuestDefinition` and `QuestTrigger` for authored content, `Quest` for the
  `NotStarted → Active → Completed` lifecycle, `QuestLog` for the player's quests and their
  persistence, and `ICampaign` as the seam a game supplies quests through. The model declares
  what fires a quest; measuring it belongs to the presentation. ([#1](https://github.com/olivegamestudio/TDD/issues/1), [#2](https://github.com/olivegamestudio/TDD/pull/2))
- **Quest 1, "Get out. The debris field is collapsing around you."** Auto-starts on a new game,
  and completes when the player reaches the exit marker 1000 units forward. Both triggers are
  proximity triggers, sized so a ship travelling at speed still fires them. ([#1](https://github.com/olivegamestudio/TDD/issues/1))
- **`OliveGameStudio.World`** — `Position` and `Player`, the X/Y spatial model. The player exposes
  `MoveTo`/`MoveBy` only, leaving control input and physics a separate concern that drives it. ([#2](https://github.com/olivegamestudio/TDD/pull/2))
- **`IWorld` and `QuestProximityWatcher`** in the game — where each quest's markers stand, and the
  per-frame measurement that drives the quest API from the player's position. ([#2](https://github.com/olivegamestudio/TDD/pull/2))
- **Saved games.** `SaveGame` and `SaveGameSerializer` persist the player's position and every
  quest's state. Quest states are written by name — and read by name only — so neither reordering
  the enum nor a number in the file can silently change a saved state, and a missing or damaged
  save reads back as "no save" so a corrupt file yields a new game rather than a crash.
  `GameSession` saves when a quest starts or completes rather than every
  frame. ([#1](https://github.com/olivegamestudio/TDD/issues/1))
- **`IGameSession.SaveError` and `IGameSession.IsSavingProgress`** report whether the player's
  progress is being kept and what went wrong when it is not, so the state is readable rather than
  guessed at. Nothing displays them yet — that needs a HUD. ([#1](https://github.com/olivegamestudio/TDD/issues/1), [#2](https://github.com/olivegamestudio/TDD/pull/2))
- **`ISaveProgressService.SetAside()`** — moves saved progress somewhere it could be recovered from
  and leaves none behind, so a game that cannot resume from its save has an option other than
  writing over it or giving up on saving. Still text in, text out: the engine never learns what a
  save contains, and whether the content is worth keeping stays the game's call.
  `LocalSaveProgressService` puts it beside the save with `.corrupt` before the extension —
  `save.json` becomes `save.corrupt.json`, one generation kept — and exposes `SetAsideFilePath`,
  because "where did my game go" is a question it is otherwise the only thing able to
  answer. ([#46](https://github.com/olivegamestudio/TDD/issues/46), [#61](https://github.com/olivegamestudio/TDD/pull/61))
- **`QuestStateExtensions.IsBehind`** — reads `QuestState` as the order a quest passes through
  rather than as a set of values, so "further on" is something a caller can ask rather than an
  enum's declaration order relied on by accident. `QuestState` now says its members are declared in
  lifecycle order and that the order is load bearing; saves are unaffected, because they carry the
  name and never the ordinal. Used to resolve a save that names one quest twice, below. ([#72](https://github.com/olivegamestudio/TDD/issues/72), [#82](https://github.com/olivegamestudio/TDD/pull/82))
- **`OliveGameStudio.Localisation`** — `ITextProvider` and a JSON-backed `JsonTextProvider` owning
  the culture fallback chain, caching and the missing-key policy. A language is a file named after
  its culture; fallback is applied per key, and a key present in no language throws
  `MissingTextException` rather than showing the player a key. ([#2](https://github.com/olivegamestudio/TDD/pull/2))
- **Quest 1's title in seven languages** — English (source), French, Italian, German, Spanish,
  Brazilian Portuguese and Japanese, shipped as `Text/<culture>.json` beside the game. Adding a
  language is a file drop: no rebuild, no satellite assembly. ([#1](https://github.com/olivegamestudio/TDD/issues/1))
- **Project documentation** — this changelog, a README, and the design canon, architecture notes
  and workflow under `docs/`.

### Changed

- **`ISaveProgressService` gained `Load` and `Save`**, both in terms of text, so the engine stays
  agnostic about what a save contains and the same service works for any game built on it. ([#2](https://github.com/olivegamestudio/TDD/pull/2))
- **`BattleForceCampaign.Quests` is built on each read** rather than in a field initialiser. The
  campaign is a DI singleton, so a cached list froze the player's language at startup. It is read
  only when a game starts or resumes, so it is not on the frame path. ([#2](https://github.com/olivegamestudio/TDD/pull/2))

### Fixed

- **Comparing a save whose quest list is null no longer throws.** `SaveGame.Quests` is declared
  non-nullable, but nothing enforced it, so a snapshot built with `Quests = null!` — or read from a
  file saying `"Quests": null` before `SaveGameSerializer` patched it up — reached `Equals` and
  `GetHashCode`, where `SequenceEqual` and `.Count` threw on it. The list is now normalised where it
  is set rather than where it is read: `Quests` takes a null as no quests, so the declaration holds
  for every reader instead of each one defending itself, and a save with no quests is written as an
  empty array rather than as null. The serializer's own null patch went with it, being the same rule
  stated twice. ([#99](https://github.com/olivegamestudio/TDD/issues/99))
- **Two buttons with the same name are two buttons.** `Element` and its kinds are now classes
  rather than records, so `==` is identity. `Button` was a record, which made `==` value equality
  on the name, and `UIController` resolves every button through `==` — so with the controller
  registered as a singleton, two screens each labelling a button `BACK` shared one node. One
  screen's `Disable` greyed out the other's button and one screen's `OnReleased` overwrote the
  other's handler, silently. `Add` now also rejects a button it already holds, since the second
  node it used to create could never be reached — but never a button that merely shares a name with
  one it holds, which is the case the rule exists for. The visible change for callers is that a
  button the controller does not hold is now an error rather than a misdirection: `OnPressed`,
  `OnReleased`, `Press`, `Enable`, `Disable`, `IsEnabled` and both ends of `Link` throw
  `InvalidOperationException` instead of acting on the namesake. `FocusOn` is the exception — it
  does not check, so focus aimed at an unmanaged button surfaces at the next `Press`.
  ([#13](https://github.com/olivegamestudio/TDD/issues/13), [#27](https://github.com/olivegamestudio/TDD/pull/27))
- **A save naming one quest twice no longer undoes the progress it also records.** `QuestLog.Restore`
  applied entries in order, so the last one won by accident of iteration — a file holding `quest-1`
  as both `Completed` and `NotStarted` handed the player back a campaign they had finished. It now
  restores the **furthest** of the states an entry names: a later entry counts only if it carries
  the quest further on. That is the only reading whose answer does not depend on the order of the
  entries, which matters because `Capture` writes one entry per quest — a duplicate means a
  hand-edited or merged file, where the order says nothing about which entry is right. Stated on
  `QuestState`, whose declaration order is now load bearing, and readable through the new
  `QuestStateExtensions.IsBehind`. A state outside the lifecycle is still refused wherever it
  appears. ([#72](https://github.com/olivegamestudio/TDD/issues/72), [#82](https://github.com/olivegamestudio/TDD/pull/82))
- **A save the game could not read no longer freezes it, and is no longer overwritten.**
  `Continue` documented a fallback to a new game when the save "cannot be read" but only recovered
  from damaged *content*; the read itself was unguarded, so an `IOException` from a file locked by
  cloud sync came straight out into a task nothing holds — the session never became ready, every
  frame turned straight back, and the game had quietly stopped with no crash and no log. Damaged
  and unreadable are now distinguished: a damaged save is replaced, while an unreadable save is
  played over **without being written to**, because it may be perfectly intact. (What happens to
  the damaged one it replaces is settled below, by
  [#46](https://github.com/olivegamestudio/TDD/issues/46).) A save that cannot be *written* no longer stops the game either, and leaves
  saving on so the next quest tries again. Only `IOException` and `UnauthorizedAccessException`
  count as storage getting in the way — anything else is a defect and is still
  thrown. ([#1](https://github.com/olivegamestudio/TDD/issues/1), [#2](https://github.com/olivegamestudio/TDD/pull/2))
- **A save the game refuses is no longer written over by the new game that replaces it.**
  `Continue` fell back to `StartNewGame`, which saves immediately, so the first thing the game did
  after refusing a file was write over it — and refusing is a judgement, not a measurement. A
  coordinate just outside the world's edge or a quest entry read more strictly than `QuestLog`
  reads it cost the player the game they were trying to continue, permanently, and no later fix
  could give it back. The refused content is now set aside first and the new game saved only after,
  so a shape a later build reads perfectly well is still on disk; if it cannot be moved, the player
  gets a playable game with saving held back rather than the file destroyed, which is the same
  answer a save that could not be *read* already gets. Nothing is set aside for an absent or blank
  save, so a first-time player is not left a file that looks recoverable and is
  not. ([#46](https://github.com/olivegamestudio/TDD/issues/46), [#61](https://github.com/olivegamestudio/TDD/pull/61))
- **A quest state that is not a state no longer bricks the quest.** `JsonStringEnumConverter` reads
  numbers as well as names and does not check that the number names a defined member, so
  `"State": 99` deserialised to `(QuestState)99` — a quest that could never start and never
  complete, dead for the rest of the game. Refused at both layers: the serializer reads states by
  name only (`allowIntegerValues: false`), so a numeric state is treated as a save this build
  cannot read, and `Quest.Restore` rejects an undefined state with `ArgumentOutOfRangeException`
  rather than holding one. ([#1](https://github.com/olivegamestudio/TDD/issues/1), [#2](https://github.com/olivegamestudio/TDD/pull/2))
- **`GameScreen` no longer drops the task that begins the game**, logging a failed start through
  `ILogger` before rethrowing. The session recovers from the save failures that are expected, so
  anything reaching the log is a defect — but one somebody can now
  see. ([#1](https://github.com/olivegamestudio/TDD/issues/1), [#2](https://github.com/olivegamestudio/TDD/pull/2))
- **`LocalSaveProgressService` now really persists**, writing to a file and creating the save
  folder on first write. `HasProgress()` was a hardcoded `return true`, reporting progress it had
  never stored. ([#2](https://github.com/olivegamestudio/TDD/pull/2))
- **Text assertions no longer depend on the machine's language.** A screen test compared a quest
  title against an English literal with no culture pinned, failing the whole suite on any machine
  not running in English. ([#2](https://github.com/olivegamestudio/TDD/pull/2))
- **A press now stays with the button that was pressed.** `UIController.Press` armed the held
  button *after* running the pressed action, by re-reading the focused element — so an action that
  moved focus, including the codebase's own idiom of disabling a button as it activates, handed the
  press to an unrelated button whose released action then fired on release. The same ordering meant
  a pressed action could not `Cancel()` the press it started. `Held` now follows the pressed button
  rather than the focused one, and the surrounding rule is pinned from both sides: enablement is
  read at release, so disabling a held button withholds its commit without cancelling the press, and
  re-enabling it before the player lets go restores
  it. ([#12](https://github.com/olivegamestudio/TDD/issues/12), [#18](https://github.com/olivegamestudio/TDD/pull/18))
- **A redirect cycle now fails loudly instead of freezing the game.**
  `LifecycleScreenDirector.NavigateTo` followed an `EnterResult.Redirect` chain with no bound, so
  two screens redirecting at each other — or one redirecting to itself — spun inside the call
  forever and the update loop never ticked again. It now throws `InvalidOperationException` naming
  the path the moment a screen would be entered twice in one navigation, exits the live screen and
  leaves nothing current. The bound is per navigation — going back to a screen visited earlier is
  ordinary — and screens are compared by reference, so two instances of one type, or two records
  equal by value, are two different screens rather than a
  cycle. ([#14](https://github.com/olivegamestudio/TDD/issues/14), [#18](https://github.com/olivegamestudio/TDD/pull/18))
