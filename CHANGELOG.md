# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **The ship can strafe.** `ShipControls` gains a third axis — `Strafe`, alongside `Thrust` and
  `Turn` — pushing the ship sideways at whatever it is currently pointed, independent of turning.
  `ShipMovement` applies it 90° clockwise from `Heading` (the ship's own starboard) and sums it
  into the same force thrust already applies, so both together fly a diagonal rather than one
  cancelling the other. It shares `Handling.Acceleration` with thrust; strafing arrived with no
  engine rating of its own authored yet.

  On the keyboard, Q and E now turn the ship and A and D strafe it — the WASD cluster had nowhere
  to put a third axis without taking a key from thrust or turn, so turning moved off A/D. The
  arrow keys are untouched: Left/Right still turn, so that four-key scheme still means what it
  always did. A gamepad's previously-unread right stick now carries strafe on its X axis, with the
  same dead zone the left stick's axes get.

  **A playtest asked for the glow too, so it followed** — `EngineGroup.StrafePort`/
  `StrafeStarboard` join `Fore`/`Aft` in `ShipView.EngineGlows`, gated on `ShipView.Strafe`'s sign
  the same way the other two gate on thrust, independently — burning ahead while strafing lights
  both groups at once. Unlike the other six, these two are a guess rather than an authored
  placement: strafing had no lateral-thruster art at all, so the offset, rotation and scale are a
  first pass, reasoned rather than measured against `ship1.png`, and worth revisiting once someone
  has actually looked at where they land on the art.

- **The engine glow answers the keypress, and the opening's help arrows fade as the ship reaches
  them.** Two playtesting reports against the debris field.

  The engine glow was parented to the ship in an earlier pass, which fixed it staying at the spawn
  point — but all six lights still burned constantly, at every thrust, which read as decoration
  rather than as an engine responding to anything. `ShipEngineGlow` now carries an `EngineGroup`
  (`Fore`, the three bow thrusters; `Aft`, the three mains), and `ShipView.Thrust` — set by
  `GameScreen` from the same `ShipControls` that flies the ship, read once and shared rather than
  asked for twice — decides which group is actually lit: ahead lights the mains, astern lights the
  bow thrusters, coasting lights neither.

  The opening's eight breadcrumb arrows were drawn at a fixed strength regardless of where the
  player had actually flown to, cluttering the view of the debris they exist to help someone
  survive. `HelpArrowView` (new) draws them instead of `RegionView`, fading each one by its own
  distance to the ship — full strength beyond `HelpArrowView.FullyVisibleDistance`, gone within
  `HelpArrowView.HiddenDistance`. `RegionView` now skips the `"UI"` layer the arrows live on, so
  the two views never draw the same body.

  **Two more playtesting rounds against this same pair.** The engine flame rendered from a plain
  canvas centre, reading as bottom-heavy against its mount the same way the field's own lights did
  before `RegionView.LightArtworkCentreFraction` existed — an earlier pass deliberately left the
  ship's glow off that correction, reasoning its offsets were authored against the uncorrected
  origin; a play session found that reasoning backwards, and `ShipView.RenderGlow` now anchors from
  the same constant every other light in the game does. And the arrows' fade was pure distance,
  recomputed fresh every frame — fly past one, double back, and it read as though the guidance had
  never registered. Reaching one is permanent now: `HelpArrowView` remembers which arrows it has
  already faded to nothing, in a set only `Reset()` clears, called by `GameScreen.Enter()` alongside
  the other per-flight state it resets — a resumed or restarted game starts a fresh approach to the
  field, not a continuation of the one that last reached these arrows.

- **The title screen has a sky behind it.** `MenuScreen` draws `space` as a sixth layer, first and
  behind the other five, from the new `MenuScreen.BackgroundAssetKey`. The composition already had
  its figures, its horizon band, its logo and its button; what it did not have was the "dark space"
  the screen was specified with, so everything above the 3.4:1 horizon band was literally black —
  the last strip of the black boot this screen exists to end.

  **Covered, not fitted, and not stretched.** The scale is taken from whichever axis needs the
  larger one, so the 4096² asset reaches both edges of any window and the overspill falls off the
  screen; fitting it would leave a bare strip on every window that is not square, which is the black
  the layer was added to remove. Uniform rather than stretched, unlike `Vignette` — that is a mask
  which has to meet the window's edges exactly, whereas this is a picture of stars, and a stretched
  star is an oval on every window but one. Cropping costs nothing because a star field has no
  subject to cut in half. Four window shapes are held to covering, including one taller than it is
  wide and one absurdly wide, because a cover rule written as a minimum passes the widescreen case
  and fails both of those.

  The unused `Image("BACKGROUND")` placeholder that stood for this is gone. A backdrop takes no
  focus and answers no press, so it is a sprite rather than an element in the controller's tree —
  left there it would only have given the player something to tab onto.

- **The ship has a hull, and the debris field has something to hit it with.** `ShipMovement`'s
  Aether body now carries a circle fixture sized from the new `ShipProfile.HullRadius`, and
  `AddObstacle(Position, width, height, rotation)` seeds static rectangular obstacles into the same
  physics world — physics only, no damage: rocks are harmless for now, and there will be places
  later that can hurt the player. `SceneBody.Solid` (new, defaults `false`, so existing regions lose
  nothing) marks which bodies collide; `debris-field.json` sets it on the actual rock and asteroid
  bodies, never the backdrop. `RegionObstacles.Seed` (game-side) turns a loaded scene's solid bodies
  into obstacles sized from each sprite's real measured pixel dimensions, the same conversion
  `RegionView` already uses to draw them, and `GameScreen` seeds them once a game has both a ready
  ship and a loaded region.

  **Rectangular, not circular — a playtest showed why.** The first cut averaged each sprite's width
  and height into one collision radius; against the actual debris field that read badly on anything
  long and thin, a rock wall's own collision circle reaching well past where the wall is drawn in
  some places and falling short of a corner in others. `AddObstacle` now takes a width, a height and
  a rotation, and builds a four-corner polygon fixture turned by hand to match — the same convention
  `Heading` already uses, clockwise from positive Y, rather than trusting Aether's own rotation to
  agree with it untested. `RegionObstacles` derives width from a body's `ScaleX` and height from its
  `ScaleY` independently, and turns the rectangle by the same `-RotationDegrees * π / 180`
  conversion `RegionView` already draws the sprite with, so the collision shape sits exactly under
  the art it is standing in for.

  **Every `asteroid1`, not just the ones on the `Environment` layer.** The original heuristic marked
  `asteroid1` solid only there, reasoning the rest were background picture; a play session found one
  sitting in the open, drawn exactly like a real obstacle, with nothing behind it to read as
  backdrop. All 14 `asteroid1` instances in `debris-field.json` are solid now, regardless of layer.

  **`asteroid2` was missing altogether — not in `RegionObstacles`'s known sprites, not marked
  solid, and its one instance sits at `(0, 0)`, the ship's own spawn point.** A play session put a
  ship right up against it with no collision ring at all, the same symptom as the `asteroid1`
  finding above at one level up: this sprite had never been accounted for, not merely
  mis-classified. Measured the same way as the others — a 512×512 canvas, 389×407 of it opaque —
  added to `RegionObstacles`, and marked solid.

  The body's position now syncs from `Player.Position` every flying frame rather than always
  starting at zero, which is what lets the ship and an obstacle placed at its true position actually
  occupy the same coordinates — except when the ship is idle (neutral controls, no velocity), which
  skips physics entirely, because a resumed save or a test moving the player by hand landing inside
  an obstacle must not be corrected as though the ship had flown there.

  **A real finding, not a defect:** once solid, the debris field turned out to have no comfortable
  straight-line route through it for a hull this size — checked with a grid pathfind, the only route
  near the direct line from spawn to quest 1's exit marker has almost no clearance either side, and
  some rocks' authored positions even overlap each other as circles. `GameScreenTests`' old
  "flies straight to the exit, completes quest 1" test asserted something no longer true of the
  content once the field is genuinely solid; it now asserts the ship gets blocked instead, which is
  correct — the debris was always meant to be something the player has to see and avoid. A handful
  of acceptance tests that used quest-1 completion only as proof that a key or a stick reaches the
  ship now check the ship travelled a stretch of open water short of the field's first obstruction.
  Whether the field itself should be thinned out for a clearer route is tracked separately as a
  content decision.

  **The hull and one obstacle sprite's radius are measured against the art now, not reasoned by
  eye.** A play session against the real game — the point of collecting this before treating it as
  settled — found the ship visibly overlapping rocks well before anything registered a hit.
  `ship1.png`'s art turned out to fill most of its canvas rather than a fraction of it, so the
  ship's true visible size was roughly three times the guessed hull radius; `DisgracedShip
  .HullRadius` is now half the shorter axis of the sprite's own alpha-channel bounding box, not a
  number chosen for reading well in a comment. `asteroid1.png` had the opposite problem — real
  transparent padding a full-canvas guess did not account for — and `RegionObstacles` now measures
  that one too. The rocks needed nothing: their canvases turned out to have no padding at all.

  **`CollisionDebugView` draws a ring around the hull and a rectangle around every solid body, at
  the exact size and rotation the physics actually collides at** — reading
  `RegionObstacles.SizeOf`/`RotationOf` and `ShipProfile.HullRadius` rather than keeping a second
  copy that could quietly disagree with what is seeded. The rectangle followed the physics off the
  circle for the same reason a playtest gave for the collision shape itself: a red ring around an
  elongated rock wall was visibly the wrong shape once it was on screen next to the art. A developer
  aid, not a shipped feature: `Enabled` defaults to `true` while the gap between "the physics is
  right" and "the physics looks right" is still being closed, and drawn last, after even the
  vignette, so a collision worth checking near the screen's edge is not dimmed to find. There is no
  primitive-shape drawing anywhere in this renderer, so both shapes are faked the standard way —
  short segments of `pixel.png`, one new opaque pixel asset, stretched and rotated.
  ([#184](https://github.com/olivegamestudio/TDD/issues/184))

- **The game screen is framed, and the sky behind it has come down.** The backdrop was authored as
  a picture in its own right and read as one — bright and flat enough that the ship and the debris
  in front of it had nothing to stand out against. `RegionView.BackdropTint` takes it down, and
  `GameScreen` sets it on entering from `GameScreen.BackdropBrightness`.

  **The tint reaches the sky and stops there.** The stars keep their brightness, because points of
  light on a darker sky is the contrast this is buying, and so does everything standing in the
  world: the backdrop is scenery the player looks past, the debris is scenery the player flies
  into, and dimming the second one costs them the warning they get of what they are about to hit.

  `Vignette` is the frame over the top: one sprite stretched across the whole window, drawn after
  everything else, clear across the middle and closing to dark at the corners. **It intercepts
  nothing and cannot** — a sprite is a drawing instruction and the type answers nothing but
  `Render`, so there is no seam through which it could take a press, a click or a finger from what
  is under it. The shape of the ramp is in the asset, generated by `scripts/make-vignette.py` so
  the radii are reviewable rather than buried in a PNG; how heavily it is laid on is
  `Vignette.Strength`, in code, so tuning it does not mean regenerating anything.

  **Both numbers are untuned and said so plainly.** `BackdropBrightness` and `Strength` were chosen
  by reasoning rather than by looking — nobody has seen this on a screen — and how dark a sky
  should be and how heavy a frame should be are judgements only a human at a window can settle.

- **A sprite can be sized to the window.** `Sprite.Stretch` scales the two axes relative to each
  other on top of `Scale`, and is one on both axes unless something says otherwise, so everything
  already drawn keeps its texture's shape. It exists because a window's aspect is whatever the
  player dragged it to: an overlay covering one cannot be square, and a uniform scale can only
  leave two edges bare or push the other two off the screen. `MonoGameRenderer` multiplies the two
  on the way to the device, so `Scale` still means what it says on the sprites that only set it.

- **The engine can see fingers, and knows what two circles on the screen mean.** `InputFrame` now
  carries a `TouchFrame`: every finger that is down, each a `TouchPoint` with the platform's
  identifier and a position in window pixels. The identifier is the part that matters — two
  coordinates cannot say whether the finger reading 640 this frame is the thumb that read 600 last
  frame or a second one that landed nearby, and a control that follows a finger cannot be built
  without knowing. A host that has no touch screen says nothing and reports none, so the desktop
  host is not made to name a device it does not have.

  `TouchOverlay` is the mobile control scheme: a stick on the left that flies the ship, a button on
  the right that fires, both drawn over the same HUD rather than as a second mobile screen. It
  answers a `ShipControls` the physics cannot tell apart from a stick's. **The helm captures its
  finger and the fire button does not** — a virtual stick has no physical stop, so a player pushing
  for full ahead slides off the circle without feeling it, and a helm that let go there would cut
  the engine at the exact moment they asked for all of it. It follows the finger that took it until
  it lifts, and a second finger landing on the circle cannot snatch it mid-turn. Firing is a button
  and a finger sliding off one has stopped pressing it. There is no dead zone and none is wanted: a
  finger is where it is put, and when lifted is not there at all.

  Not yet wired, and said plainly rather than implied: **nothing routes touch** — a tap cannot lock
  the device because the UI navigates by focus and holds no positions to hit-test against, which is
  a design call rather than a gap; **nothing draws the circles**, which need sprite assets; and
  **nothing consumes the firing half**, because the engine has no weapons. It is reported rather
  than dropped, so the right-hand circle reads as a control whose other end is unbuilt.

- **A hold has a size, and items stack in it.** `Inventory` is now a bounded set of slots rather
  than an unbounded list. `ItemStats` — the stats type this brings in, alongside `ShipHandling`,
  `ShieldStats` and `OrbStats` — says how high an item stacks and which slot it fits; items of a
  kind share a slot up to that limit, and the one after it opens another. The limit is capped at
  99, because carrying pressure is a designed constraint the player is meant to keep feeling, and
  one item type stacking to a thousand is a hole straight through it.

  Capacity is counted in slots and never in weight, so "have I got room?" is a question the player
  answers by looking. How many slots there are is a ship stat — `ShipProfile.CargoSlots`, 16 on the
  starting hull and untuned — while what is in them belongs to the character, which is what keeps
  possessions out of the transient hull that a save would have to rebuild them into.

  **Being full is an answer rather than a failure.** `Inventory.Add` reports that there was no room
  and takes nothing, so nothing is ever destroyed on the player's behalf. Loot that arrives at a
  full hold now stays in the world instead of evaporating on contact, and is collected on the first
  frame after room is made; `Loot.DropGuaranteed` says a guarantee could not be kept rather than
  losing the item to keep its promise tidy. The one place a full hold throws is authoring — content
  stocking a character with more than the hull holds is a mistake with no good runtime answer.

- **A ship has eight equip slots and a collected item finds its own.** `Loadout` is four weapon
  slots, two shield slots and two additional slots, all empty on a new hull, and `EquipSlot` is what
  an item says to name the one it fits. `Character.Collect` is the pickup rule in one place: the
  item is owned first, then fitted into a free slot of its kind when the ship has one — because a
  player who collects their first weapon and flies on unarmed has been given no reason to think a
  screen exists that they have to open. Fitting does not take the item out of the inventory, so
  losing the ship loses the fitting and not the item.

  The shield and additional counts are read from `Shielding.Slots` and `Orbs.Slots` rather than
  written down twice, so the item slots and the stats slots cannot drift apart into a ship with two
  shield slots that fits three shields.

  **An item is its identifier and only its identifier.** Equality is the id alone, so an item read
  back from a save is the item the game shipped and a save can carry ids and nothing else. An id
  authored twice with two sets of numbers is one item described wrongly rather than two items, and
  it is refused where the two first meet instead of quietly becoming a second stack held under the
  wrong limit.

- **Loot reaches the player without a button.** `Loot` holds what has been dropped into the world
  and not yet picked up. `Drop(item, position)` leaves an item where it fell; `Update(from, to,
  elapsedSeconds, into)` draws in whatever the frame came near, moves what is already coming, and
  collects what arrives. Coming near a drop is the whole of collecting it, which is what keeps the
  mechanic identical on a device with no buttons to spare — nothing here takes an input.

  Reach is measured against the **journey** a frame covered rather than the point it ended at, the
  same measure `QuestProximityWatcher` is held to and for the same reason: sampling one point a
  frame would make a pickup a property of the frame rate, and the faster the player flew the more
  of their loot they would leave behind. `Loot` keeps no memory of where the player was, so a
  resumed save cannot sweep up every drop lying between where the player was put down and wherever
  they had been.

  `LootMagnet` is the mechanic's stats type — `Reach` and `DriftSpeed`, both refused at authoring
  unless positive and finite, because a reach of zero asks for a pickup the player has no button to
  perform and a drift speed of zero draws a drop in and then never delivers it.

  `DropGuaranteed(item, into)` is the drop that cannot be missed: collected the instant it falls,
  wherever the player is, never lying in the world at all. It is a separate method rather than a
  flag because it shares no step with the ordinary path, and it is what the game will use where a
  missed drop would soft-lock a quest.

  Two things the model had to answer to work at all, neither of them decided in design capture: a
  drop that has been drawn in **stays** drawn in, so flying back out of reach does not strand loot
  the player has no button to claim; and a drop that would overshoot the player arrives instead.
  Both are open to being overruled. Three things this deliberately does not do: nothing drops loot,
  because nothing in the world can be destroyed yet (#137); no `LootMagnet` is authored, because
  its numbers are content's and no content drops anything; and nothing draws a drop, which is
  screen work of its own.
  ([#138](https://github.com/olivegamestudio/TDD/issues/138))

- **A ship carries two orbs, and they fly themselves.** `OrbStats` is the orb's stats type, authored
  as one of the two things an orb does — `Orbiting(radius, angularSpeed)` circles the ship and never
  leaves it, `Tracking(radius)` holds station until it has something to go after. An orb does one or
  the other, so there is no constructor that would let a data file invent a third behaviour by
  filling in both.

  `Orbs` is what is in the two additional slots, filled freely exactly as the shield slots are — and
  that is where the resemblance stops, because **nothing adds up**. An orb acts on its own, so two of
  a kind is two companions each doing its own thing rather than one effect at double strength, and
  there is no total to read because there is no quantity two orbs share. That settles the question
  design capture flagged as *"assume free-choice / stack-or-combo like shields — confirm"*.

  `Orbs.PlaceAround(ship, secondsFlown)` is the whole of "auto-controlled — no manual fire input":
  where a companion is depends on what it was authored with and how long has been flown, and on
  nothing the player pressed. Nothing ticks and nothing accumulates, so the answer cannot drift with
  the frame rate and a resumed game does not have to remember where the orbs had got to. Each slot
  takes an even share of the ring, so two orbs start on opposite sides of it rather than occupying
  one point; the ring is measured in world terms rather than turning with the hull, because a
  companion that runs itself keeps its own bearing while the ship turns under it. A time that is
  negative or not finite is refused there, for the reason the camera refuses a target that is not
  finite: it would place every orb nowhere, in full, raising nothing.

  `Ship.Fit` now has an orb overload, and a new ship comes out of the yard with the additional slots
  empty — which is what the design asks for at the start of the story. Three things this deliberately
  does not do: a tracker does not track, because nothing in the world is hostile yet and what it
  chases belongs with the fight; the "ball" design capture names as a third kind of orb is not
  modelled, because what a ball *does* is stated nowhere; and nothing draws an orb, which is screen
  work of its own.
  ([#136](https://github.com/olivegamestudio/TDD/issues/136))

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

- **Thrust and drag are Aether.Physics2D, not a closed-form formula.** `ShipMovement` builds a
  one-body, gravity-free Aether `World` and applies thrust as a force against the body's linear
  damping every physics step, in place of the exact exponential integration the model used before.
  Frame independence — a ship covers the same ground at any frame rate — is now held by a
  fixed-step accumulator (1000 Hz) rather than by the integration being exact: Aether's damping is
  a per-step approximation of the same decay, so it converges towards the old model's terminal
  speed rather than landing on it exactly. Turning is untouched — the helm was never modelled with
  inertia, only a rate the pilot commands, so `Heading` is still the same hand-rolled kinematic
  update. `ShipMovement`'s public surface (`Heading`, `Velocity`, `Update`, `Reset`) did not change,
  so `Ship`, `GameScreen` and `QuestProximityWatcher` needed nothing. Collision detection is a
  natural follow-on now the ship is a rigid body, and is not part of this change.
  ([#181](https://github.com/olivegamestudio/TDD/issues/181))

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

- **A hull's authored loadout is copied, not kept.** `ShipProfile` stored the `Loadout` it was
  given as its own backing list, so a caller that passed a `List<Item>` and went on adding to it
  changed what the profile reported — against the type's own remarks, which say a profile "is
  authored and never changes". `IReadOnlyList<Item>` was doing the work it can do, stopping the
  profile's *readers* mutating the loadout, and none of the work it cannot: it says nothing about
  whoever still holds the list the profile was made from. `Loadout` is now a copying property
  alongside `CargoSlots` and `HullRadius`, the same defensive copy `Orbs` and `Shielding` already
  make of what they are fitted with — `ShipProfile` was the last authored-content type without one.

- **A hull whose size it could not collide at is refused where it was authored.**
  `ShipProfile.HullRadius`'s own param doc has said since it landed that the value "must be a
  positive, finite number", but the record stored whatever it was given — `0`, `-5`, `NaN` and
  `Infinity` all made a profile that could be built, stored and passed around. `ShipMovement`
  caught them, but only once a `Ship` was built from the profile, and the exception it threw named
  `hullRadius` — its own constructor parameter — leaving whoever wrote the content to work out
  which of the profile's numbers that was. `HullRadius` is now a validated property alongside
  `CargoSlots`, throwing `ArgumentOutOfRangeException` named for the profile's own member.
  Not-finite is named before the sign is ranged, because every comparison against `NaN` is false
  and a `NaN` radius otherwise walks past a range check to become a ship that collides with
  nothing.

- **The debris field's lights hang where they were placed, and they flicker.** Both halves were
  reported from the same play session. `RegionView` drew every body about the middle of its own
  texture, which is right for a rock filling its canvas and wrong for `glow.png`: the lit part of
  that file spans rows 52–171 of 183, with near-empty canvas above it and almost none below, so its
  alpha-weighted centre is 0.654 down the file rather than 0.5. Placed by the file's middle, each of
  the field's 14 lights landed roughly a third of a ship's length below the point it was authored
  at — which reads as a light resting on the debris rather than hanging in space among it.

  `RegionView.LightArtworkCentreFraction` corrects it at the draw rather than by redrawing the
  asset, for the reason `MenuScreen.TitleContentCentreFraction` does: the number describes the file,
  so a redrawn light restates it instead of silently moving every light in the region. Only the
  light is corrected — everything else keeps the origin it had, because a solid body's collision
  rectangle is seeded from its authored position and a sprite moved off its own collision shape is
  worse than one drawn off its authored point.

  **And they were static.** `RegionView.SecondsElapsed` is a clock the view is *handed* rather than
  one it keeps, the same way `Orbs.PlaceAround` is handed how long has been flown: `GameScreen`
  accumulates it and sets it beside the camera's own state each frame, so nothing here ticks, a
  frame drawn twice draws the same picture both times, and the flicker cannot drift with the frame
  rate. `RegionView.BrightnessAt` is the curve — a slow breath with a faster tremor over it, at a
  ratio that is not a whole number so the pair does not come back into step on a short cycle,
  weighted to stay within `DimmestLight`..`BrightestLight` (0.6..1). It never exceeds 1 because
  brighter than the artwork is a light the author never drew, and it never reaches 0 because a light
  the player is navigating a debris field by should flicker rather than blink out. A light's phase
  comes from where it stands, so the 14 of them are out of step without anything being authored per
  light — a field dipping on one beat is a power cut, not a flicker.

  A time that is negative or not finite is refused at the setter, for the reason the camera refuses
  a target that is not finite: the sine of a number that is not one is not a number, and the failure
  would otherwise surface from inside `Colour` on some later frame, naming a channel rather than the
  clock that produced it.

  **`ShipView`'s engine glows are deliberately untouched**, though they draw the same asset with the
  same file-centred origin. Their offsets were authored against that origin, so correcting theirs
  would move flames that are currently where somebody put them — a separate change, and one only a
  human at a screen can judge.
  ([#188](https://github.com/olivegamestudio/TDD/issues/188))

- **`Inventory.HasRoomFor` no longer promises room for an item `Add` refuses outright.** It is
  documented as saying what `Add` would do without doing it, and callers use it exactly that way —
  loot that has to stay in the world if it cannot be taken asks before the attempt. But it answered
  from the free-slot count and a match on `Item.Equals`, which compares ids alone, while `Add`
  additionally refuses an id already held under different authored `ItemStats`. So a hold with a
  free slot said "yes, room" for a misauthored item and then threw `ArgumentException` the moment
  the caller acted on the answer.

  The stats check now lives in one place — a private `StackWithRoomFor` both `Add` and `HasRoomFor`
  go through — so the two cannot state different rules again. `HasRoomFor` consults it *before* the
  free-slot count, because a hold with room would otherwise short-circuit to `true` without ever
  looking at what it already holds, which is how the two came apart.

  **It throws rather than answering `false`**, which is the less obvious half. Returning `false`
  keeps the query a query, but it is the wrong no: a caller guarding an `Add` reads `false` as an
  ordinary full hold and leaves the item in the world — correct for a hold with no room, and a
  silent grave for a misauthored one, which would then never be added, never throw and never be
  seen. `Add` is deliberately loud about two authorings of one id because it is the first place the
  two are ever seen side by side; answering the same question quietly would undo that.
  ([#192](https://github.com/olivegamestudio/TDD/issues/192))

- **The ship no longer flies out of its own engine glow.** The glow was authored into
  `debris-field.json` as six `glow` bodies — `FrontEngine`, `FrontEngine2`, `FrontEngine3` and the
  three `RearEngine` ones — sitting at the world origin on the `Characters` layer, which is where
  the ship starts. Standing still it looked right, and that is what made it survive: the fault only
  appears once the player touches the throttle, and what they see then is the ship leaving its
  engines burning at the spawn point.

  The six bodies are out of the region, and `ShipView` draws them itself, beneath the hull: new
  `ShipEngineGlow` (offset, rotation and the two authored scale factors) and
  `ShipView.EngineGlows`, read in the ship's own frame. Each glow's offset is turned by the ship's
  heading before it is added to the ship's position, and its own angle is added to the heading
  before the camera is asked what that looks like — both halves of being parented, and each has its
  own failing test if it goes: an offset added in world axes puts the port engine off the starboard
  bow the moment the ship comes about, and an angle drawn on its own leaves the plume pointing where
  it was authored while the ship turns underneath it.

  **The numbers are the authored ones, unchanged**, including the authored `scaleX`/`scaleY` pair —
  which `Sprite.Stretch` can now carry, so a glow is drawn as the plume it was authored as rather
  than as the averaged blob `RegionView` has to settle for. This fixes *where* the glow is drawn and
  deliberately decides nothing about how it looks: whether it sits right against a hull that has
  since been resized to 30 world units is a judgement for a human at a screen.

  `DebrisFieldRegionTests` now holds the region to carrying nothing on the `Characters` layer at
  all, so a re-import that brings the bodies back fails rather than quietly drawing a second glow;
  `ShippedRegionContentTests` holds the ship's own asset keys — hull and glow — to being in the
  content build, which the region check could not see now that the ship names them in code.

- **A countdown can no longer be ticked backwards, which silently un-elapsed a finished timer.**
  `Countdown.Tick` took its interval on trust, so a negative `TimeSpan` added time back rather than
  taking it away. That is not a slow countdown, it is a rearmed one: `IsElapsed` is meant to go one
  way and only `Reset` is meant to bring it back, and a negative tick undid that transition behind
  the back of the screen that owns it — a logo that had finished playing starting over.

  `Tick` now throws `ArgumentOutOfRangeException` on a negative interval, the same refusal
  `Meter.Reduce` makes and for the same reason: a duration that came out backwards is an error, not
  time returned. Zero is still allowed, because a frame that took no measurable time is a real frame
  and refusing it would make the game loop check the clock before it was allowed to report it. The
  only consumer, `CompanyScreen.Update`, is unaffected — its interval is the frame time from the
  game loop, which never runs backwards. This finishes the guard the constructor started: the
  countdown's duration and its ticks are now both held to the same rule.
  ([#175](https://github.com/olivegamestudio/TDD/issues/175))

- **A countdown can no longer be built with a negative duration, which produced a timer that could
  never be rearmed.** `Countdown` was the last numeric type in the engine to take its input on
  trust — `Meter`, `QuestTrigger`, `LootMagnet`, `ShieldStats` and the rest all refuse values that
  would make the object behave wrongly. A negative duration started the countdown already elapsed,
  and because `Reset` puts the constructed duration back, it stayed elapsed through every reset
  after that: the one promise the type makes about resetting, broken silently and for good.

  The constructor now throws `ArgumentOutOfRangeException`, at the point where the wrong number is
  still legible as the wrong number. Zero is deliberately still allowed — a countdown given no time
  to run is finished the moment it is made, which is a coherent answer and is how a splash screen
  configured to last no time skips itself. The only consumer, `CompanyScreen`, is unaffected: its
  duration comes from options that already reject a negative on resolve.
  ([#174](https://github.com/olivegamestudio/TDD/issues/174))

- **A pair of shields too large to add up now leaves a ship enormously well shielded instead of
  crashing the fit.** `Shielding` summed the capacity of its fitted shields without asking whether
  the total was still a number a pool could hold. Two `ShieldStats.Absorbing` shields that each
  passed their own validation — `ShieldStats` correctly refuses anything not finite — summed past
  the end of a double and came back as infinity, and `Ship.Fit` then handed that infinity to
  `Meter`, which refuses it deliberately: an infinite pool can never be emptied.

  So fitting two individually valid shields threw, and threw in the wrong layer's words —
  *"maximum ('Infinity') must not be equal to 'Infinity'"*, from a constructor the caller never
  called, about a number the caller never passed. Every layer validated its own inputs; the gap was
  that `Shielding` never validated its own *output*, and nobody in between caught it.

  The sum is now held at `double.MaxValue`, which is the line the reflected share on the next line
  was already drawn on and drawn for the same reason: each shield is authored on its own and neither
  of such a pair is a mistake, so the total is held rather than refused. The ceiling is not a rule
  about stacking — a pair that adds up to anything a double can still count adds up to exactly that,
  however large, so the only pairs it touches are the ones that would otherwise arrive as infinity.
  Practically this is content nobody will author; the defect was the chain, not the number.
  ([#168](https://github.com/olivegamestudio/TDD/issues/168))

- **An infinite hit is now refused as the hit it was, instead of surfacing from inside a meter as a
  number nobody passed.** `Ship.TakeDamage` guarded against a negative amount and against NaN, and
  neither guard refused infinity. The arithmetic below them then turned an infinite hit into a NaN
  by either of two IEEE 754 routes, depending on what was in the shield slots: with nothing
  reflecting, `reflected = ∞ × 0` is NaN; with anything reflecting, `reflected` is infinity and
  `landed = ∞ − ∞` is NaN.

  Either way the NaN reached `Meter.Reduce`, which refused it — so the call did fail, and failed
  with the wrong story. The exception said *"a meter holds real numbers only"* and reported an
  actual value of NaN: it named the meter for the caller's mistake, pointed at a layer the caller
  never called, and gave a number that appears nowhere in the call. `Meter.Reduce` accepting
  infinity is deliberate, and it never got the chance, because the intermediate arithmetic corrupted
  the value before it arrived.

  The guard now asks for a finite number ahead of the sign, which names infinity, negative infinity
  and NaN by the rule they actually break — the ordering `QuestTrigger` already uses on a distance,
  and for the second of the same two reasons: `ThrowIfNegative` stops the standard NaN only because
  the sign bit of `double.NaN` happens to be set, and one whose sign bit is clear walks past it.

  The stricter line is drawn here and not in `Meter` on purpose. A meter takes a single change, and
  an infinite one is simply the whole pool in one go; a hit is *apportioned* across three parts, and
  infinity divides into no shares — reflecting a quarter of an infinite hit sends back infinity,
  which is the whole of it, so a shield authored to bounce a share back would bounce all of it and
  `DamageOutcome` could no longer add up to the hit that arrived. There is still no upper bound
  short of infinity: `double.MaxValue` lands, because nothing caps what a weapon may be authored to
  hit for. ([#167](https://github.com/olivegamestudio/TDD/issues/167))

- **A time scale of infinity is now refused where it is set, instead of crashing the frame loop
  later.** `ScaledFrameTimeController.TimeScale` guarded with `value >= 0`, which is an ordered
  comparison and so answers the wrong question about the three values that are not numbers: NaN and
  negative infinity were refused by accident — both compare false against zero — and positive
  infinity passed, because it really is greater than zero.

  What got through did not stay quiet. `Filter` multiplies the frame time by the scale, and
  `TimeSpan`'s multiply throws `OverflowException` for a result larger than the ticks a `TimeSpan`
  holds, which infinity always is. So the setter succeeded, the game carried on, and the failure
  arrived on the *next frame* — from the frame loop, on every frame, naming `TimeSpan` arithmetic
  rather than the scale that caused it. A scale computed from a division whose divisor reached zero
  therefore set a time bomb somewhere the stack trace no longer pointed at.

  The guard now asks for a finite number ahead of the sign, which refuses all three in one check and
  leaves zero — the documented freeze — allowed. It is the same answer `Camera2D` gives for the same
  reason: a value that cannot be used is refused at the setter, because a frame cannot be declined
  quietly. ([#162](https://github.com/olivegamestudio/TDD/issues/162))

- **A save holding a state no quest has is now refused before any of it is applied.**
  `QuestLog.Restore` checked the entries that were *not there* across the whole batch before
  applying any of it — the documented reason being that a caller catching the refusal still has the
  log it started with rather than half a save. The other refusal, a state outside the quest
  lifecycle, was raised by `Quest.Restore` as the entry was met, during the walk that applies them.
  So a file whose second entry was corrupt had already had its first entry committed when the
  exception arrived, leaving a log that was neither what the caller had before the call nor what
  the save described, and that nothing could finish or undo.

  The outcome also depended on the order the entries happened to be in, which is the one thing the
  duplicate rule was chosen to be free of: the same two lines the other way round restored a
  different log. `Restore` now walks the batch a third time, between the two it already made,
  refusing an undefined state wherever it names a registered quest. What it refuses is unchanged —
  an entry naming no quest, or naming one this build no longer ships, is still passed over before
  its state is read, so drift still costs the file nothing.
  ([#161](https://github.com/olivegamestudio/TDD/issues/161))

- **A meter can no longer be handed a not-a-number that leaves it holding nothing readable.**
  `Meter`'s constructor, `Reduce` and `Restore` all documented that they refuse a value that is not
  a number, but all three leaned on `ArgumentOutOfRangeException.ThrowIfNegative`, which reads the
  sign bit. `double.NaN` happens to carry a set sign bit, so it was stopped by accident rather than
  on purpose — and a NaN whose sign bit is clear, which is exactly what `Math.Abs` hands back for a
  calculation that came out wrong, walked straight past all three guards.

  What got through did not throw later either, which is what made it worth fixing: `Current` became
  NaN and stayed there, and NaN is neither above zero nor at or below it. `IsEmpty` answers `false`
  for a pool holding nothing, so a hull can never be destroyed; and a shield layer's
  `Math.Min(landed, NaN)` absorbs nothing, so every hit goes straight through to health. The damage
  model breaks silently and in whichever direction is worse.

  NaN is now named by the rule it actually breaks, ahead of every negative guard, the way
  `QuestTrigger` and `ShieldStats` already name it. Infinity is deliberately still allowed in
  `Reduce` and `Restore` — an infinite change is simply the whole pool in one go and still clamps to
  a real number — and still refused as a `Maximum`, which is a pool nothing could ever empty. That
  is the distinction the guard is drawing: not "is this finite", but "does this clamp to a number
  anything downstream can compare against".
  ([#160](https://github.com/olivegamestudio/TDD/issues/160))

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
