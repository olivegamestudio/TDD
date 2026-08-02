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
- **Ship movement and physics.** `ShipMovement` flies the ship from `ShipControls` — a thrust axis
  and a helm axis — carrying a heading and a velocity, and moving the player by the ground actually
  covered each frame. Thrust is applied along the heading and momentum survives a turn. Drag is
  integrated exactly rather than stepped, so a second of flight covers the same distance whether it
  arrived as one frame or a hundred and sixty. `ShipHandling` holds the tuning, and derives the top
  speed from the acceleration and drag rather than letting a third number disagree with them, and
  how long the ship takes to come about from the turn rate for the same reason. An axis a device
  cannot report — `NaN` — reads as hands off rather than propagating through the physics and
  leaving the ship permanently unflyable. ([#3](https://github.com/olivegamestudio/TDD/issues/3))
- **`IShipInput`** — the seam gameplay input arrives through, because the platform host owns the
  real device. The engine ships `NeutralShipInput` as the default, so a game composes and runs with
  nobody at the controls; a host registers its keyboard or gamepad after `AddOliveGameStudio`. ([#3](https://github.com/olivegamestudio/TDD/issues/3))
- **A keyboard and a gamepad at the controls.** The desktop host binds both through
  `AddDesktopPilot`, so Quest 1 is completable by a person rather than only by a test. W/A/S/D or
  the arrow keys, either hand alone; the gamepad's left stick for both axes, with a fifth of its
  travel as a dead zone so a worn stick does not fly the ship on its own. Both connected at once
  are arbitrated by `FirstActiveShipInput` — asked in order, gamepad first, the first device asking
  for anything winning outright rather than the two summing into something neither hand asked for.
  What a device means (`ShipControls.FromKeys`, `ShipControls.FromStick`) is engine code; which key
  and which stick is the host's. An axis a device cannot report reads as hands off rather than
  throwing on the frame path. ([#9](https://github.com/olivegamestudio/TDD/issues/9))
- **`BattleForceShip`** — the game's ship: 180 units per second per second against a drag of 0.9,
  settling at 200 units a second, and 4.5 radians a second at full helm — a little under a second
  and a half to come about. Quest 1's exit marker is a run of roughly six seconds at full
  burn. ([#3](https://github.com/olivegamestudio/TDD/issues/3))
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
- **`GameScreen` flies the ship before it measures the quests.** The order is load bearing: a
  quest measured against the previous frame's position fires a frame late, which at speed is a
  marker the player has already gone past. Entering the screen also brings the ship to rest, since
  the save carries where the player is and never how fast they were going. ([#3](https://github.com/olivegamestudio/TDD/issues/3))
- **`BattleForceCampaign.Quests` is built on each read** rather than in a field initialiser. The
  campaign is a DI singleton, so a cached list froze the player's language at startup. It is read
  only when a game starts or resumes, so it is not on the frame path. ([#2](https://github.com/olivegamestudio/TDD/pull/2))

### Fixed

- **A save the game could not read no longer freezes it, and is no longer overwritten.**
  `Continue` documented a fallback to a new game when the save "cannot be read" but only recovered
  from damaged *content*; the read itself was unguarded, so an `IOException` from a file locked by
  cloud sync came straight out into a task nothing holds — the session never became ready, every
  frame turned straight back, and the game had quietly stopped with no crash and no log. Damaged
  and unreadable are now distinguished: a damaged save is replaced, because its content is already
  gone, while an unreadable save is played over **without being written to**, because it may be
  perfectly intact. A save that cannot be *written* no longer stops the game either, and leaves
  saving on so the next quest tries again. Only `IOException` and `UnauthorizedAccessException`
  count as storage getting in the way — anything else is a defect and is still
  thrown. ([#1](https://github.com/olivegamestudio/TDD/issues/1), [#2](https://github.com/olivegamestudio/TDD/pull/2))
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
