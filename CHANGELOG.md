# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **A ship carries two shields, and what a hit costs it now depends on them.** `ShieldStats` is the
  shield's stats type, authored as one of the two things a shield can do — `Absorbing(capacity)`
  contributes to the layer that depletes before health, `Reflecting(fraction)` bounces its share of
  an incoming hit back at whatever fired it. A shield does one or the other, so there is no
  constructor that would let a data file invent a third type by filling in both.

  `Shielding` is what is in the two slots, and **the stacking rule is that slots add up**: two of a
  kind double one effect, a mix gives both at single strength, and neither is written down as a
  special case. A rule phrased as "double when they match" would have to answer what happens when
  two shields nearly match. A pair reflecting more than the whole hit is held at the whole hit —
  each shield was authored on its own and neither is a mistake, but damage travelling back out
  larger than it arrived is.

  `Ship.Fit` puts shields in the slots and builds the shield layer from them, so a ship's shield
  maximum is no longer always zero. `Ship.TakeDamage` resolves a hit in the order the fiction
  implies — what is reflected never arrives, what does meets the shield layer, and only what the
  layer cannot hold reaches health — and returns a `DamageOutcome` whose three parts add back up to
  the hit. It *reports* the reflected damage rather than applying it: a ship knows what its shields
  turned around and has no business knowing what shot at it, so pairing that with an attacker
  belongs wherever the fight does.

  Nothing shoots yet and no content authors a shield, so the Disgraced still flies with empty slots
  — which is what the design asks for at the start of the story.
  ([#135](https://github.com/olivegamestudio/TDD/issues/135))

- **A sprite carries a colour, so a screen can fade or tint what it draws.** `Colour` is four
  channels from 0 to 1 — red, green, blue, and how much of it shows — and `Sprite.Colour` is
  opaque white until something says otherwise, which is what every existing caller keeps getting
  without mentioning one. `MonoGameRenderer` draws with it in place of the hard-coded white it
  used before.

  The colour is held *straight*: "red, half faded" is `(1, 0, 0, 0.5)`, not the premultiplied
  `(0.5, 0, 0, 0.5)`. `SpriteBatch` blends premultiplied by default and the content pipeline
  premultiplies the textures to match, so the multiply happens on the way to the device, in the
  one class that knows which device it is. A channel that is not a number is refused where the
  colour is built, for the reason the camera refuses a target that is not one: it would reach the
  device as whatever the conversion produced, draw in full without raising anything, and name
  neither the sprite nor the frame that produced it.
  ([#112](https://github.com/olivegamestudio/TDD/issues/112))

- **A character owns the game; a ship is what they are currently sitting in.** `CharacterTemplate`
  and `Character` pair authored content with the instance that plays it, the same way
  `QuestDefinition` and `Quest` already do: a character holds `Progression` (experience, level,
  spend points, gifts), credits, `Reputation` per group, an `Inventory` and their quest history, and
  survives changing ship. `ShipProfile` and `Ship` do the same for the hull — handling, loadout,
  and `Meter`s for health, shield and durability. `ICharacterRoster` / `BattleForceRoster` is the
  seam the game supplies characters through, and the Disgraced is the one it ships, flying
  `DisgracedShip`'s handling and starting in the mines. The split is pillar 4 as ownership: losing
  the ship cannot take the record with it.

  The ship is no longer a container singleton. `Ship` builds its own `ShipMovement` from its own
  handling, so the ship the player owns and the ship they fly cannot be given two different sets of
  numbers; `GameSession` provisions one per game and `GameScreen` reads `session.Ship`. That also
  removes a reset — entering the game screen used to bring the physics to rest, and a new game now
  simply builds a new ship, which has never been anywhere.

  `IWorld.Introduce(ship, location)` brings a ship into the world at a *named* place and answers
  where that put it, so content names places and never states coordinates. Nothing here is written
  to the save yet: `SaveGame` still carries position and quest state only.
  ([#5](https://github.com/olivegamestudio/TDD/issues/5))

- **The camera turns with the ship, so the ship always points forward.** `ICamera.Orientation` is
  the world heading held pointing up the screen, and `GameScreen` sets it from the ship's own
  heading every frame. `Camera2D.WorldToScreen` turns the world about the camera's target rather
  than about the world origin, so the point being followed keeps the middle of the viewport however
  far it turns — the ship is drawn where it is and the world rotates around it. `ShipView` draws
  the ship with the camera's turn taken off its heading, which is zero while the camera is tracking
  it and still correct for a camera that is not. The star field takes the turn into account when it
  decides which tiles to sow: a turned viewport's corners reach further along the world's axes than
  its edges do, and sowing the upright box alone drains the stars out of the corners of the window
  as the ship turns. An orientation that is not finite is refused where it is set, because its sine
  is `NaN` and every sprite in the world would otherwise be drawn, in full and without an error, at
  a position that is nowhere.
  ([#35](https://github.com/olivegamestudio/TDD/issues/35))

- **The player flies the ship.** `IHost.Input(InputFrame)` is the entry a platform host pushes one
  frame of device state through, once a frame, before `Update` — `KeyboardFrame` and `GamePadFrame`
  in one snapshot, so the game can compare devices rather than reading each where it happens to
  need one. `InputRouter` routes it: **UI first**, so a frame belongs to the menu while something
  there is focused and to the ship when nothing is, with the ship written `Neutral` on the frames
  it does not get. A press is treated as an edge, so a held key presses a button once and a confirm
  already held when a menu appears presses nothing. `RoutedShipInput` is where the router leaves
  the frame's controls for the physics to read, and is what the engine now registers as
  `IShipInput`. Before this, nothing carried a key press to the menu at all: the shipping game
  could not be started by a person.
  ([#7](https://github.com/olivegamestudio/TDD/issues/7))

- **The control device is locked at the start press.** The device that presses start is the device
  the game is played on, for the session — flight controls *and* menu. A pad left plugged in with
  something resting on the stick cannot take the ship from somebody flying it on the keys, and a
  second person cannot work the menu of a game they are not playing. It is taken at the press
  rather than the release, since the menu commits on release and enters the game screen from there.
  The cost is stated rather than hidden: a pad unplugged mid-flight leaves the ship hands off
  rather than handing it to a device the player did not choose.
  ([#117](https://github.com/olivegamestudio/TDD/issues/117))

- **`IUIController.HasFocus`** — the query input routing needs before it can decide who a frame's
  input belongs to. The interface could set focus but not be asked about it, and a router cannot
  remember what it last focused because `Add`, `Disable` and `Enable` all move focus on their own.
  It reports whether anything is focused and never which button holds it: naming the button would
  hand a consumer one belonging to a screen that is not current, and the routing decision does not
  need it. A focused button that is disabled still counts as focus, since disabled is a statement
  about pressing rather than about who is listening.
  ([#113](https://github.com/olivegamestudio/TDD/issues/113))

- **A keyboard and a gamepad at the controls.** The shipping game is flyable by a person rather
  than only by a test: quest 1 can be completed on W/S/A/D or the arrow keys, or on a gamepad's
  left stick. `ShipControls.FromKeys` translates held keys — opposite keys are summed, so they
  cancel and the answer never depends on which was read first — and `ShipControls.FromStick`
  applies a dead zone and stretches the remaining travel back over the full range, so a worn stick
  does not fly the ship while a stick at its stop still asks for everything.
  `FirstActiveShipInput` asks its devices in order and lets the first one asking for anything
  answer for the frame, arbitrating per device rather than per axis and holding no state, so
  putting one device down hands over on the next frame. `BattleForce2249.MonoGame` binds the
  devices through `AddDesktopPilot()`, after `AddBattleForce` so it wins over the engine's
  `NeutralShipInput`. ([#9](https://github.com/olivegamestudio/TDD/issues/9))

  *How the devices reach the game changed before this shipped — see the Changed entry for
  [#7](https://github.com/olivegamestudio/TDD/issues/7). The keys, the stick and the dead zone are
  the same; the arbitration is not.*

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
- **`Position.DistanceToSegment`** — the closest a position came to a journey between two others.
  Engine geometry, knowing nothing about quests: it is what lets a system that reacts to the
  player ask "how close did they get" rather than "where were they when the frame ended". The
  journey is rescaled before the arithmetic squares anything, so it holds at any magnitude rather
  than silently degrading to sampling one end above 1.3e154. ([#8](https://github.com/olivegamestudio/TDD/issues/8))
- **Project documentation** — this changelog, a README, and the design canon, architecture notes
  and workflow under `docs/`.
- **Continuous integration** — `.github/workflows/build.yml` builds and tests the whole solution,
  MonoGame host included, on every push to `main` and every pull request. Until it landed, nothing
  in this repository had ever been compiled by anything but the author of the change: pull requests
  carried no checks at all, and several were stacked on one another unbuilt. A failing test now
  fails the pull request. ([#34](https://github.com/olivegamestudio/TDD/issues/34))

### Changed

- **The desktop devices are pushed rather than pulled, and no longer arbitrate.** The `IShipInput`
  implementations the MonoGame host bound in [#9](https://github.com/olivegamestudio/TDD/issues/9)
  — `KeyboardShipInput` and `GamePadShipInput` — are now `DesktopKeyboard` and `DesktopGamePad`,
  which read the same keys and the same stick into an `InputFrame` the frame loop hands to
  `IHost.Input`. They gained the confirm binding a menu needs: Enter or Space, A or Start.
  `AddDesktopPilot()` no longer binds devices — it states this host's dead zone to the router, and
  still has to be called after `AddBattleForce` for the same reason. `FirstActiveShipInput` is
  untouched and stays in the engine, but the shipping game no longer composes to it: the device
  lock replaces free per-frame arbitration, which is what
  [#117](https://github.com/olivegamestudio/TDD/issues/117) decided.
  ([#7](https://github.com/olivegamestudio/TDD/issues/7))

- **Quest proximity triggers are swept across the frame rather than sampled at the end of it.**
  `QuestProximityWatcher.Update` takes where the frame began as well as where it ended, and fires
  a trigger on the closest the player came to its marker anywhere along that journey. It is a
  behaviour change only where the old measure was wrong: a frame that carried the ship from
  outside a trigger to outside it on the far side used to fire nothing, which made whether a quest
  started a property of where the frames happened to fall. The watcher still remembers nothing —
  the journey is passed in, so a resumed save cannot sweep the line between where the player was
  and where they are put down. The single position overload remains, meaning a frame the player
  did not move in. ([#8](https://github.com/olivegamestudio/TDD/issues/8))

- **`ISaveProgressService` gained `Load` and `Save`**, both in terms of text, so the engine stays
  agnostic about what a save contains and the same service works for any game built on it. ([#2](https://github.com/olivegamestudio/TDD/pull/2))
- **`BattleForceCampaign.Quests` is built on each read** rather than in a field initialiser. The
  campaign is a DI singleton, so a cached list froze the player's language at startup. It is read
  only when a game starts or resumes, so it is not on the frame path. ([#2](https://github.com/olivegamestudio/TDD/pull/2))

### Fixed

- **Earning experience or a level can no longer wipe out the progress it was meant to add.**
  `Progression.Gain` and `Progression.Advance` accumulated in ordinary unchecked `int` arithmetic,
  so `Experience`, `SpendPoints` and `Level` each wrapped negative once their totals carried past
  `int.MaxValue`. This is the same defect `Character.Earn` and `Reputation.Adjust` already carried,
  in the third place progress is added up, and it lands hardest on `SpendPoints`: `Spend` refuses
  any amount greater than the points held, so against a wrapped negative total *every* later spend
  is refused, and the doc comment's promise that a character cannot be left owing points was broken
  by the door marked "levelling up". `Experience` wrapping negative says a character has earned less
  than nothing, which no reporting code can read.

  All three totals are now added in `long` and held at `int.MaxValue`, written the same way
  `Character.Earn` writes it — widened before the addition rather than after, because after the wrap
  the evidence is gone. Only the top end is guarded, because neither a total nor an amount is ever
  negative, and both methods still refuse a negative amount outright. Held rather than refused for
  the reason credits are: the caller has made no mistake, and throwing would take the game down for
  a reward the player had just earned. It is a ceiling and not a latch — points held at the top
  spend normally. ([#153](https://github.com/olivegamestudio/TDD/issues/153))

- **Being paid can no longer put a character into debt and lock them out of spending for good.**
  `Character.Earn` added the payment to the balance in ordinary unchecked `int` arithmetic, so
  credits carried past `int.MaxValue` wrapped negative — the one state `Spend` exists to prevent,
  arrived at silently through the door marked "earning", with nothing thrown where it happened. The
  player found out later and could not recover: `Spend` refuses any amount greater than the balance,
  so against a negative one *every* purchase is unaffordable, and a payment had taken spending away
  permanently. The addition is now made in `long` and held at `int.MaxValue`, so earning can never
  leave a character poorer than it found them and never in debt. Only the top end is guarded,
  because neither the balance nor the amount earned is ever negative, and `Earn` still refuses a
  negative amount outright.

  Held rather than refused, for the reason a standing is: the caller has made no mistake, a reward
  of the usual size lands on a balance that happens to be at the end of the range, and throwing
  would take the game down for a job the player had just been paid for. It is a ceiling and not a
  latch — a balance held at the top spends normally.
  ([#152](https://github.com/olivegamestudio/TDD/issues/152))

- **Earning favour with a group can no longer turn it hostile.** `Reputation.Adjust` added the
  delta to the standing in ordinary unchecked `int` arithmetic, so a standing carried past
  `int.MaxValue` wrapped to `int.MinValue`. The sign of a standing *is* the relationship — negative
  is hostile, positive is friendly — so that is not a number coming out slightly wrong; it is a
  group the character has spent a whole game earning favour with turning maximally hostile on one
  reward, raising nothing and naming nothing, with every later award afterwards working back
  through the wrap rather than towards what the caller asked for. The move is now made in `long`
  and clamped to the `int` range, so a standing stops at the end rather than passing it: a positive
  delta can never leave a group less friendly than it found them, and a negative one can never leave
  them more.

  Clamped rather than refused, unlike the content guards below, because the caller has made no
  mistake — an award of the usual size lands on a standing that happens to be at the end of the
  range, and throwing would take the game down for a quest the player had just succeeded at. What
  it costs is the difference between two standings that are both already as friendly as the model
  can express. It is a ceiling and not a latch: a group held at the maximum still falls out with the
  character normally.
  ([#151](https://github.com/olivegamestudio/TDD/issues/151))

- **A ship can no longer be built with handling that is not a number, which flew it out of the
  world or nowhere at all.** `ShipMovement`'s constructor guarded acceleration and drag with
  `ThrowIfNegativeOrZero` and the turn rate with `ThrowIfNegative`, for the stated reason that a
  ship with no acceleration never moves and one with no drag never stops accelerating.
  `double.PositiveInfinity` is neither negative nor zero, so it walked past all three guards into
  degenerate physics that named nothing: infinite acceleration makes `MaximumSpeed` infinite and
  the ship's position runs off to infinity within a frame or two, taking the player with it;
  infinite drag makes `MaximumSpeed` zero, so full thrust moves the ship nowhere and the controls
  read as broken; an infinite turn rate leaves the heading at whatever wrapping infinity happens to
  produce. Each of the three values is now required to be finite, checked before the sign guards so
  that negative infinity and `NaN` are named by the rule they actually break — `NaN` was already
  refused, but only because the sign bit of `double.NaN` happens to be set, which is incidental
  behaviour to be resting on for the one value that would otherwise spread from the heading into
  every position the ship reports afterwards. The refusal names the value rather than the record it
  came from — `handling.Drag`, not `handling` — because a ship is built from authored content, and
  naming only the record leaves whoever wrote the profile reading three numbers to find the one
  that was meant. There is no upper bound on a finite value: how fast a ship goes is content's
  decision, and only a number that is not a handling value at all is refused.
  ([#125](https://github.com/olivegamestudio/TDD/issues/125))

- **A quest trigger can no longer be declared with an infinite radius, which fired it from
  everywhere.** `QuestTrigger`'s constructor guarded against a negative distance —
  `ArgumentOutOfRangeException.ThrowIfNegative` — for the stated reason that nothing could satisfy
  it, so the trigger would silently never fire. `double.PositiveInfinity` is not negative, so it
  walked past that guard into the opposite failure: the presentation applies the rule as
  `measured <= Distance`, every finite measurement is at most infinity, and the quest therefore
  started or completed the instant the player existed, wherever they were. Distance is now required
  to be finite, checked before the negative guard so that negative infinity and `NaN` are named by
  the rule they actually break. `NaN` was already refused, but only because the sign bit of
  `double.NaN` happens to be set — incidental behaviour to be resting the one value that makes
  every comparison against it false on. There is still deliberately no upper bound on a finite
  distance: the world is unbounded, so a large radius is a decision the content is allowed to make,
  and only a value that is not a distance at all is refused.
  ([#124](https://github.com/olivegamestudio/TDD/issues/124))

- **Progress is no longer silently lost to two saves being written at once.** `GameSession` marks a
  new game ready to play before it awaits its first save — deliberately, so loading stays off the
  frame loop — and the first frame puts the player inside quest 1's start trigger, which writes the
  same file again. Two snapshots were outstanding together with nothing ordering them, so the older
  one, taken before the quest began, was free to land last and become the save. The player was
  handed back a game in which the opening quest never started, and no file was corrupted, no
  exception raised and `SaveError` left `null`, so nothing reported it. Saves are now snapshotted
  when they are *asked for* and queued behind whatever is already outstanding: two writes are never
  in flight against the same storage, the last save asked for is the last to land, and `PendingSave`
  is the whole queue up to it rather than that save alone. A failure is raised to the caller who
  asked for that write and to nobody behind it, and the queue carries on, because a file locked for
  a moment must not stop the game saving for the rest of the run. `SetAside` is in the same queue —
  moving a refused save out of the way before the new game is written over it is an ordering
  guarantee and was worth only as much as the ordering underneath it — and because `IGameSession` is
  a singleton whose queue has no idea a game has ended, `StartNewGame` and `Continue` wait for
  everything already asked for before they read or replace the file, so a write left over from the
  previous game cannot land on top of the one just resumed.
  ([#63](https://github.com/olivegamestudio/TDD/issues/63))

- **A save service written to twice at once can no longer tear the file, and the contract now says
  it cannot.** `ISaveProgressService.Save` said only "replacing anything previously saved", which
  implies a last writer without defining one — and `LocalSaveProgressService` wrote through
  `File.WriteAllTextAsync`, which truncates and rewrites with no serialisation between callers.
  Measured at a 200 KB payload, concurrent pairs produced files holding bytes from both writes and
  raised nothing; on Windows the second write would instead fail `FileShare` and throw, which
  `GameSession.TrySave` turns into a save error shown to the player for a write that was merely
  unlucky in its timing. The contract now states that two overlapping writes leave the whole of one
  of them, that neither may fail on account of the other, and that the last write to *start* is the
  one that survives — and the shipped service keeps it two ways, because the game's own ordering
  fix cannot help a second process or another game built on the engine. Everything one instance is
  asked to do goes through a gate, and a write goes to a file of its own beside the save and is then
  moved over it, so the save's path never names a half-written file and a reader racing a writer
  sees one whole save or the other. `Load` now asks the file system once instead of asking whether
  the save exists and then opening it: `SetAside` moved the file out from under that gap, and a read
  that had passed the existence check threw `FileNotFoundException` instead of answering. A save
  that has gone by the time the read reaches it is genuinely no save, and that is now what it says.
  ([#63](https://github.com/olivegamestudio/TDD/issues/63))

- **A camera target or zoom that is not a number is refused where it is set, instead of drawing the
  whole world nowhere.** `Camera2D.Target` and `PixelsPerUnit` were plain settable values feeding
  straight into `WorldToScreen`. A `NaN` or an infinity in either put *everything* drawn through the
  camera — the star field and the ship both — at a position that is nowhere: drawn in full, with no
  exception raised and nothing written anywhere, and what the player saw was a blank window. Both
  now throw `ArgumentOutOfRangeException` at the point of assignment, so the failure names the frame
  that produced the number rather than surfacing as an empty screen somewhere else entirely, and a
  refused assignment leaves the camera on its last good value. `ICamera` states both rules, because
  everything drawn in the world takes the interface rather than the class. There is deliberately no
  bound on how far out the target is — the world is unbounded — and a very small zoom is still
  allowed, since the star field's clipping when it is wound far down is a stated limit of the star
  field rather than a mistake for the camera to veto. The star field's own guard against an
  undrawable zoom became a finiteness check at the same time: it asked `pixelsPerUnit <= 0f`, and
  every ordered comparison against `NaN` is false, so the one value it existed to stop was the one
  value that walked past it. ([#57](https://github.com/olivegamestudio/TDD/issues/57))
- **A saved position too far out to be drawn is reported as "no save" rather than resumed.**
  `GameScreen.PoseOf` narrows the world's `double` coordinates to the `float` the drawing side
  holds, so a coordinate past `float.MaxValue` becomes an infinity on the way to the camera. That
  is a perfectly good `double` — `1e300` parses, round trips and carries intact quests beside it —
  and it can only arrive fully formed from a file, since the ship cannot fly that far in any amount
  of time. Before the camera guard above it was a blank screen; with the guard and nothing else it
  would have been an exception once per frame out of the frame loop, which is a worse failure than
  the one being fixed. `SaveGame.CanBeResumed` is now the one place the repository states how far
  out a saved position may be, and `SaveGameSerializer` declines such a file exactly as it declines
  one that will not parse — the player gets a new game, which is what a save this build cannot read
  already gets. The bound is the drawable range and not a world size, so `float.MaxValue` still
  resumes. ([#57](https://github.com/olivegamestudio/TDD/issues/57))
- **`ShipControls` takes an axis it cannot read as hands off.** The constructor clamped with
  `Math.Clamp`, which does not hold for `NaN` — it returns it unchanged — so a driver reporting an
  axis it could not read put a `NaN` into the heading, then the velocity, then the position, and
  nothing after it recovered: the ship was still being drawn and flown and no longer had a place in
  the world. Unreachable before now, because the only input device was `NeutralShipInput`; reachable
  the moment a real pad was bound, so it is fixed with the binding. The same value would also have
  won the arbitration and shut every device behind it out, since `NaN == 0` is
  false. ([#9](https://github.com/olivegamestudio/TDD/issues/9))
- **`QuestLog.Register` refuses an identifier that names no quest.** Both edges that read a save skip
  a quest entry whose identifier is `null`, empty or blank, on the stated grounds that nothing is
  registered under one — but nothing enforced that, so a campaign could register a quest under a
  blank identifier and have it captured with its progress and restored to nothing, silently. A
  completed quest came back unstarted, and through the save file it cost the saved position too,
  because a game restoring no progress declines the coordinates saved beside it. `Register` now
  refuses that identifier where the duplicate check already lives, which turns the premise the two
  reading edges assert into one they can rely on. Only an identifier made of *nothing but* whitespace
  is refused: one with whitespace in it, or around it, names something and stays a campaign's to
  choose. Unreachable in Battle Force 2249, which names its quests — it matters because `Pilgrimage`
  is a standalone library whose content belongs to whoever builds on
  it. ([#106](https://github.com/olivegamestudio/TDD/issues/106))
- **`UIController.FocusOn` refuses a button it does not hold.** It was the last entry point taking a
  button that did not check, so focus could be aimed at a stranger and the complaint arrived at the
  next `Press` — an `InvalidOperationException` thrown from a call the author of the mistake was no
  longer standing in, naming a button they had not passed. It now throws where the aim is taken, and
  leaves the existing focus untouched when it refuses. Only membership is checked: a managed button
  that is currently disabled may still be focused, since `Press` declines a disabled button on its
  own and widening the guard would be a new decision rather than this
  fix. ([#101](https://github.com/olivegamestudio/TDD/issues/101))
- **One junk quest entry no longer discards the progress saved beside it, and no longer freezes the
  game.** A quest entry naming no quest — a `QuestId` that is absent, `null` or blank — is drift
  between a save and a campaign, exactly like an entry naming a quest this build has dropped, and
  the two edges that read one now agree on it: `SaveGameSerializer` drops the entry and reads the
  rest of the file, and `QuestLog.Restore` skips it. A `null` id used to come out of the dictionary
  as `ArgumentNullException` and a `null` *entry* as a `NullReferenceException`, neither of which is
  a storage failure — so they escaped `GameSession.Continue` onto a task nothing awaits, leaving the
  game screen waiting on a session that never became ready: no error, no new game, and the save
  neither read nor set aside. An entry that is not there is still refused, now as a stated
  contract and across the whole batch before any of it is applied, so a caught refusal leaves the
  log it started with rather than half a save. ([#44](https://github.com/olivegamestudio/TDD/issues/44))
- **A save holding no campaign progress no longer strands the player where it says.** Drift is
  tolerated on purpose, so a readable save whose every quest entry names nothing this build ships
  — a dropped quest, no quest at all, or no entries — restored an empty quest log while still
  putting the player at its coordinates. Quest 1 begins within 25 units of the marker a new game
  spawns on, so a player set down 700 units out had nothing active, nothing to fly towards, and
  got further from the only trigger that could help with every frame of flying forward; the only
  way out was backwards, through the debris field quest 1 is about escaping. `GameSession.Continue`
  now uses the saved position only when at least one registered quest came back started or
  completed. The file is still read rather than refused, because a refused save is set aside and
  written over while a declined position leaves it on
  disk. ([#44](https://github.com/olivegamestudio/TDD/issues/44))
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
  `InvalidOperationException` instead of acting on the namesake. `FocusOn` was the exception until
  [#101](https://github.com/olivegamestudio/TDD/issues/101) below.
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
