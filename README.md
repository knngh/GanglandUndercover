# Gangland Undercover

Unity project for **港区潜线 / Harbor Undercover**, an original online 2.5D Hong Kong police, gang, and undercover social deduction game set in a corrupt port district.

The current playable build is now online-first and structured as a large-map release-candidate skeleton. It includes a Host / Client flow through Netcode for GameObjects and Unity Transport, synchronized players and tasks, opening role briefing, hidden role assignment, police evidence chains, gang sabotage and knockdowns, body reports, emergency meetings, voting, results, restart flow, room rules, player names, professions, suspicion pressure, generated runtime audio cues, and a case log. A Host can also auto-fill AI players, so one local Unity instance can play a full police-and-gang deduction match while real clients are still supported.

当前权威开发计划：

`output/master_development_plan_20260605.md`

（`Assets/_Project/Docs/` 中为历史开发文档，以 `output/` 目录中的最新计划为准）

## Open In Unity

1. Open Unity Hub.
2. Add this folder as an existing project: `/Users/zhugehao/projects/GanglandUndercover`.
3. Open it with Unity `6000.4.9f1`.
4. For the fastest playable check, choose `Gangland > Play Online Demo`.
5. Watch the `Game` view, not the edit-time `Scene` view.

`Assets/_Project/Scenes/Prototype.unity` is intentionally a runtime bootstrap scene. In edit mode it only contains `PrototypeBootstrap`; the map, players, HUD, task stations, props, and camera are created after entering Play mode. If you only open the scene in the editor, it will look empty.

The prototype builds the large online harbor city map, task stations, characters, props, neon lights, generated sound cues, AI fill, opening briefing, meeting flow, and HUD at runtime. If the menu script does not compile because your Unity version is older, create an empty scene manually, add an empty GameObject named `PrototypeBootstrap`, attach `Assets/_Project/Scripts/Gameplay/PrototypeBootstrap.cs`, then press Play.

An offline practice mode is available as a single-player sandbox with AI bots for quick prototyping and learning basic controls. This offline mode is **not** part of the 2D production track — it stays on the legacy 3D render path and is intended as a testing/tutorial sandbox only.

Chinese setup notes are in `Assets/_Project/Docs/UnitySetup.zh-CN.md`.

## Current Playable Loop

- Create Host, then start an online match. If fewer than five people are connected, the match auto-fills AI players.
- Join Client connects to a Host IP on port `7777`.
- The room panel supports player name, room name, minimum and maximum player counts, evidence target, AI fill, role-reveal-on-eject, and online text chat with three channels (meeting, global, proximity) plus a ghost-only channel for dead players. Proximity voice (Vivox) has been removed — text chat replaces it.
- Each match opens with a dedicated briefing / role reveal phase before action begins.
- Police and Undercover win by completing evidence tasks or voting out all gang players.
- Gang wins by knocking down enough investigators, sabotaging evidence, or reaching parity with non-gang survivors.
- Professions include Inspector, Forensics, Tech, Undercover Agent, Enforcer, Fixer, and Driver. `F` triggers each profession's match ability.
- The runtime map uses Unity built-in models and lights for a large Hong Kong harbor city district: container yard, customs gate, night market, cha chaan teng, command van, evidence room, clinic, power room, rooftop route, CCTV room, finance alley, back-lane food stalls, police van, roadblocks, neon signage, and emergency bell.
- The map is data-driven and replaceable: a real Hong Kong port 2D pixel-art base can later be plugged under the same gameplay zones.
- Runtime audio cues are generated in Unity for match start, task completion, ability, blackout, knockdown, meeting, vote, and result.
- AI players move around the harbor, work tasks, sabotage evidence, report bodies, trigger meetings, and vote.
- Meetings support manual voting, skip vote, synchronized vote counts, ejection, and role reveal on result.
- The HUD shows room rules, professions, AI tags, suspicion, cooldowns, task progress, bodies, vote progress, blackout state, release-readiness coverage, and case log.

## Controls

- `WASD`: move.
- `E`: work on a nearby evidence task or sabotage as Gang.
- `R`: report a nearby body or press the emergency bell at the center.
- `Q`: knock down a nearby target when playing Gang.
- `F`: use profession ability.
- HUD buttons create Host, join Client, ready, start the online match, auto-fill AI, vote, skip vote, restart same room, return to lobby, and leave room.

## Engine Direction

Continue in Unity for the full production track. The priority is building a distinctive online police-and-gang deduction game with fast iteration, Netcode/Lobby/Relay/online text chat, and a stable multiplayer vertical slice before considering a higher-cost engine switch. Unreal can be revisited later only if the project moves toward realistic third-person production, cinematic animation, and large authored environments.
