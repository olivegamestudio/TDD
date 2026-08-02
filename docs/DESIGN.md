# Battle Force 2249 — design canon

The reference the rest of the work is checked against. When a change and this document
disagree, one of them is wrong; say which before writing code.

## Premise

A conspiracy thriller in space. Cast out and left for dead, you pilot your ship through a
galaxy-wide cover-up that finds *you* — because you were desperate enough to be in the wrong
place at the wrong time.

A top-down, physics-based, single-ship living-world RPG for desktop (keyboard and gamepad), in
the *Escape Velocity* / *Star Valor* lineage.

You are **the Disgraced**: sole survivor of an erased bloodline, no faction, critically low on
everything. The goal at the start is simply to get through the week. One ship, named by the
player, whose name comes to mean something.

## The four pillars

Everything below is a constraint on design and on code, not flavour text.

### 1. Flying feels good

Piloting is the minute-to-minute verb. If the moment-to-moment act of flying is not enjoyable
on its own, nothing layered on top rescues it.

**What this asks of the code.** Movement and physics are never subordinated to another system's
convenience. Systems that react to the player read position; they do not dictate how the player
got there. Triggers are tolerant enough that a ship travelling at speed still fires them — a
trigger a fast ship flies straight through is a bug against this pillar, not a tuning detail.

### 2. The conspiracy finds you

Personal stakes before world stakes. The player is not recruited into a plot; the plot arrives
because of where they already were. Scale escalates outward from survival, never starting there.

**What this asks of the code.** Early content is framed second-person and immediate. The opening
is about getting out alive, not about the galaxy.

### 3. Locations have layers

You return to places with new understanding, rather than being fed new places. A location is
re-read, not replaced.

**What this asks of the code.** A place needs an identity that outlives any one quest, so several
quests can mean something at the same location and a later visit can carry different weight than
the first. Coordinates alone do not give a place identity.

### 4. The world was here first

The world runs without the player and outlives their incarnations. It is not staged for the
player's arrival and does not pause for their absence.

**What this asks of the code.** World progression belongs to the world, not to whatever is
currently on screen. The persistent record (experience, credits, quest history) survives death;
death costs position only. Anything that can silently discard that record is a design failure,
not just a robustness one.

## Gating

**Danger gates the space; content gates the story.** Nothing stops the player flying somewhere
above their weight. The zone does not lock — it kills. Do not add access checks that refuse
travel; add danger that punishes it.

## Build horizon

Episode 1.

## Status against the pillars

Recorded so drift is visible rather than rediscovered. See `docs/ARCHITECTURE.md` for what
exists today.

| Pillar | Standing |
| ------ | -------- |
| 1. Flying feels good | Being exercised. The ship has thrust, a helm, momentum through a turn and a frame-rate-independent reach, and triggers are swept across the frame rather than sampled at the end of it, so a marker fires at any frame length rather than relying on the markers being tolerant enough. The ship is now on screen too, turned to its heading with the camera following it. A keyboard and a gamepad are now bound, so a person can fly quest 1 rather than only a test — which makes "does flying actually feel good?" answerable for the first time, and it has not been answered: the handling in `BattleForceShip` has only ever been checked against quest 1's distances, never felt. One thing still short of the pillar: with the camera holding the ship in the middle of an empty background, full thrust looks the same as a standstill until there is something fixed to read motion against. |
| 2. The conspiracy finds you | Held. Quest 1 opens on immediate personal survival. |
| 3. Locations have layers | Not yet expressed. The world models coordinates, not places; markers are held per quest. |
| 4. The world was here first | Partly expressed. The save carries position and quest state only — there is no persistent record distinct from perishable position yet, and world progression currently advances from the game screen. The rule against silently discarding the record is honoured early: a save that could only not be *read* is played over but never written to, so a file locked for a moment is not replaced by a new game. A save this build judges *damaged* is no longer discarded either — it is set aside as `save.corrupt.json` before the new game writes, so a refusal this build got wrong is a restart rather than a loss. Nor is the record discarded by a save that contradicts itself: one naming a quest twice restores to the furthest state it names, so a finished campaign cannot be handed back by an entry standing behind it. Nor by one junk line beside good progress: an entry naming no quest is dropped and the campaign saved next to it is kept, where refusing the file would have lost both and lost them for good. That is the answer the persistent record will need, arriving before the record it protects. |
