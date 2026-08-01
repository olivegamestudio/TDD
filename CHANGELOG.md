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
  speed from the acceleration and drag rather than letting a third number disagree with them. ([#3](https://github.com/olivegamestudio/TDD/issues/3))
- **`IShipInput`** — the seam gameplay input arrives through, because the platform host owns the
  real device. The engine ships `NeutralShipInput` as the default, so a game composes and runs with
  nobody at the controls; a host registers its keyboard or gamepad after `AddOliveGameStudio`. ([#3](https://github.com/olivegamestudio/TDD/issues/3))
- **`DisgracedShip`** — the Disgraced's ship: 180 units per second per second against a drag of 0.9,
  settling at 200 units a second, and 2.5 radians a second at full helm. Quest 1's exit marker is a
  run of roughly six seconds at full burn. Named for the pilot rather than the game, and left
  without a `Ship` base type until a second ship exists to factor one out of.
  ([#3](https://github.com/olivegamestudio/TDD/issues/3),
  [#15](https://github.com/olivegamestudio/TDD/issues/15))
- **`IWorld` and `QuestProximityWatcher`** in the game — where each quest's markers stand, and the
  per-frame measurement that drives the quest API from the player's position. ([#2](https://github.com/olivegamestudio/TDD/pull/2))
- **Saved games.** `SaveGame` and `SaveGameSerializer` persist the player's position and every
  quest's state. Quest states are written by name so reordering the enum cannot silently change a
  save, and a missing or damaged save reads back as "no save" so a corrupt file yields a new game
  rather than a crash. `GameSession` saves when a quest starts or completes rather than every
  frame. ([#1](https://github.com/olivegamestudio/TDD/issues/1))
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

- **A ship travelling fast enough could fly straight through a quest trigger.**
  `QuestProximityWatcher` measured the player once a frame, at wherever the frame happened to end,
  so a trigger fired only when a frame *landed* inside it — a frame carrying the ship from just
  outside one side of a marker to just outside the other fired nothing. It now measures each marker
  against the segment the player travelled, through the new `Position.DistanceToSegment`, so a
  marker cannot be stepped over at any frame length. The segment is a segment and not the line
  through it, so this does not widen a trigger sideways, and both ends of the journey come from the
  caller within one frame, so nothing keeps state.
  ([#8](https://github.com/olivegamestudio/TDD/issues/8))
- **`LocalSaveProgressService` now really persists**, writing to a file and creating the save
  folder on first write. `HasProgress()` was a hardcoded `return true`, reporting progress it had
  never stored. ([#2](https://github.com/olivegamestudio/TDD/pull/2))
- **Text assertions no longer depend on the machine's language.** A screen test compared a quest
  title against an English literal with no culture pinned, failing the whole suite on any machine
  not running in English. ([#2](https://github.com/olivegamestudio/TDD/pull/2))
