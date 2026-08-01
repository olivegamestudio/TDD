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

## Status

Engine, screen flow, the quest system and ship movement are in. Quest 1 auto-starts on a new game,
tracks the player forward through the collapsing debris field, and completes at the exit marker —
flown there, with thrust and a helm and momentum through the turns — with progress persisted
across sessions and its title translated into seven languages.

Not yet built:

- **A real input device** — the ship is flown through `IShipInput`, and the MonoGame host does not
  bind a keyboard or gamepad to it yet, so the shipping game still has nobody at the controls.
- **Any on-screen quest display** — no HUD, no quest log.
- **Language selection** — translations follow the machine's own culture.
- **A persistent record** — experience, credits and quest history that survive death, which
  pillar 4 calls for.
