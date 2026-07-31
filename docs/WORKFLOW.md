# Workflow

Work moves through this repository as GitHub issues, routed between agents by labels. Each stage
hands off by commenting what it did and applying the next label.

## The pipeline

```
NEED_MORE_INFO ──► READY ──► LOGIC ──► LOCALISATION ──► QA ──► DOCUMENTATION ──► done
```

| Label | Stage |
| ----- | ----- |
| `NEED_MORE_INFO` | Triage could not route the issue without guessing. The owner answers, and the issue is re-triaged. |
| `READY` | Triaged, scoped and routable. |
| `LOGIC` | Game logic and state. No rendering. |
| `ENGINE` | On-screen work — HUD, quest log, anything drawn. Raised as its own issue rather than folded into logic. |
| `LOCALISATION` | User-facing text and its translations. |
| `QA` | Review and merge of the pull request. |
| `DOCUMENTATION` | Documenting what shipped, after QA has merged. |

**Adding a label triggers a run; removing one does not.** The automation subscribes to
`issues.labeled`, not `issues.unlabeled`. To re-trigger a stage, remove the stale label first,
then add the label you want to fire.

## Rules that keep the stages honest

- **Logic first, screen second.** An issue that produces user-facing state does not also draw it.
  Displaying something is always a separate `ENGINE` issue, so logic can land tested and
  headless.
- **Text is not a literal.** Anything the player reads goes through localisation. Identifiers —
  quest ids, element names, asset keys — are not text and are never translated.
- **Out-of-scope work becomes an issue, not a bigger pull request.** Ship movement and physics
  came out of Quest 1 this way, as #3.

## The documenter's remit

Documentation is not a transcription of the diff. The stage exists to:

1. **Keep the context consistent.** The README, the architecture notes, the design canon and the
   changelog have to still be true after a change lands. Descriptions that have drifted from the
   code are defects — including pull request bodies and handoff comments, which are the record of
   what shipped.
2. **Document the work that was sent.** What shipped, in terms of behaviour and public API, not a
   restatement of the commits.
3. **Check the work against the goals and the game design.** Read `docs/DESIGN.md` and say plainly
   where the shipped work serves a pillar, where it is silent on one, and where it pulls against
   one. Drift is cheapest to name at the moment it appears.

Point 3 is a report, not a veto. Documentation does not block a merge; it records what a later
decision will need to know.

## Testing

The repository is test-driven. Tests are xUnit, under `tests/<project>.Tests`, targeting
`net10.0`.

**Agent containers have no .NET SDK, and the network policy blocks downloading one**
(`builds.dotnet.microsoft.com`, `dotnetcli.azureedge.net` and `aka.ms` return 403 through the
proxy; NuGet itself is reachable). Work is therefore reviewed by hand and unverified locally — a
green CI run is the first real confirmation that anything compiles. Say so explicitly at handoff
rather than implying a build passed.

Tests that assert on user-facing text must pin the culture. A test comparing against an English
literal with no culture pinned fails on any machine not running in English; this has already
broken the suite once, reproduced under `LANG=fr_FR.UTF-8`.
