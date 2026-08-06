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
Held by the model rather than by the content: a trigger is measured against the whole journey a
frame covered, so it fires at any frame length and marker tolerance is a matter of how forgiving
the objective should feel rather than the only thing standing between the player and a quest that
sometimes does not start.

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
| 1. Flying feels good | Being exercised. The ship has thrust, a helm, momentum through a turn and a frame-rate-independent reach, and triggers are swept across the frame rather than sampled at the end of it, so a marker fires at any frame length rather than relying on the markers being tolerant enough. The ship is now on screen too, and the camera turns with it: the ship's nose is pinned to the top of the window and the world rotates around it, so a turn reads as the world swinging past rather than as a sprite spinning in place. A keyboard and a gamepad are now bound, so a person can fly quest 1 rather than only a test — which makes "does flying actually feel good?" answerable for the first time, and it has not been answered: the handling in `BattleForceShip` has only ever been checked against quest 1's distances, never felt. One thing still short of the pillar: with the camera holding the ship in the middle of an empty background, full thrust looks the same as a standstill until there is something fixed to read motion against. |
| 2. The conspiracy finds you | Held. Quest 1 opens on immediate personal survival. |
| 3. Locations have layers | Barely begun. The world still models coordinates and markers are still held per quest — but a place now has a name that is not a coordinate: content says the Disgraced starts in `mines`, and `IWorld.Introduce` is what turns that into a position. Content states no coordinates at all. That is the smallest form of the identity this pillar asks for, and nothing yet reads a place twice or means anything different on the second visit. |
| 4. The world was here first | Partly expressed. The save carries position and quest state only — there is no persistent record distinct from perishable position yet, and world progression currently advances from the game screen. The rule against silently discarding the record is honoured early: a save that could only not be *read* is played over but never written to, so a file locked for a moment is not replaced by a new game. A save this build judges *damaged* is no longer discarded either — it is set aside as `save.corrupt.json` before the new game writes, so a refusal this build got wrong is a restart rather than a loss. Nor is the record discarded by a save that contradicts itself: one naming a quest twice restores to the furthest state it names, so a finished campaign cannot be handed back by an entry standing behind it. Nor by one junk line beside good progress: an entry naming no quest is dropped and the campaign saved next to it is kept, where refusing the file would have lost both and lost them for good. That is the answer the persistent record will need, arriving before the record it protects. The record itself has now arrived in the model: `Character` holds experience, credits, standing, possessions and quest history, and it is deliberately separate from the `Ship`, which is built per game and thrown away — so losing the ship cannot take any of it. What has *not* arrived is the persistence: the save still carries position and quest state only, so the record survives losing the ship but not closing the game. Widening the save shape is the next thing this pillar asks for. One smaller expression of the pillar has just landed whole: the world does not pause for a conversation. NPCs walk their routes on every frame the game screen updates, whether or not the player is talking to somebody, and the ship stops during one only because the conversation took the input — not because anything froze. What that costs the player is paid for by the way out being one press, on any line, on the frame it was asked for. |
