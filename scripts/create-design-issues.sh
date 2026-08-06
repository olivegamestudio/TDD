#!/usr/bin/env bash
# Creates the first wave of issues from docs/design-capture.md.
# Run from the repo root in an authenticated terminal:  bash scripts/create-design-issues.sh
# Review before running. Re-running will create duplicates — this is a one-shot.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

# --- labels (id-only; safe to re-run) --------------------------------------
gh label create READY        --color 0e8a16 --description "Ready for the Dev agent to pick up" 2>/dev/null || true
gh label create NEEDS_INPUT  --color d93f0b --description "Blocked on a human decision"        2>/dev/null || true
gh label create decision     --color 5319e7 --description "A design decision to be made"       2>/dev/null || true

# ===========================================================================
# READY — decided, self-contained, one agent to verified+merged behind CI
# ===========================================================================

gh issue create --label READY --title "Add opacity/tint to Sprite and honour it in the renderer" --body \
'Give Sprite a colour/alpha so screens can fade and tint. Today Sprite carries no colour and
MonoGameRenderer always draws Color.White.

Acceptance criteria:
- Sprite carries a colour (RGBA, default opaque white) — a value, no behaviour change when unset.
- MonoGameRenderer passes it through to SpriteBatch.Draw instead of the hard-coded white.
- Covered by a RecordingRenderer test asserting the colour reaches the draw call.
- Full suite green.

From docs/design-capture.md §2/§4 (enables the Company-screen fade).'

gh issue create --label READY --title "Add a focus query (HasFocus) to IUIController" --body \
'Input routing needs to know whether the UI currently owns input. IUIController can set focus
(FocusOn/UnFocus) but exposes no way to ask if anything is focused.

Acceptance criteria:
- IUIController exposes whether an element is focused (e.g. bool HasFocus, or Element? Focused).
- UIController implements it against its current focus state.
- Unit tests: nothing focused -> false; after FocusOn -> true; after UnFocus/Disable-last -> false.
- Full suite green.

From docs/design-capture.md §7. Enables the UI-first input routing.'

gh issue create --label READY --title "CompanyScreen renders the Olive logo for its duration" --body \
'CompanyScreen is a timer with no rendering, so the splash is black. Draw the studio logo centred
for the ~2s the screen is shown. Static is acceptable; a fade in/out is optional and only if the
Sprite-alpha issue has landed.

Acceptance criteria:
- CompanyScreen implements IRenderable and draws the logo asset centred in the viewport.
- Logo asset added to Content + Content.mgcb (asset provided separately by the owner).
- A RecordingRenderer test asserts the logo is drawn while the screen is current.
- Full suite green.

NOTE: needs the logo asset before it can be completed. From docs/design-capture.md §4 (Company screen).'

# ===========================================================================
# NEEDS_INPUT — decisions to close before they can become work
# ===========================================================================

gh issue create --label NEEDS_INPUT --label decision --title "DECISION: camera rotation model" --body \
'Does the world rotate around the ship (ship always points up) or is camera orientation independent
of heading? Decides the camera/render math. Current Camera2D is position-follow only, no rotation.
From docs/design-capture.md §2.'

gh issue create --label NEEDS_INPUT --label decision --title "DECISION: world area + dungeon + streaming model" --body \
'What is an "area" for streaming — chunk grid or named hand-authored regions? Are dungeons separate
instances loaded on entry (overworld unloaded) or contiguous? Streaming granularity/trigger?
From docs/design-capture.md §3.'

gh issue create --label NEEDS_INPUT --label decision --title "DECISION: world editing tool + data format" --body \
'In-game editor vs external tool; the world data format (must be editable AND streamable); audience
(you only vs modders). World editing is first-class per the design. From docs/design-capture.md §3.'

gh issue create --label NEEDS_INPUT --label decision --title "DECISION: control-method lock and scope" --body \
'Is the control method chosen at the Start press locked for the session or switchable mid-game? Does
"control system" mean the device only, or the whole UI paradigm (touch vs focus)? Note existing
FirstActiveShipInput arbitration switches devices per frame. From docs/design-capture.md §4.'

gh issue create --label NEEDS_INPUT --label decision --title "DECISION: touch + focus UI coexistence" --body \
'How does one UI layout serve both focus-navigation (keyboard/gamepad) and direct-tap touch + on
-screen thumbsticks, with no redesign for mobile? Target sizes, stick placement. From §1.'

gh issue create --label NEEDS_INPUT --label decision --title "DECISION: item durability-zero behaviour + item model" --body \
'When an item durability bottoms out: stops working until repaired (WoW-style) or destroyed? Item
model detail (stackable/equippable/categories) and repair/replace (shops? save zones?). From §6.'

gh issue create --label NEEDS_INPUT --label decision --title "DECISION: player/character attributes" --body \
'Which player attributes exist (health, XP/level, credits, skills, reputation, ...)? Conventional
for now, but the save payload needs the list. From docs/design-capture.md §6/§8.'

gh issue create --label NEEDS_INPUT --label decision --title "DECISION: resurrect points vs save zones" --body \
'Are resurrect points the same as save zones or separate? On death: respawn where, and what beyond
item degradation (if anything) is lost? From docs/design-capture.md §9.'

gh issue create --label NEEDS_INPUT --label decision --title "DECISION: placement authority in world.Introduce" --body \
'Is the world the single authority on placement (save passed into Introduce), or does resume override
the spawn afterwards? Governs the ship lifecycle seam. From docs/design-capture.md §10.'

echo "Done. Review the created issues at: gh issue list"
