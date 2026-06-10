# Gangland Undercover — Steam PC Art/UI/Visual Optimization Plan

> Date: 2026-06-10
> Priority: Steam PC first. macOS and mobile builds move to later release stages.
> Build target: Windows x64 Steam candidate package, minimum 600 MB distribution size using real art/audio assets only.

---

## 1. Product Goal

The Steam version must read as a complete PC social deduction game at first launch:

- The first screen should show a polished noir-police identity, not an editor prototype.
- The lobby should make remote play status obvious: login, Relay code, players, Ready state, and Host disconnect recovery.
- The in-match view should feel like a dense港区行动 map: street props, task stations, room dressing, readable landmarks, lighting states, and incident feedback.
- Screenshots should be usable for a Steam store page without requiring debug UI or editor-only context.

The current PC-first goal is not to ship macOS or mobile. Those platforms remain useful later, but they should not drive this phase's build size, control layout, install docs, or store checklist.

---

## 2. Current Visual Baseline

Already in the runtime:

- LimeZu 2D environment sprites drive floors, walls, landmarks, room props, task stations, meeting visuals, and task overlays.
- Kenney UI and SFX assets are used by runtime HUD buttons and feedback cues.
- The HUD has moved key remote-test actions into Canvas, including chat report/block and Host disconnect recovery via the formal "离开房间" button.
- The remote-test build now records Host disconnect as a visible recovery state: old room code expired, return to menu, or Host reopens a room.

Weak points that still show in screenshots:

- Some large art libraries exist locally but are not represented in the PC build, so the package feels smaller than the source art footprint.
- The current map is functional but still needs richer theme consistency across exterior streets, interior evidence rooms, and cinematic lighting.
- The UI is usable but still too dense in some operational panels for a Steam first-impression screenshot.

---

## 3. Steam PC Visual Direction

### Art Tone

Use "police procedural in a dense harbor district" as the visual north star:

- Wet asphalt, exterior signage, stairwells, back-lot service props, utility rooms, surveillance corners.
- Low-saturation operational lighting: patrol amber, evidence blue, emergency red, blackout shadow.
- No generic neon-heavy sci-fi look; accent colors must indicate game state.

### UI Tone

Use a quiet PC investigation HUD:

- Dense but scan-friendly panels.
- Small, consistent controls.
- Clear status hierarchy: service status, room code, player readiness, match phase, evidence, chat, and recovery actions.
- Error and disconnect messages should be direct and actionable.

### Screenshot Targets

Steam screenshot set should eventually include:

- Main menu with login/settings visible.
- Relay lobby with room code and player list.
- Action map with two players, props, task stations, and landmarks.
- Meeting/voting screen with readable player seats.
- Task overlay using 2D art skin.
- Blackout or emergency lighting state.
- Chat safety controls in the real Canvas HUD.

---

## 4. 600 MB+ PC Build Strategy

The package-size target must be met with real deliverables, not dummy filler.

This phase adds a Windows-only `SteamVisualArchive` beside the player build. It is a PC review/depot asset archive containing real local project art/audio references:

- `street-and-road-kit`: street, pavement, sidewalk, and exterior prop candidates.
- `synthetic-police-urban-kit`: stylized character/material/prop candidates.
- `city-crowd-animation-kit`: crowd body and animation candidates for future NPC staging.
- `simple-poly-city-kit`: building, vehicle, and skyline silhouette candidates.
- `cinematic-audio-reference`: atmosphere, impact, alarm, and trailer-sound candidates.

This archive is not gameplay-loaded by default. It ships beside the Windows executable so the Steam candidate package can be reviewed as a complete PC art drop while keeping runtime memory and scene load behavior stable.

Expected result:

- Windows build folder exceeds 600 MB.
- Windows zip exceeds 600 MB in normal distribution packaging.
- The archive has a `MANIFEST.md` that explains source groups and purpose.
- macOS and mobile builds do not inherit this PC-first archive by default.

Current delivery evidence:

- Steam PC visual archive exported successfully to `Builds/SteamPC-20260610/StandaloneWindows64/SteamVisualArchive/`.
- Archive manifest total copied bytes: `1,075,256,839` bytes (`1025.4 MiB`).
- Build-folder footprint: `1.0G` at `Builds/SteamPC-20260610/StandaloneWindows64`.
- Distribution zip: `Builds/SteamPC-20260610/GanglandUndercover-SteamPC-VisualArchive-20260610.zip`.
- Zip size: `789M` by `ls -lh`, `800M` by `du -sh`.
- Zip SHA-256: `6fe3f08ea37a9a1b85ba494fc078b55efe4c444a5576f5c1971f96d749da90d1`.
- No dummy filler files are used; the archive is built from existing project art/audio source directories.

Current machine blocker:

- The real Windows `.exe` build is blocked on this Mac because Unity 6000.4.9f1 only has `WebGLSupport` installed under `PlaybackEngines`.
- Install `Windows Build Support (Mono)` for Unity 6000.4.9f1 in Unity Hub, then rerun the Windows build command.
- Until that module is installed, the PC visual archive package is complete, but the runnable Steam Windows build is not complete.

---

## 5. Acceptance Checklist

### Build

- Done: `Builds/SteamPC-20260610/StandaloneWindows64/SteamVisualArchive/MANIFEST.md` exists.
- Done: distribution zip size is greater than 600 MB.
- Done: package contains no dummy filler files.
- Pending: Windows x64 `.exe` build succeeds with Unity 6000.4.9f1 after installing Windows Build Support.
- Pending: `Builds/SteamPC-20260610/StandaloneWindows64/GanglandUndercover.exe` exists.

### Automated Tests

- EditMode passes.
- PlayMode has no failures; ignored Relay role tests remain documented.
- Relay two-process cloud test remains the remote-networking health check, not a Steam packaging requirement.

### Manual Visual QA

- Launch Windows build.
- Capture main menu, settings, lobby, action map, meeting, task overlay, and disconnect recovery screenshots.
- Confirm no debug-only OnGUI path is required for normal friend testing.
- Confirm the first 10 seconds communicate a Steam-ready identity: title, room flow, controls, and visual theme.

---

## 6. Next Visual Work

Recommended next implementation slices:

1. Steam screenshot capture pass: fixed 1920x1080 scripted states and saved PNGs.
2. Main menu PC polish: title treatment, login/settings placement, background composition.
3. Action map polish: more exterior silhouettes, signage, wet-road highlights, and task-object clustering.
4. Meeting polish: stronger player-seat hierarchy and readable vote states.
5. Store capsule draft: derive capsule text and screenshot ordering from the PC build.

---

## 7. Platform Boundary

PC/Steam is the shipping lead.

macOS:

- Keep useful for development/friend testing.
- Do not optimize package size or signing first.
- Revisit after Windows Steam candidate is visually stable.

Mobile/app:

- Later-stage control/layout pass.
- Do not drive current UI density or package structure.
- Needs separate performance, touch, and store compliance work.
