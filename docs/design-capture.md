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
- **[DECIDED] Data format is JSON.** TOML reads better by hand, but **nothing authors this by
  hand** — placement is in-game and values go through the external editor, so hand-readability is a
  debugging and code-review concern rather than a workflow one. JSON is already the repository's
  format (localisation files, the save game), so it adds no second format and no third-party
  dependency, and `System.Text.Json` is built in, fast and source-generatable — which matters when
  chunks are streamed on proximity.
- **[NOTE]** What this gives up is **comments**, which JSON has no form for. If authored data ever
  wants a note attached — *"this marker is deliberately generous"* — it goes in as a **field the
  editor writes**, not as a comment in the file.
- **[DECIDED]** Editing is a **hybrid**, and the split is by *what the job is*:
  - **In-game — placement.** Drag-and-drop entities into the world, where you can see them in
    context at the scale they will be played at.
  - **External tooling — values.** Setting numbers, stats and properties, where a proper form beats
    poking at things through a game window.
- **[DECIDED]** **The editor is the priority.** Content authoring is the thing that blocks
  everything else in the world model, so the editor leads rather than waits on the chunking scheme.
- **[DECIDED] ImGui is not expected to be enough** for the external tool — it suits debug panels
  rather than the kind of data editing this needs.
- **[DECIDED, provisional] Avalonia** for the external tool — the front runner from the PoC. It
  keeps the toolchain in .NET alongside the game, gives proper forms and data binding for the
  values-editing job ImGui was wrong for, and runs on whatever machine the authoring happens on
  rather than tying content work to Windows.
- **[DECIDED] The editor lives in this repository, in its own project, abstracted from the game.**
  One repo keeps the tool and the content it edits versioned together — an editor that can drift out
  of step with the data format is a trap. But **Avalonia stays inside the editor project**: no UI
  framework reaches `OliveGameStudio.*` or `BattleForce2249.*`.
- **[IMPLICATION]** What the two sides share is the **world data model and its serialisation**, in a
  project both reference — the game to *stream* it, the editor to *read and write* it. That shared
  project is the contract, and it is the thing to design when the data format is chosen. The editor
  depending on the game, or the game knowing an editor exists, would be the wrong way round.
- **[DECIDED] The world is partitioned into hand-drawn regions**, each its own size and shape,
  rather than a fixed grid. The world is handcrafted, so its seams follow the places that were
  authored — the mines, a city, a debris field — instead of an arbitrary lattice laid over them. A
  region is a *place*, and that is what the designer already thinks in.
- **[IMPLICATION]** This puts real weight on the editor: it has to let you **draw and reshape region
  boundaries**, not just drop entities into them. Since the editor is already the priority, that is
  the first thing it needs to do rather than a later addition.
- **[IMPLICATION]** Proximity loading gets harder than arithmetic. With a grid, "which piece am I
  in" is a division; with arbitrary shapes it is a **spatial test against every nearby region**, and
  the load radius has to be checked against shapes rather than cells. Worth an index of some kind
  once there are many regions.
- **[DECIDED] Distance triggers the load; the region is what loads.** The two are separate questions
  and get separate answers. A **load radius** around the ship is the trigger, so warning comes
  smoothly and early as the player approaches; the **region** is the unit that then starts loading,
  so content arrives in a package that was authored as one place. This is the hybrid already implied
  above, stated plainly because "distance-based or region-based" is a false choice — it is both, at
  different jobs.
- **[DECIDED, direction] Whether a region loads whole or progressively is deferred — but the data
  must not assume whole.** With regions hand-drawn and varying in size, loading a large one in a
  single pass is a stall even if the player only clipped its edge. Loading it **progressively**,
  nearest content first, removes any cap on how big a region may be, at the cost of more machinery.
  That choice can be made later **provided entities carry their own positions within the region** —
  which they do — so nothing in the format forecloses it. Whole-region loading is the simpler
  starting point; progressive is where it goes if regions get big.
