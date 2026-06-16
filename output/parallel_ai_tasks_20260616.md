# Gangland Undercover - Parallel AI Handoff

> Date: 2026-06-16
> Owner split: Codex works on online match stability, tests, and build closure. The other AI works on low-conflict content, UI copy, asset audit, and QA evidence.

## Coordination Rules

- Do not edit `Assets/_Project/Scripts/Online/OnlineMatchController*.cs`, `OnlineMatchTypes.cs`, `TaskSync.cs`, `MeetingSync.cs`, `SabotageSync.cs`, `PlayerStateSync.cs`, or `OnlineSyncManager.cs` unless a single bug reproduction is assigned first.
- Do not move or delete Unity `.meta` files, `Assets/_Project/Resources`, `Library`, `Logs`, or build outputs.
- Prefer new dated docs under `output/` instead of overwriting older reports.
- If code changes are unavoidable, keep them outside the online core and include a test or exact manual verification step.
- End every task with `git diff --check` and a file list.

## Track A - Map And Gameplay Readability

**Goal:** Make the current police/gang map easier to read without changing online runtime code.

**Inputs:**
- `Assets/_Project/Scripts/Online/Map/*`
- `Assets/_Project/Scenes/Stage1VerticalSlice.unity`
- `Assets/_Project/Prefabs/Stage1VerticalSliceWorld.prefab`
- `output/screenshot_plan_20260609.md`
- `output/steam_screenshot_checklist_20260610.md`

**Output:** `output/map_readability_handoff_20260616.md`

**Acceptance criteria:**
- List 8-12 key map locations with purpose, gameplay risk, and screenshot value.
- Identify confusing routes, occluders, or task clusters from existing docs/assets.
- Provide a task-point table with stable names, suggested room labels, and priority.
- No scene, prefab, or runtime code changes.

## Track B - UI Copy And Terminology

**Goal:** Make lobby, HUD, meeting, sabotage, and result text consistent with the police/gang theme.

**Inputs:**
- `output/ui_terminology_guide_20260609.md`
- `output/ui_text_audit_20260609.md`
- `Assets/_Project/Scripts/UI/*`
- `Assets/_Project/Scripts/Online/OnlineMatchHud.cs`
- `Assets/_Project/Scripts/Core/Localization.cs`

**Output:** `output/ui_copy_handoff_20260616.md`

**Acceptance criteria:**
- Provide a before/after table for lobby, ready state, meeting, vote, sabotage, evidence, task, result, and disconnect text.
- Mark each item as `safe copy-only`, `needs code owner`, or `needs design decision`.
- Keep Chinese terms consistent: police, gang, undercover, mole, evidence, sabotage, meeting, vote.
- Do not edit `OnlineMatchHud.cs`; provide patch-ready recommendations only.

## Track C - Asset And Audio Audit

**Goal:** Identify production-ready assets already in the repo and the gaps blocking screenshots/playtests.

**Inputs:**
- `Assets/_Project/Legacy3D`
- `Assets/_Project/Audio`
- `Assets/_Project/Audio/SFX_BINDING_MANIFEST.md`
- `output/asset_license_credits_20260610.md`
- `output/art_audio_runtime_audit_20260606.md`

**Output:** `output/asset_audio_handoff_20260616.md`

**Acceptance criteria:**
- List reusable character, police, street, vehicle, interior, UI, and SFX assets.
- Flag license/credit uncertainty separately from technical readiness.
- Recommend 10 highest-impact asset swaps or screenshot props.
- Do not import, delete, or relocate assets.

## Track D - QA And External Playtest Evidence

**Goal:** Prepare a friend-test packet that records useful failures without changing the game.

**Inputs:**
- `output/friend_remote_test_runbook_20260610.md`
- `output/remote_test_closure_20260610.md`
- `output/qa_runbook_20260609.md`
- `output/KNOWN_ISSUES.md`

**Output:** `output/friend_test_packet_20260616.md`

**Acceptance criteria:**
- Include Host steps, Client steps, screenshot checkpoints, and failure classification.
- Include a table for issue id, build, map, player count, role, phase, observed result, expected result, media link, severity, reproduction count.
- Highlight P0/P1 issues that should stop new feature work.
- Do not edit build scripts or generated logs.

## Track E - Store Page And Screenshot Planning

**Goal:** Convert current gameplay state into a realistic Steam page draft and screenshot shot list.

**Inputs:**
- `output/steam_store_copy_draft_20260610.md`
- `output/steam_screenshot_checklist_20260610.md`
- `output/overview_stage2_4_20260607.md`
- `Assets/_Project/Docs/GameDesign.md`

**Output:** `output/steam_page_handoff_20260616.md`

**Acceptance criteria:**
- Provide short description, long description, feature bullets, tags, and screenshot captions.
- Avoid claims not supported by current playable build.
- Separate current-build claims from roadmap claims.
- No code or asset changes.

## Recommended Prompt For The Other AI

```text
You are working in /Users/zhugehao/projects/GanglandUndercover.

Do not edit core online runtime files:
Assets/_Project/Scripts/Online/OnlineMatchController*.cs
Assets/_Project/Scripts/Online/OnlineMatchTypes.cs
Assets/_Project/Scripts/Online/TaskSync.cs
Assets/_Project/Scripts/Online/MeetingSync.cs
Assets/_Project/Scripts/Online/SabotageSync.cs
Assets/_Project/Scripts/Online/PlayerStateSync.cs
Assets/_Project/Scripts/Online/OnlineSyncManager.cs

Pick one track from output/parallel_ai_tasks_20260616.md.
Create only the requested dated output document.
Do not delete or move assets, .meta files, logs, builds, Library, or Resources.
Finish by running git diff --check and report the files changed.
```

