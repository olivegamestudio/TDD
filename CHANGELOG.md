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
- **`BattleForceCampaign.Quests` is built on each read** rather than in a field initialiser. The
  campaign is a DI singleton, so a cached list froze the player's language at startup. It is read
  only when a game starts or resumes, so it is not on the frame path. ([#2](https://github.com/olivegamestudio/TDD/pull/2))

### Fixed

- **One junk quest entry no longer discards the progress saved beside it.** `SaveGameSerializer`
  refused a whole save when any quest entry had a missing, `null` or blank id, so a file holding a
  completed campaign and one line naming no quest started a new game — and, because a refused save
  is overwritten by the game that replaces it, lost the campaign for good. An entry naming no quest
  is now dropped and the rest of the file read, which is the answer `QuestLog.Restore` already gave
  for the same input; `Restore` skips a `null` id on the same grounds it already skipped a blank
  one. An entry that is `null` is not drift and is still refused whole. ([#44](https://github.com/olivegamestudio/TDD/issues/44))
- **A save that restores no quest progress no longer strands the player where its coordinates
  say.** Tolerating drift made such files readable, and reading one handed the player its position
  as well as its empty progress — a thousand units past a start trigger 25 units wide, with no
  quest active and the direction the campaign teaches leading further away every frame.
  `GameSession.Continue` now uses the saved position only when at least one registered quest came
  back started or completed, and otherwise begins the player where a new game begins. The file is
  still accepted rather than refused, so it is left on disk instead of being overwritten. ([#44](https://github.com/olivegamestudio/TDD/issues/44))
- **`LocalSaveProgressService` now really persists**, writing to a file and creating the save
  folder on first write. `HasProgress()` was a hardcoded `return true`, reporting progress it had
  never stored. ([#2](https://github.com/olivegamestudio/TDD/pull/2))
- **Text assertions no longer depend on the machine's language.** A screen test compared a quest
  title against an English literal with no culture pinned, failing the whole suite on any machine
  not running in English. ([#2](https://github.com/olivegamestudio/TDD/pull/2))