- **[OPEN]** **How big may a region be, and can regions overlap?** Sizes vary by hand now, so a large
  region is a long load and a small one is cheap — streaming cost has become an **authoring
  decision**, and it needs a stated budget or the world will stutter wherever someone drew
  generously. *(Progressive loading would lift the size cap; until then a budget is the only thing
  holding it.)* Overlap needs a rule too: either regions may not overlap, or the player is in
  several at once and something decides which wins.
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
- **[DECIDED, provisional] Starting locations — a start says where the character *stands*.** The two
  already set follow that rule (Pirate at a construction site, with raw material to scavenge;
  Disgraced down the mines, exiled to the bottom), so the other two do too:
  - **Diplomat — somewhere institutional.** A council station, embassy dock or the diplomatic
    quarter of the homeworld. Starting at the centre of power is what makes the arc's tough
    decisions cost something.
  - **Trader — somewhere commercial.** A market hub, trade docks or a freight waypoint between
    territories. Starting where the money moves fits the ruthlessness.

  Marked provisional: the *kind* of place is settled, the specific location is content to be named
  when those characters are written.
- **[DECIDED] The mine collapse *is* the debris field — one opening beat.** The Disgraced is working
  the mines when they come down, and the wreckage is what Quest 1 flies out through. The existing
  quest text needs no change: *"Get out. The debris field is collapsing around you."*
- **[WHY]** It earns the opening. The Disgraced was exiled to the bottom, and then the bottom itself
  collapses — the last thing they had is taken inside the first minute, which is the moment the
  story starts from rather than a scene before it. It is also **one location to build instead of
  two**, and it is what the PoC mockup already shows: a ship under thrust in a debris field with a
  beacon to reach. The alternative — the mines as home and the debris field somewhere else — costs a
  second location and a reason to travel between them, and buys nothing the first version does not
  already have.
- **[OPEN]** The concrete ship profiles (handling numbers, weapons, durability per character) — feel/
  content, tuned later.

## 6. Ship, items, inventory ("model now")

### Entity model (skeleton) **[DECIDED]**
- **`CharacterTemplate`** (content; the 4 — Pirate/Diplomat/Disgraced/Trader): id, name,
  personality + story arc, **ship profile** (starting handling + starting loadout), start location,
  starting inventory.
- **`Character`** (instance; **persistent**; saved): template, progression (XP, level, spend points,
  gifts), credits, reputation (per group), **possessions (the items owned)**, quest/story progress,
  and the **current `Ship`**. **Persists across ship changes.**
- **`Ship`** (instance; **per-ship, transient**): `Handling`; **carrying capacity** (built-in slots
  + how many cargo bays it takes — see below); **loadout** (equip slots — weapon(s) / shield /
  engine… — fitted from the character's possessions); **health pool + current health** (the ship's
  own — a new ship is a new pool); **shield** (from the equipped shield item) + current shield;
  **durability**; drives `ShipMovement` (existing physics).
- **Gifts (pilot) can modify ship stats** — e.g. +hit points — so effective ship health = ship base
  + pilot gift bonuses.
- **`Player`** (existing engine entity) = position in the world. `GameSession` owns `Character` +
  `Ship`; `world.Introduce(ship)` places it.

Locked placement decisions: **capacity and loadout on the Ship**; **possessions on the Character**;
**health is the ship's** (gifts boost it); **XP / credits / reputation / gifts persist on the
Character**.

- **[DECIDED]** A **ship is a transient instance**, **owned by the `GameSession`**, created per game
  from the character's ship handling. *(Fixes the wrong global `AddSingleton<ShipMovement>()`.)*
- **[DECIDED]** A ship has: static **handling** (content: accel/drag/turn) + mutable **ship
  attributes** (state) + **the capacity to carry**.
- **[DECIDED — clarified] "Inventory" was doing two jobs; they are separate things.**
  - **How much you can carry is the ship's** — built-in slots plus the cargo bays it takes. A
    fighter carries less than a hauler *because it is a fighter*.
  - **What you own is the character's** — possessions are part of the persistent record, saved
    alongside XP, credits and standing, and they **survive losing the hull**.

  Everything decided below about capacity, bays, the sell-up and the refused swap is unchanged; what
  changes is that nothing has to be *moved* on a ship change, because the possessions never lived on
  the ship in the first place. Bays following the pilot becomes structurally true rather than a
  special rule, and there is no transfer step to get wrong.
