# Olive Game Studio

> Cast out and left for dead, you pilot your ship through a galaxy-wide cover-up that finds
> *you* — because you were desperate enough to be in the wrong place at the wrong time.

This repository holds **Battle Force 2249**, a top-down, physics-based, single-ship living-world
RPG for desktop, and the **Olive Game Studio** engine it is built on. Both are developed
test-first.

| | |
| --- | --- |
| **Design canon** | [`docs/DESIGN.md`](docs/DESIGN.md) — the premise, the four pillars, and what they demand of the code |
| **Architecture** | [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — project layout, the engine/game split, how the systems fit |
| **Workflow** | [`docs/WORKFLOW.md`](docs/WORKFLOW.md) — how issues are labelled, routed and handed off |
| **Changelog** | [`CHANGELOG.md`](CHANGELOG.md) |

## The four pillars

Battle Force 2249 is held together by four commitments. They are constraints on the code, not
marketing copy — [`docs/DESIGN.md`](docs/DESIGN.md) spells out what each one asks for.

1. **Flying feels good** — piloting is the minute-to-minute verb.
2. **The conspiracy finds you** — personal stakes before world stakes.
3. **Locations have layers** — you return to places with new understanding, not to new places.
4. **The world was here first** — it runs without you and outlives your incarnations.

Danger gates the space; content gates the story. Nothing stops you flying somewhere above your
weight — the zone does not lock, it kills.

## Layout

```
src/
  OliveGameStudio.*        the engine — no game content
  Pilgrimage               the quest system — standalone, no project references
  BattleForce2249.*        the game — all content lives here
BattleForce2249.MonoGame/  the MonoGame entry point
tests/                     xUnit, one project per source project
docs/                      design, architecture, workflow
```

The rule the split turns on: **the engine provides the service, the game provides the content.**
Saved progress and localised text both follow it.

## Building

Requires the .NET 10 SDK. MonoGame restores its own tooling from `BattleForce2249.MonoGame/.config`.

```bash
dotnet restore OliveGameStudio.slnx
dotnet build OliveGameStudio.slnx
dotnet test OliveGameStudio.slnx
dotnet run --project BattleForce2249.MonoGame
```

CI runs the build and the full test suite on every push to `main` and every pull request
([`.github/workflows/build.yml`](.github/workflows/build.yml)), so a change arrives with a result
already against it.

## Status

Engine, screen flow and the quest system are in. Quest 1 auto-starts on a new game, tracks the
player forward through the collapsing debris field, and completes at the exit marker, with
progress persisted across sessions and its title translated into seven languages.

A save the game cannot read never stops it and is never lost: one that was merely locked is played
over and left alone, because it may be intact, and one this build refuses is moved aside to
`save.corrupt.json` before the new game writes — so a shape a later build could read is still
there. If it cannot even be moved, nothing is written at all.

A save that contradicts itself costs no progress either: one naming the same quest twice — once
completed, once never started — resumes completed, because the furthest state a file records is
the one taken, whichever line it is on. That is the only reading whose answer does not depend on
the order the entries are in, and a duplicate can only come from a file that was hand-edited or
merged, where the order says nothing. See
[the saved progress notes](docs/ARCHITECTURE.md#saved-progress).

Two ways the engine could fail quietly are closed. A menu press stays with the button it started
on, so a button that disables itself as it activates — the menu's own idiom — no longer hands the
press to whichever button focus landed on, and the wrong action no longer runs on release. And a
screen navigation that redirects round in a circle now throws, naming the path it took, instead of
spinning inside the call while the update loop stops ticking and the window goes black. See
[the screen flow and menu input notes](docs/ARCHITECTURE.md#screen-flow).

A third is closed with them: two buttons named the same thing are two buttons. One controller is
shared by every screen, so two screens are free to label a button `BACK` without either author
knowing the other did — and while UI elements were records, `==` compared names and both resolved
to whichever was registered first. One screen's `Disable` greyed out the other's button and one
screen's `OnReleased` overwrote the other's handler, silently. Elements are identities now, so a
button is only ever itself, and asking the controller about one it does not hold says so instead of
answering about its namesake. See [the UI element notes](docs/ARCHITECTURE.md#ui-elements).

The game is now flyable by a person. Enter or Space starts it from the menu, W/S/A/D or the arrow
keys fly the ship, and a gamepad's left stick and A button do the same — whichever device presses
start is the one the game is played on for the session, so a pad left plugged in with something
resting on the stick cannot take the ship from somebody flying it on the keys. Input is pushed into
the game one frame at a time and routed UI first: it works the menu while a button is focused and
flies the ship when none is. See [the input notes](docs/ARCHITECTURE.md#input).

Not yet built:

- **Strafing** (#7) — the ship has thrust and helm, so it rotates and burns ahead and astern, but
  there are no lateral thrusters. Moving sideways means turning, burning and turning back.
- **On-screen key prompts** — nothing tells the player which keys fly the ship or which device the
  game locked to.
- **Any on-screen quest display** — no HUD, no quest log. The session knows when the player's
  progress is not being saved, but there is nowhere yet to tell them.
- **Language selection** — translations follow the machine's own culture.
- **A persistent record** — experience, credits and quest history that survive death, which
  pillar 4 calls for.
