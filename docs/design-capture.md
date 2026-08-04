# Design capture (live)

> Working capture of the game's mechanics and foundations, taken straight from the designer.
> This is a **draft to be folded into `DESIGN.md` (game) and `ARCHITECTURE.md` (structure)** once
> settled — it is not yet canon. Status tags:
> **[DECIDED]** settled · **[v1]** now / **[FUTURE]** later · **[OPEN]** needs a decision ·
> **[PARKED]** deferred on purpose · **[vs main]** diverges from the current code.

---

## 1. Platform & targets

- **[DECIDED]** Steam-led, **Windows-first**, desktop primary.
- **[DECIDED]** **Mobile is a real target** — touch + virtual thumbsticks. The UI must **not need a
  redesign** to get there, so input *and* UI layout are device-agnostic from day one.
- **[OPEN]** How one UI layout serves both **focus-navigation** (keyboard/gamepad) and
  **direct-tap touch** + on-screen thumbsticks (target sizes, stick placement).
- **[vs main]** Current UI (`OliveGameStudio.UI`) is focus-navigation only; touch isn't modelled.

## 2. Presentation

- **[DECIDED]** Sci-fi, **2D top-down**.
- **[DECIDED]** **360° camera rotation — the world rotates *around* the ship; the ship always
  points forward/up.** Camera orientation follows the ship's heading (world is drawn rotated by
  −heading). *(Confirms issue #35; closes decision #111.)*
- **[DECIDED]** Implication: `Camera2D` gains an **orientation**; `ShipView` draws the ship
  **un-rotated** (it's always "up"); `WorldToScreen` applies the rotation. Impl tracked in **#35**.
- **[vs main]** `Camera2D` is position-follow only (`Target` + `PixelsPerUnit`); **no rotation** yet.

## 3. World

- **[DECIDED]** **Fixed, open world** + separate **dungeon areas** you "zip off to".
- **[DECIDED]** **Stream areas on demand** — only load what's needed.
- **[DECIDED]** **World editing is first-class** — data-driven, authorable, with tooling, designed
  for *now* (it's the blocker that always bites later).
- **[DECIDED]** The world is **handcrafted** (hand-authored layout, not procedural).
- **[DECIDED]** It is **chopped into chunks** and streamed by **proximity**: a **load radius** around
  the player, and when that radius collides with an area/chunk, its assets start loading.
- **[DECIDED]** **Dungeons are NOT separate instances** — entering one **teleports you to another part
  of the *same* world** once that part is loaded. One contiguous coordinate space + teleport + stream,
  not a separate scene.
- **[DECIDED]** **Everything is authored world data** — quest markers, save zones, resurrect points,
  vendors, NPCs, mining/material nodes, dungeon entrances/teleports, spawns.
- **[DECIDED]** Data format is **TOML or JSON** (pick one), chosen to be both editable and streamable.
- **[DECIDED]** Editing is a **hybrid** — place/move entities **in-game**, plus an **external toolbox**.
- **[OPEN]** The **chunking scheme** — *how* the world is partitioned into streamable chunks (fixed
  grid? chunk size? regions-made-of-chunks?). The core remaining question of #115.
- **[OPEN]** TOML vs JSON (pick one); how the in-game editor and external toolbox **share/sync** the
  same data; load-radius & chunk-size tuning; whether the origin region unloads on a dungeon teleport.
- **[vs main]** `BattleForceWorld` is one in-memory bag of quest markers — no streaming, areas,
  dungeons, or editable data.

## 4. Startup & character flow

- **[DECIDED]** Company screen → title screen(s).
- **[DECIDED]** Title screen starts **ignorant of saves and control method**; **Start enables only
  once save state is known** (a save exists, or confirmed none). *(Already true on `main`.)*
- **[DECIDED]** **The device that presses Start selects the control system** for the session.
- **[DECIDED]** The control choice is **locked for the session** — the device that presses Start is
  the control method until the game restarts. *(This overrides the current `FirstActiveShipInput`
  free per-frame arbitration: the input system must lock to the chosen device, not switch.)*
- **[vs main]** `FirstActiveShipInput` currently hands over between devices every frame — needs to
  become "lock to the device chosen at Start."

### Company screen **[DECIDED]**
- Shows the **Olive studio logo**, for ~**2 seconds** (already the default duration on `main`).
- **Static is acceptable**; a **fade on/off** is a nice-to-have, not required.
- Implication: `CompanyScreen` must become `IRenderable` and draw the logo — needs the **logo asset**
  (you have it) added to `Content` + `Content.mgcb`. This is the first "make a screen actually draw"
  task, and it's asset-ready.
- **[OPEN/vs main]** A fade needs **opacity/tint on `Sprite`** — today `Sprite` carries no
  colour/alpha and `MonoGameRenderer` always draws `Color.White`. Small addition, only if you want
  the fade.

### Title / Menu screen **[DECIDED look; assets to come]**
- Looks like the provided mockup: dark **space + planet/nebula glow** background, the **BATTLE FORCE
  2249** title logo, hero **character art**, and a **PLAY** button (this is the focusable START
  button; shown as "PLAY"). Owner supplies the assets.
- The renderer draws **sprites, not text** (no font yet), so the background, logo, characters and
  PLAY button are all **sprite assets**. This is the `MenuScreen` render.
- **[OPEN]** Whether the three hero characters shown are just title art or tie to character-select.

### v1 flow **[v1] [DECIDED]**
```
Company → Title  (Start enables once save state known; the pressing device picks the control system)
   ├─ no save → Character Select (templates; 1 for now)
   │            → pick → create that character's ship (+items)
   │            → world.Introduce(ship) → write save (tagged with character) → Game world
   └─ save    → Game world at last save point (continue)
```

### Future **[FUTURE]**
- Multiple characters + multiple saves (WoW-style **roster**): Start → character-select shows
  *created characters* (continue) **and** *creation templates* (new). Designed-for, **not built**.
- **[DECIDED] Forward-compat guardrails (cheap, take now):** the save **records which character it
  belongs to** even though there's one; character-select stays **data-driven** (a length-1 list).
  So multi-save later is *additive*, not a re-model.
- **[vs main]** No `Character`/story concept and no character-select screen exist.

## 5. Characters & story

- **[DECIDED]** A **character bundles**: a fixed starting **ship**, potentially a distinct
  **starting point**, and its own **story arc**.
- **[DECIDED]** **Story shape (WoW model):** shared endgame; characters differ in *how they get
  there*. Quests are **mostly shared, some character-specific** → a quest carries a **scope**
  (shared vs character-specific).
- **[DECIDED]** The first character is the Disgraced; `DisgracedShip.Handling` is *that character's*
  ship, not a global.

### Character templates **[DECIDED roster; v1 selectable = Disgraced only]**
Four planned templates. Each bundles a **personality/fiction**, a **ship with a distinct profile**,
a **starting location**, and a **story-arc theme**. (Fiction + arcs are content; numbers are feel,
tuned later.)
- **Pirate** — a no-rules race of hoarding and stealing. Ship is a cobbled-together mess of
  technologies that somehow works; some weapons can be genuinely good. *Start: the construction
  site near the homeworld.*
- **Diplomat** — diplomacy first, shoot later; but the ships are solid and dangerous ("shoot and be
  blown out of the sky"). Arc: has to make some tough decisions.
- **Disgraced** — kicked out of the race for causing an uproar on the home planet. Everyone feels
  like an enemy, but there are genuine people to reach. A political movement back home is revived
  and must be met head-on. *Start: the mines.* **(The v1 character — the first one developed.)**
- **Trader** — ruthless; won't tolerate dithering, cleans up the mess later.

**Structural implications for the model:**
- A **character maps to a ship *profile***, not just handling — ships differ by **handling**,
  **weapons/loadout**, and **build quality / durability** (Pirate: fragile-but-armed; Diplomat:
  solid/tanky). Same physics (`ShipMovement`), different `ShipHandling` + loadout + durability per
  character — consistent with "another set of these, not another physics."
- A character therefore carries: identity/name, personality (content), **ship profile**, **starting
  location**, **story-arc theme**.
- **[DECIDED]** v1 develops **Disgraced only**; the other three are future templates.
- **[DECIDED]** Disgraced starts **in the mines**.
- **[OPEN]** Starting locations for Diplomat and Trader (Pirate = construction site; Disgraced = mines).
- **[OPEN]** Reconcile with Quest 1 ("the debris field is collapsing around you"): is the mine the
  thing collapsing into that debris field (same opening beat), or a separate location?
- **[OPEN]** The concrete ship profiles (handling numbers, weapons, durability per character) — feel/
  content, tuned later.

## 6. Ship, items, inventory ("model now")

### Entity model (skeleton) **[DECIDED]**
- **`CharacterTemplate`** (content; the 4 — Pirate/Diplomat/Disgraced/Trader): id, name,
  personality + story arc, **ship profile** (starting handling + starting loadout), start location,
  starting inventory.
- **`Character`** (instance; **persistent**; saved): template, progression (XP, level, spend points,
  gifts), credits, reputation (per group), **inventory (owned items)**, quest/story progress, and the
  **current `Ship`**. **Persists across ship changes.**
- **`Ship`** (instance; **per-ship, transient**): `Handling`; **loadout** (equip slots — weapon(s) /
  shield / engine… — filled from the character's inventory); **health pool + current health** (the
  ship's own — a new ship is a new pool); **shield** (from the equipped shield item) + current shield;
  **durability**; drives `ShipMovement` (existing physics).
- **Gifts (pilot) can modify ship stats** — e.g. +hit points — so effective ship health = ship base
  + pilot gift bonuses.
- **`Player`** (existing engine entity) = position in the world. `GameSession` owns `Character` +
  `Ship`; `world.Introduce(ship)` places it.

Locked placement decisions: **inventory on the Character**, **loadout on the Ship**; **health is the
ship's** (gifts boost it); **XP / credits / reputation / gifts / inventory persist on the Character**.

- **[DECIDED]** A **ship is a transient instance**, **owned by the `GameSession`**, created per game
  from the character's ship handling. *(Fixes the wrong global `AddSingleton<ShipMovement>()`.)*
- **[DECIDED]** A ship has: static **handling** (content: accel/drag/turn) + mutable **ship
  attributes** (state) + **items**.
- **[DECIDED]** **Inventory = items**, belonging to the ship (introduced with it).
- **[DECIDED]** **Items degrade** (conventional durability): **use** wears them gradually; **death**
  is the big hit (the primary death penalty). Durability is saved state.
- **[DECIDED]** When durability bottoms out, the item **stops working until repaired** — disabled,
  not destroyed (WoW-style). Death stings gear condition without losing the gear.
- **[DECIDED, partial]** Items are **collectible** (awarded through the quest chain), **categorised**
  (shields are one category, each with **variants**), and **equippable** (loadout). → **quests award
  items.**
- **[DECIDED] Loadout (v1, fixed):** **4 weapon slots** + **2 shield slots** + **2 "additional"
  slots** (orbs / defensive items) — all **empty at new-game start**. A collected item **auto-slots**
  into a free matching slot on pickup; otherwise the player **drag-and-drops** it in.
- **[FUTURE]** Slot count may **vary by ship** — *not at the start*; v1 ships use the fixed layout above.
- **[DECIDED] Inventory:** **slot-based**. Items **stack per slot up to a per-item-type max** — some
  types stack to **99**, some are **1 per slot**; it depends on the item type.
- **[NOTE]** Drag-and-drop is one interaction that serves **both mouse and touch** — helps the
  "mobile without a UI redesign" goal.
- **[OPEN]** Number of inventory slots; the full item category list; and **repair** (shops? save
  zones?).

### Economy & acquisition **[in progress]**
- **[DECIDED] Credits/currency** exists.
- **[DECIDED] Vendors** sell items for credits, but vendor gear is **deliberately mediocre**
  (WoW-style — not best-in-slot; the good stuff comes from elsewhere).
- **[DECIDED] Crafting is important** — a primary route to good gear.
- Acquisition paths: **quests** (rewards) · **vendors** (credits, mediocre) · **crafting**
  (important) · collectibles.
- **[NOTE]** The Disgraced starts **in the mines** — a natural materials/mining source feeding
  crafting; worth tying the opening to the economy.
- **[OPEN]** Crafting model — recipes, materials/resources, stations/skill. **New subsystem to
  design.**
- **[OPEN]** How credits are earned; whether repair costs credits.
### Player attributes **[in progress]**
- **[DECIDED] Health** — the vitality of the player's ship/pilot.
  - **Shield first, then health:** if a shield is fitted, incoming damage depletes the **shield**,
    then bleeds into **health**.
  - **Regen out of combat:** after a period with no hit, it **recharges health first, then shield**.
  - **Shield is an equipped item** (part of the loadout), not innate. **You don't start with one** —
    a shield is acquired **later in the quest chain**, and there are **several shield items to
    collect** (variants). Early game (Disgraced start) has no shield → damage goes straight to health.
- **[DECIDED]** Health reaching 0 = **death** → respawn at a resurrect point + the item-degradation hit.
- **[OPEN]** The out-of-combat delay before regen begins, and the regen rates — feel/tuning.
- **[DECIDED] Progression:** **XP** is earned from doing things; each **level** requires a set
  amount of XP; every level gained grants **one spend point**; spend points buy **gifts** (a
  perk/ability system — "additional gifts"). So the player carries: **XP, level, unspent spend
  points, and chosen gifts**.
- **[OPEN]** What awards XP (quests / combat / exploration?) and the XP-per-level curve — tuning.
- **[DECIDED, shape] Gifts are Diablo IV-style** — spend points buy gifts that are **stat boosts**
  (e.g. additional hit points) as well as perks/abilities, affecting **both ship and pilot** stats.
  This **is** the skill/perk/upgrade system (so "skills/aptitudes" = gifts, one system).
- **[OPEN]** Gift **structure** (a board/tree like D4's Paragon, or a flat list), the **catalogue**,
  and whether a **respec** is allowed — content/design.
- **[DECIDED] Reputation/standing** — tracked **per group/faction** (many groups). Doing a group's
  quests **gains reputation** with them, and reputation **thresholds unlock rewards** (WoW-style
  faction rep → another acquisition path). The Disgraced also has a reputation level with their own
  movement/home faction. Fits the fiction: everyone an enemy at first, genuine people to reach.
  → a **quest belongs to a group** (whose rep it grants) as well as carrying a scope (shared /
  character-specific).
- **[OPEN]** The group list, reputation tiers/thresholds, whether standing can go hostile/negative,
  and whether standing gates NPC reactions/quests — content/tuning.
- **[DECIDED]** "Skills/aptitudes" **are** the gifts system — one system, not a separate stat.
- **[vs main]** No ship-instance aggregate, no items/inventory, no attributes — only `ShipMovement`
  (physics) + static `DisgracedShip` numbers.

## 7. Input & control

- **[DECIDED]** `IShipInput` seam — **engine provides the service, host provides the device**
  (documented + built engine-side).
- **[DECIDED] Input routing:** the **UI has first claim**; **when nothing is focused, input feeds
  the ship.**
- **[OPEN/needed]** Routing needs a focus **query** on `IUIController` (e.g. `HasFocus`) — doesn't
  exist yet — plus a single input dispatch point.
- **[vs main]** `IHost` has **no input seam** and `BattleForceGame.Update` reads no device, so
  nothing reaches the menu or the ship — the documented Known Gap behind the black screen.

## 8. Save model

- **[DECIDED] Save payload (per character):** character identity · position · inventory · ship
  attributes · player attributes · quest progress.
- **[DECIDED] Save zones:** authored **world data** (regions); **entering auto-triggers a save**
  (once, on entry). Reuses the proximity pattern (`QuestProximityWatcher`) — presentation measures
  the player against markers.
- **[DECIDED]** Continue = the last save.
- **[vs main]** Save is position-based today; needs character identity + the richer payload.

## 9. Death & combat

- **[DECIDED]** On death → **respawn at a resurrect point** + **major item degradation** (the death
  penalty). Progress/items aren't lost, gear condition is.
- **[DECIDED]** **Resurrect points are separate** from save zones — dedicated respawn locations
  (WoW-style graveyards vs rest areas), authored as world data like save zones.
### Combat **[in progress]**
- **[DECIDED] Firing (the 4 weapon slots):**
  - Slot 1 = **primary**, fired on the **A button**.
  - Slot 2 = **secondary**, fired on the **B button**.
  - Slots 3 & 4 = **auto-firing** weapons (e.g. missiles, auto-cannons).
  - So a weapon is either **manual** (A/B) or **auto-fire**. A/B are the device's fire buttons — the
    input system maps them to keyboard keys and on-screen touch buttons (the device locked at Start).
- **[DECIDED] Weapon behaviour is per-weapon (data-driven).** Most fire **forward** (the ship's
  facing). Guidance varies — some **lock on (homing)**, some are **dumb** (unguided). Fire pattern
  varies — **1-way / 3-way / 5-way / multi-fire** spreads. So a weapon carries: **direction**,
  **guidance** (homing vs dumb), and **spread pattern**.
- **[DECIDED] Shield slots (2) — free choice, typed shields.** Shields have types — e.g.
  **absorption** (soaks damage; the shield layer that depletes before health) and **reflecting**
  (bounces damage back at the attacker). The player fills the 2 slots freely: **two of a kind doubles
  the effect** (double absorption, or double reflect), or **mix them for a combo**.
- **[DECIDED] Orb / "additional" slots (2):** an **orb is an auto-controlled companion object** that
  acts on its own — e.g. one that **circles the ship** (orbiting defence), a **spiky object that
  tracks** enemies (autonomous attacker), a **ball**. Data-driven behaviours; **no manual fire
  input** — they run themselves. *(Assume free-choice / stack-or-combo across the 2 slots like shields
  — confirm.)*
- **[DECIDED] Firing constraints are per-weapon.** Some weapons **consume ammo** (drawn from
  **stackable ammo items** in inventory — the 99-stack kind); the **initial/starter weapons don't**
  (ammo-free, so early game isn't gated on ammo). Some weapons need a **charge time** before firing.
  (Others are cooldown/rate-gated.) So a weapon carries: ammo (none / a stackable type), charge time,
  and fire rate.
- **[DECIDED] Targets:** you fight **hostiles** (ships / factions / creatures / turrets) **and
  quest-related objects**; hostility ties to reputation/faction. **Enemy AI varies** — some **lock on
  and chase** the player.
- **[DECIDED] Loot:** enemies and objects **drop items into the world**; normal drops are **collected
  on proximity** and **drift toward the player** (loot-magnet — no button, so it suits touch too).
  Feeds inventory, crafting, and the economy.
- **[DECIDED] Quest items auto-collect** the moment you **destroy** the quest object — instant and
  guaranteed (not proximity-drift), so a required drop can never be missed / soft-lock a quest.
- **[OPEN]** How the **player's** homing weapons / lock pick their target (nearest / in-arc / cycled);
  other shield types & reflect detail; enemy roster & drop tables (content/tuning).

## 10. Ownership & lifecycle (architecture seam)

- **[DECIDED]** `GameSession` **owns** the transient ship instance (created per game from the
  character's handling).
- **[DECIDED]** `world.Introduce(ship + items)` **places** it — spawn for a new game; saved
  position on resume.
- **[DECIDED]** The **world is the single authority on placement** — the save/spawn is passed
  *into* `world.Introduce`, which decides where you end up (new game → spawn; resume → saved
  position). No separate override step.
- **[DECIDED]** `GameScreen` reads `session.Ship` rather than being injected a singleton.

---

## 11. Quests

- **[DECIDED] Objective types — lots of variety, all of these (and more):** destroy X · reach a
  location (proximity, like Quest 1) · collect/deliver · escort · talk-to · survive/defend. Data-driven.
- **[DECIDED] Givers & acquisition:** many giver types (NPCs, boards, world triggers). Some quests
  **auto-start** (like Quest 1); some need you to **interact with a giver** to accept. **The giver is
  not necessarily where the quest ends** — turn-in can be elsewhere.
- **[DECIDED] Structure:** **quest chains** (sequences) and **prerequisites** — a quest can be gated
  behind other quests / conditions.
- **[DECIDED] Level / difficulty:** quests have a **level**; some are tougher (level-dependent), and
  this is **shown to the player** (an indicative difficulty marker, WoW-style).
- **[DECIDED] Rewards:** every quest gives **XP + credits** at minimum; plus a **reward item** (fixed)
  and/or a **random piece with a % chance** to drop. **Faction quests award reputation** with that
  faction.
- **[DECIDED] Scope:** **shared story quests** and **character-specific quests** (a quest carries a
  scope + a faction, per §5).
- **[DECIDED] Mission log (UI):** view each quest's **details and requirements to complete**.
- **[DECIDED] On-screen quest display (HUD):** an active-quest tracker on screen (objectives /
  progress). → feeds the **HUD** subsystem.
- **[OPEN]** Reward choice (pick-one vs all); drop %s / reward tables; the full objective-type list;
  branching & choices (the Diplomat's "tough decisions"); exact turn-in flow.
- **[vs main]** Engine has the Pilgrimage quest system + Quest 1 (proximity). New: varied objective
  types, givers/accept, chains, pre-reqs, levels, reward tables, faction rep, mission log + quest HUD.

## 12. NPCs & dialogue

- **[DECIDED] NPCs:** all types — quest givers, vendors, story characters — as world-data entities;
  can be **dynamic** (move/patrol) or **static**.
- **[DECIDED] Dialogue:** yes — **simple text** conversations.
- **[DECIDED] Interaction:** start a conversation with **interact / space bar** on a nearby NPC
  (proximity + the mapped interact button — routes through the input system).
- **[OPEN] Consequences:** whether dialogue **choices** change outcomes / reputation / which quests
  open — undecided (relates to the quest branching in §11).
- **[OPEN]** Whether talking **pauses flight** or plays out in-world; voice (assume text-only for now).

## 13. HUD

- **[DECIDED] Player icon — top-left:** a **health bar + shield bar**, with the player's **level**
  shown on the icon.
- **[DECIDED] Weapon stats — bottom-middle:** status of the equipped weapons (A/B + auto-fire) —
  ammo / charge / cooldown.
- **[DECIDED] XP bar — full width across the very bottom**, beneath the weapon stats.
- **[DECIDED] Target lock indicator — diegetic:** drawn **in the game world** on the target, not as a
  screen overlay.
- **[DECIDED] Interaction / key prompts — bottom section** (needs specific graphics).
- **[DECIDED] Quest tracker (§11)** — on-screen active-quest display (corner; exact position TBD).
- **[DECIDED] Layout:** standard desktop; **mobile = circular overlays** (virtual thumbsticks +
  buttons drawn as circles over the same HUD — no redesign). → answers much of **#118 touch UI**.
- **[OPEN]** **Radar / minimap** — undecided ("not sure"). Quest-tracker exact position; prompt
  graphics (content).
- **[vs main]** No HUD exists on main (known gap). Needs HUD elements + a UI-to-renderer bridge on top
  of the Sprite renderer + `IUIController`.

### PoC reference **[confirms direction]**
A proof-of-concept mockup validates the visual direction and much of the spec: 2D top-down ship
pointing **up** with engine thrust in a **debris field** (Quest 1 / mines opening); a **diegetic
target-lock reticle** (top); an **in-world objective marker** — a green ring **beacon** with a text
label ("Reach The Beacon") — for reach-location objectives; **two circular touch controls**
(left/right) as the mobile overlay; and a full-width **bottom bar**.
- **[DECIDED]** Touch controls: **left circle = movement** (helm/thrust), **right circle = firing**
  (weapons / A·B). → closes most of **#118 touch UI**.
- **[DECIDED]** **Reticle (lock target)** and **beacon (objective waypoint)** are distinct HUD/world
  elements.
- **[OPEN from PoC]** What the **bottom bar** shows (health / shield / XP).

## Reconciliation: spec vs `main`

Structural scan confirms `Character / Ship / Inventory / Item / Area / SaveZone / ResurrectPoint /
Attributes / Introduce` = **0 files on main** → all NEW. Verdict per subsystem:

**KEEP (no change):** Physics (`OliveGameStudio.World`) · Quest engine (`Pilgrimage`).

**EXTEND (existing code, additive):**
- Rendering: ✅ Camera NaN guard (#128), ✅ camera rotation (#127) — **DONE**. Still open: Sprite
  alpha (**#112**).
- UI: ✅ `HasFocus` (#123) — **DONE**. Still open: touch layout (**#118**, gated).
- Quests: ✅ swept proximity (#105) — **DONE**. Still open: quest scope shared/character-specific.
- Saves: ✅ serialise writes / no torn file (#129) — **DONE**. Still open: richer payload (gated).
- Screens: Company logo (**#114**, needs asset); Menu render (new-ish).
- Localisation: language selection (**#33**, gated).

**REWRITE (existing seam, reshaped):**
- Input: ✅ **DONE** (#126) — `OliveGameStudio.Input` (`InputRouter`), `IHost.Input(InputFrame)`
  push model, UI-first routing via `HasFocus`, device chosen at Start.
- Ship provisioning: `AddSingleton` → `GameSession` owns a **transient** ship built from the
  character's profile (**#5**) — still open (needs the Character type).

> **NOTE:** our tracking issues #8, #35, #57, #63, #113, #7 are now **duplicates of merged work**
> (#105, #127, #128, #129, #123, #126) — close them.

**NEW (build fresh; several gated on open decisions):**
- Character system (`Character` + 4 templates + ship-profile mapping + Character-Select screen) —
  partly decided (Disgraced/mines/roster); profile numbers open.
- Ship aggregate (handling + attributes + items/durability) — partly decided; attribute/item detail
  open (#119/#120).
- Item / inventory model — gated (#119 remainder + inventory location).
- Player attributes — gated (#120).
- World: areas/streaming/dungeons/editing + `IWorld.Introduce` + save zones + resurrect points —
  `Introduce`/placement/save-zones/resurrect **decided**; area/streaming/editing model open (#115/#116).

**Bottom line:** mostly **keep + extend**, **two rewrites** (input, provisioning), and a set of
**new** systems gated on the remaining decisions. **Nothing foundational conflicts → do not bin.**

### Build order (skeleton before feature)
1. **Skeleton (authored deliberately, reviewed, behind CI):** the seams everything hangs off —
   `Character`, the `Ship` aggregate, `IWorld.Introduce`, the input seam on `IHost`, and the
   provisioning refactor (#5/#7).
2. **Feature (Dev agent, behind CI):** fill in against the fixed shape — screens, item types,
   quests, per-character profiles.

### Code shape (OO) **[DECIDED]**
- **OO codebase** — classes with encapsulated state + behaviour (as `main` already is), not
  ECS/data-tables.
- **Every object's stats live in one clear, consistent place** — a dedicated **stats type per object**
  (weapon · ship · character · shield · orb · item), following the existing `ShipHandling` pattern
  (`DisgracedShip.Handling`). The stats for *any* object must be **easy to find and tune** — which is
  also what makes the "feel" pass tractable. Every feature issue names the stats type it introduces.

## Consolidated open questions
1. Camera: world-rotates-around-ship vs independent orientation.
2. Area model + dungeon instancing; streaming granularity.
3. World editing: tool, data format, audience.
4. Control choice: session-locked vs switchable; device-only vs UI-paradigm.
5. Touch + focus UI coexistence.
6. Item durability-zero behaviour; item model detail; repair.
7. Player attribute list.
8. Resurrect points vs save zones.
9. Placement authority in `Introduce`.

## Parked (deliberately deferred)
- Combat detail. · Quests content (character-specific vs shared authoring). · "Feel" (handling
  tuning — human play only). · Multi-character roster (future version).