- **[WHY]** This keeps **pillar 4 — "the world was here first"** intact. `DESIGN.md` names
  possessions as part of the record that survives death and hull loss, and `Ship` is built fresh on
  every start *and resume* then thrown away. A hold living on the ship would have to be captured
  into the save and restored into the newly-built ship, and anything missed there discards the
  player's goods silently — the exact failure the pillar calls out. Capacity is a *ship stat*, so it
  is content and rebuilds harmlessly; possessions are a *record*, so they persist.
- **[DECIDED] Built-in capacity is a ship stat** — each ship has its own slot count (e.g. a starter
  at **16**, a later one at **24**). It is part of the ship profile a character template maps to,
  and a reason to want a better ship beyond handling.
- **[DECIDED] Capacity is also bought** — **additional cargo bays** are purchasable modules that
  **couple up to whatever ship you are flying**. They follow the pilot, not the hull, so a bay is
  bought once and kept: changing ship re-couples them rather than re-charging for them. This is a
  credits sink alongside repair, and an acquisition path in its own right.
- **[DECIDED] Each bay carries its own slot count** — a bay is a piece of content with a **stats
  type** (per the OO stats rule), not a fixed `+N` the inventory hard-codes. Effective capacity is
  the hull's base plus the **sum of the coupled bays' slot counts**, so new bays can be authored
  without touching the inventory model.
- **[DECIDED] Carrying capacity is deliberately limited, and it is capped.** Inventory pressure is a
  **designed constraint**, not an artefact of the numbers — the player is meant to keep choosing
  what to keep, sell or leave behind, and that choice must still bite in the late game. So **slots
  do not grow without bound**: there is a largest bay the game sells, and therefore a **maximum
  total capacity** a fully-kitted ship can reach.
- **[DECIDED] Within that ceiling, cost is what gates you.** Bays are sold in a bounded range of
  sizes (4 and 12 slots are indicative points on it) with **price scaling with size**, so a small
  bay is an early attainable purchase and the largest is a genuine save-up goal. Capacity growth
  comes from **buying bigger bays**, not more of them — and it **stops** at the top of the range
  rather than tracking the player's wealth upward for ever. Wealth buys you to the ceiling faster;
  it does not raise it.
- **[DECIDED] Capacity is gated by credits alone — not by level, hull or quest progress.** A player
  who chooses to **grind** can buy a bigger bay early and carry more sooner; that is a legitimate
  route, and the effort is the price. This is what makes the ceiling load-bearing rather than
  decorative: since nothing but money stands between the player and the largest bay, the **cap** is
  the only thing keeping the constraint real. The two decisions hold each other up — grinding is
  safe to allow *because* capacity tops out.
- **[DECIDED] Capacity is counted in slots, never weight** — there is **no encumbrance system**.
  A slot is the only unit; what an item weighs is not modelled. This keeps "have I got room?" a
  question the player answers by looking, and keeps a bay's value legible at the point of sale.
- **[DECIDED] One ship at a time — the old hull is scrapped on change.** There is no hangar, no
  roster and no second ship to go back to; the previous ship is binned and crushed. This is what
  makes `Ship` a transient instance the `GameSession` owns (§10) rather than a collection the
  character keeps, and it is why the transfer rules above have to be total — nothing left behind is
  recoverable later.
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
- **[FUTURE]** **Loadout** slot count may **vary by ship** — *not at the start*; v1 ships use the
  fixed layout above. (Distinct from **inventory** capacity, which does vary by hull from day one —
  its built-in slot count and how many bays it takes are both ship stats.)
- **[DECIDED] Inventory:** **slot-based**. Items **stack per slot up to a per-item-type max** — some
  types stack to **99**, some are **1 per slot**; it depends on the item type.
- **[NOTE]** Drag-and-drop is one interaction that serves **both mouse and touch** — helps the
  "mobile without a UI redesign" goal.
