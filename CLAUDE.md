# Working in this repository

Read [`docs/DESIGN.md`](docs/DESIGN.md) before changing behaviour. It is the design canon — the
premise, the four pillars, and what each pillar demands of the code. Work that pulls against a
pillar needs saying out loud, not quietly shipping.

[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) describes the layout and the rules that hold it in
shape. [`docs/WORKFLOW.md`](docs/WORKFLOW.md) describes how issues are labelled, routed and handed
off between agents.

## Conventions

- **Test-driven.** xUnit, one `tests/<project>.Tests` project per source project, targeting
  `net10.0`. Cover behaviour, not plumbing.
- **The engine provides the service; the game provides the content.** `OliveGameStudio.*` ships no
  game content. Anything that needs to know about ships, quests or credits belongs in
  `BattleForce2249.*`. `Pilgrimage` is a standalone quest library with no project references —
  keep it that way.
- **The model declares the rule; the presentation applies it.** The quest model holds no
  coordinates and measures no distances.
- **Public types and members carry XML doc comments**, and say *why* where the reason is not
  obvious from the signature. The existing comments set the standard: they explain the decision,
  not the syntax.
- **Anything the player reads goes through localisation.** Identifiers — quest ids, element names,
  asset keys — are not text and are never translated, because a save written in one language has
  to load in another.
- **Pin the culture in any test that asserts on user-facing text.**

## Before you claim it works

**The SDK is installable in agent containers**, so build and run the tests before you hand off.
This file used to say the opposite; the missing step is `apt-get update` first, without which the
`dotnet10` package URLs 404:

```bash
apt-get update && apt-get install -y dotnet-sdk-10.0
dotnet build OliveGameStudio.slnx
dotnet test OliveGameStudio.slnx
```

That restores MonoGame's `mgcb` tooling and builds the host as well as the libraries. If it does
fail for you, say so plainly at handoff rather than implying a green build — but say what failed,
because "no SDK" is no longer a reason. CI runs on every push to `main` and on every pull request,
so a red build is visible before review.

## Keeping the record straight

Pull request bodies and handoff comments are the record of what shipped. When the code moves on,
they have to move with it — a description that no longer matches the diff is a defect in the same
way stale documentation is.