- **[DECIDED] How many bays a ship can take is a per-ship stat — and it expresses the ship's
  character, not its rank.** The ship you start in takes **two**. A **fighter might take only one**
  because it is a fighter; a hauler built for the job takes more. So the number is a **role
  differentiator**, the counterpart to handling: a nimble ship trades cargo away, a heavy one trades
  agility away. It is deliberately **not** a reward for progressing — a later, better fighter can
  still take one.
- So a ship carries two capacity numbers, both content: its **built-in slots** and **how many bays
  it can take**. Total capacity = built-in slots + the slot counts of the bays fitted to it. Bays
  **re-fit to the new ship** on a change, up to however many that ship accepts.
- **[DECIDED] Outgrowing a bay is a vendor swap.** With only two bays, upgrading (say 4 slots → 12)
  when every bay is occupied means **the vendor takes the old bay off you** and fits the new one.
  Because possessions belong to the character rather than the bay, **nothing has to be unpacked** —
  a bay is capacity, not a container with things inside it. The player's goods are untouched; only
  the ceiling moves. *(A swap that would drop capacity below what they are carrying is refused, as
  below.)*
- **[NOTE]** This is the **most-repeated economic transaction in the game** — capacity is
  credit-gated and grindable, so the swap happens throughout a run rather than once or twice. It
  deserves to be a smooth, one-step vendor interaction rather than a sell-then-buy the player
  assembles themselves.
- **[DECIDED] The old bay is credited back at its sale value.** Items have a buy price and a lower
  sale price (see Economy below), and bays are no exception — so trading a 4-slot up to a 12-slot
  part-funds the purchase without refunding it. The cheap early bay is therefore a sensible buy
  rather than a trap, while the spread means laddering up costs more than waiting and buying big
  once. *(Bay sizes, prices and ships' built-in counts are content, tuned later.)*
- **[DECIDED] Ship changes only ever go up** — nobody wants to go back to an older ship, so there is
  no downgrade path and the previous ship is crushed rather than kept.
- **[DECIDED] If what the player is carrying exceeds the new ship's capacity, the swap is refused.**
  Bay count being a role stat means a newer, better ship can carry *less* — a fighter taking one bay
  carries less than a hauler taking three — so a change can genuinely leave the player holding more
  than the new ship allows. The rule is that the game **does not take the ship change**: they must
  make room first, by selling, using or discarding. Nothing is ever destroyed on their behalf and
  nothing is stranded in a buffer they have to remember; the cost of a narrower ship is paid
  deliberately, before the swap, not discovered afterwards.
- **[NOTE]** This makes the swap a **checked transaction** — capacity is tested against what is
  carried before anything happens, and a refusal has to say *why* and *how much* has to go, or the
  player is left guessing at a vendor. It also means the old ship is only crushed once the new one
  is confirmed to accommodate everything.
- **[DECIDED] The player sells up, and that friction is the point.** Refusing the swap is not a
  safety net around an awkward edge — it is the mechanism that makes **changing ship a conscious
  change of direction**. Going from a cargo-heavy pilot to an uber fighting machine *should* cost
  you the cargo life: you liquidate what you can no longer carry, you shed the bays that will not
  fit, and you commit. A ship is an **identity**, not a loadout choice, and the sell-up is what
  makes the player weigh it rather than drift into it.
- **[DECIDED] A bay the new ship cannot take is sold up with everything else.** It follows from the
  same rule — the player clears what will not fit before the swap is allowed, bays included. No
  storage concept is needed, nothing sits owned-but-unfitted waiting to be remembered, and the cost
  of narrowing your ship is paid in full and visibly at the moment you choose it.
- **[DECIDED] Therefore: a ladder of commitments, not a garage.** Ships are *kinds* that differ by
  role, but the player still holds exactly one and pays a real price to change it — so there is no
  hangar, no swapping ship for the job, and no going back without paying again. This is what keeps
  **`Ship` a single transient instance owned by the `GameSession`** (§10) rather than a collection,
  and it is a deliberate answer to the pull the fighter/hauler split created.
- **[OPEN] Does role-differentiated shipping want a hangar after all?** If ships are *kinds* — a
  fighter, a hauler — rather than rungs, the player will want the right ship for the job and to
  switch back, which is exactly what "one ship at a time, the old one is crushed" forbids. Those two
  decisions are pulling in opposite directions. Either ships stay a strict ladder that happens to
  vary in cargo (and the fighter/hauler framing is flavour, not choice), or ships become a garage
  and `Ship` stops being a single transient instance (§10). Worth settling before either is built
  on. *(See also the §10 ownership seam, which assumes exactly one ship.)*
- **[DECIDED] Scrapping the old ship pays out in credits.** Crushing it is a sale, not a disposal —
  the old ship comes back as money toward the new one. This is the same buy-high/sell-low rule
  everything else follows rather than a special case: a ship carries a sale value below its buy
  price, so a change of ship recovers part of what was sunk into the last one and never all of it.
- **[NOTE]** That makes the whole ship change **one transaction at a vendor**: clear what will not
  fit (sold at sale value), scrap the old ship (credits in), buy the new one. The player sees a
  single running total for the direction they are choosing, which is the moment the commitment
  decision is actually made.
- **[DECIDED] Repair costs credits, and the price varies per vendor.** Repair is a vendor service,
  not a free effect of resting — so the death penalty has a real economic cost and credits have a
  reliable sink. Because the price is **per vendor**, where you repair is a decision: a vendor
  carries a **repair rate** as part of its stats, and the player can be out of pocket for
  convenience. It also gives vendors a role beyond selling deliberately-mediocre gear.
- **[DECIDED] Reputation discounts repair — the better your standing with a faction, the cheaper
  their vendors repair.** Standing has to *pay*, and repair is the ideal place for it: the player
  feels it **every time they die**, which is far more often than a reputation reward unlocks. It
  also gives the per-vendor rate a second axis and creates a real choice at the point of use —
  **a cheap stranger or a well-disposed ally**.
- So a repair bill is **vendor base rate × standing**, both authored: the rate is the vendor's
  character (§ Economy), the modifier is what the player has earned with their faction.
- **[DECIDED] The discount is a percentage, and never the whole bill.** Standing takes a slice off
  the cost — a real saving the player notices — but **repair always costs something**, however well
  regarded they are. That floor is deliberate: repair is the death penalty's teeth, and degradation
  only stings because putting it right costs money. Free repair at high standing would dissolve the
  penalty exactly when the player is deep in faction rep and doing the hardest content, which is
  when it should bite hardest. *(The actual percentages are tuning; that it never reaches 100% is
  the design decision.)*
- **[OPEN]** The full item category list.

### Economy & acquisition **[in progress]**
- **[DECIDED] Credits/currency** exists.
- **[DECIDED] Vendors** sell items for credits, and **most** vendor gear is **deliberately mediocre**
  (WoW-style — not best-in-slot; the good stuff comes from elsewhere). A **rare few** vendors are
  genuinely good — see the specialism decisions below.
- **[DECIDED] Crafting is important** — a primary route to good gear.
- Acquisition paths: **quests** (rewards) · **vendors** (credits, mediocre) · **crafting**
  (important) · collectibles.
- **[NOTE]** The Disgraced starts **in the mines** — a natural materials/mining source feeding
  crafting; worth tying the opening to the economy.
- **[DECIDED] Every item has a buy price and a sale price, and the sale price is lower.** The player
  can always turn goods back into credits, but never at what they paid — the vendor keeps a spread.
  This applies to **everything sellable, cargo bays included**, so the surrendered bay in a
  bay-for-bigger-bay swap credits back at its sale value rather than vanishing or refunding in full.
- **[NOTE]** The spread is doing real work beyond flavour. It makes **churn cost something**, so
  buying and re-selling is not a free way to shuffle inventory; it stops vendors being a lossless
  parking space for items the player cannot carry; and it gives the **sell-up on a ship change** a
  genuine price, which is exactly what that decision wants — changing direction should hurt a
  little.
- **[DECIDED] Vendors specialise, and their prices say so.** Pricing varies **per vendor and per
  category**, not as one flat margin: a weapons expert gives you good prices on ships and weapons
  because that is their trade. Finding the right vendor for what you are buying or selling is
  therefore **knowledge the player accumulates about the world**, and a reason to remember where
  people are rather than using whoever is nearest.
- **[DECIDED] Better stock costs more.** A vendor may be dearer precisely *because* what they carry
  is good, so price is a signal about quality rather than a flat tax. The player trades off **cheap
  and ordinary against expensive and worth it**, which is a real decision rather than an arithmetic
  one.
- So a **vendor carries its own stats type** (per the OO stats rule): a **repair rate**, **per-
  category buy/sell modifiers**, and a **stock quality level**. Vendor pricing is content, authored
  per vendor, not a global economy constant.
- **[DECIDED] Most vendors are bog standard; a few are genuinely good.** Mediocre stock is the
  **norm**, not a ceiling — the ordinary vendor sells ordinary things, and that is what keeps quests
  and crafting the main routes to good gear. But **good vendors exist**, and they are rare enough
  that finding one matters. It is **rarity** doing the balancing rather than a blanket quality cap,
  which is the better lever: it leaves room for a genuinely exciting shop without turning shopping
  into the answer to everything.
- **[NOTE]** This makes a good vendor a **destination** — somewhere the player remembers, travels
  to, and saves credits for. It compounds with per-vendor specialism: knowing *who* is worth the
  trip is world knowledge the player earns, and the sort of thing they tell other players about.
- **[DECIDED] Good vendors are placed, not permissioned.** They are gated by **where they are**
  rather than by rank, reputation or quest state: **some sit off the beaten track**, in special
  places that reward going and looking, and **some are in city areas — but sparse**, so a city is
  not a one-stop shop you can simply browse. Finding them is exploration, not permission.
- **[NOTE]** Because nothing bars the door, a player can **find a great vendor long before they can
  afford it** — and that is the point. The gate is money, exactly as it is for cargo bays, so a good
  shop discovered early becomes something to come back for. It is aspiration rather than a locked
  door, and it gives exploration a payoff that lasts beyond the moment of finding it.
- Vendors are **authored world data** like everything else placed in the world (§3), so their
  location is a content decision made alongside quest markers, save zones and NPCs.
- **[OPEN]** Crafting model — recipes, materials/resources, stations/skill. **New subsystem to
  design.**
- **[OPEN]** How credits are earned. *(Repair costing credits is decided — see §6 items.)*
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
  *(Built in #135 as `ShieldStats` / `Shielding` / `Ship.TakeDamage`. Two details the model had to
  answer to work at all, neither of them decided here: a reflected share **never lands**, so it
  costs neither the shield layer nor the hull; and a pair reflecting **more than the whole hit is
  held at the whole hit**, which makes total immunity reachable from two shields. Both are open to
  being overruled — see the reflect detail still listed as **[OPEN]** below.)*
- **[DECIDED] Orb / "additional" slots (2):** an **orb is an auto-controlled companion object** that
  acts on its own — e.g. one that **circles the ship** (orbiting defence), a **spiky object that
  tracks** enemies (autonomous attacker), a **ball**. Data-driven behaviours; **no manual fire
  input** — they run themselves. *(Assume free-choice / stack-or-combo across the 2 slots like shields
  — confirm.)*
  *(Built in #136 as `OrbBehaviour` / `OrbStats` / `Orbs` / `Ship.Fit`. The flagged assumption is
  answered: free choice, yes — **stack-or-combo, no**, because an orb acts on its own and there is no
  quantity two orbs share, so two of a kind is two companions rather than one effect at double
  strength. Three things the model did **not** answer, none of them decided here: the **ball** is not
  a behaviour, since what it does is stated nowhere; a **tracker has nothing to track** until enemies
  exist (#137), so it holds station and carries no damage number; and each slot takes an **even share
  of the ring**, measured in world terms rather than turning with the hull, so two orbs are not in one
  place and a turning ship does not drag its companions round. All three are open to being overruled.)*
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
- **[DECIDED] Rewards:** every quest gives **XP + credits** at minimum; plus a **reward item**
  (fixed) and/or a **random piece with a % chance** to drop. **Faction quests award reputation**
  with that faction.
- **[DECIDED] The player gets every reward item — there is no pick-one.** A quest hands over all of
  what it awards rather than offering a choice between them. Nothing is missed, nothing is regretted,
  and the turn-in stays a moment of payoff rather than a menu.
- **[NOTE]** The decision this removes has already been made somewhere better. Carrying capacity is
  deliberately limited (§6), so the interesting question is not *which reward do I take* but **what
  do I keep** — and that one is asked continuously, with everything the player owns in view, rather
  than once at a turn-in with no idea what is coming next.
- **[DECIDED] Belonging to the faction pays extra.** A quest for a faction the player stands with
  awards **additional items on top** of its normal rewards. Standing therefore pays in goods as well
  as in the repair discount (§6) — the same principle in a second place: reputation is worth having
  because it keeps handing things back.
- **[DECIDED] The world itself can award goods on a percentage chance**, not only quests. A drop is
  a roll rather than a guarantee, so the same activity can pay differently twice and there is a
  reason to do it again.
- **[OPEN]** Whether the faction bonus scales with **standing tier** or is a flat "member or not".
  Tiered rewards more the deeper the relationship, and reuses the thresholds the rep system already
  needs; flat is simpler to author and to explain.
- **[DECIDED] Scope:** **shared story quests** and **character-specific quests** (a quest carries a
  scope + a faction, per §5).
- **[DECIDED] Mission log (UI):** view each quest's **details and requirements to complete**.
- **[DECIDED] On-screen quest display (HUD):** an active-quest tracker on screen (objectives /
  progress). → feeds the **HUD** subsystem.
- **[OPEN]** Drop %s / reward tables; the full objective-type list; branching & choices (the
  Diplomat's "tough decisions"); exact turn-in flow. *(Reward choice is decided: the player gets
  all of them.)*
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
- Item / inventory model — **placement decided**: **capacity** is the `Ship`'s (built-in slots +
  cargo bays it takes), **possessions** are the `Character`'s (persistent, saved). Matches what is
  already built on `main`; what is new is capacity, bays and the checked ship swap. Item category
  list still open.
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

**Blocking — a decision is needed before the shape can be built.**
1. **The editor comes first.** **[DECIDED — priority]** Being able to *build content* is paramount:
   the editor is the key to everything else in the world model, so it leads rather than follows the
   chunking scheme. What is still open is its **shape** — see §3.
2. **Region sizing and overlap** — how large a hand-drawn region may be before it stutters on load,
   and whether regions may overlap. *(Partitioning is decided: **hand-drawn regions**, not a grid.
   Data format is decided: **JSON**.)* (§3)
3. **Touch is a design-time constraint, not a later port.** **[DECIDED]** Every interface decision
   is made having *already considered touch* — target sizes, reachability, and whether the
   interaction works by tap and drag as well as by focus. The point is not to build touch now but to
   never design something that would have to be torn up for it. §13 fixes the overlay (circular
   controls, left = helm, right = fire); what remains is applying the constraint as each screen is
   designed. (§1)

**Content and tuning — needed eventually, blocking nothing.**
4. The full **item category list** (§6) — weapon, shield, orb, ammo, material and consumable are
   implied by decisions already made; the authoritative list is not written down.
5. **Player attribute tuning** (§6) — out-of-combat regen delay and rates, what awards XP and the
   per-level curve, the gift catalogue and structure (board vs flat list), whether respec is
   allowed, the faction list and reputation thresholds.
6. **Economy numbers** (§6) — ship built-in slot counts and bay counts per ship, bay sizes and
   prices, the repair discount percentages and its floor, vendor buy/sell modifiers.
7. **Character content** (§5) — starting locations for Diplomat and Trader, the concrete ship
   profiles per character, and reconciling the Disgraced's mine start with Quest 1's debris field.

*(Struck off as decided in the body: camera rotation — world rotates around the ship, shipped in
#127 · control choice — locked to the device that presses Start, shipped in #126 · resurrect points
are separate from save zones · `Introduce` is the world's placement authority · the whole cargo,
bay, vendor and repair economy — see §6.)*

## Parked (deliberately deferred)
- Combat detail. · Quests content (character-specific vs shared authoring). · "Feel" (handling
  tuning — human play only). · Multi-character roster (future version).
