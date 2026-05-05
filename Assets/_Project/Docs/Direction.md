# Direction: Harbor Undercover

## Engine Decision

Continue in Unity for the full online production track.

Reason: the important risk is now online gameplay clarity, synchronized deduction, and production speed. Unity is already open locally, the current playable scene runs, and the project benefits from fast iteration on:

- move through a corrupt harbor district,
- gather evidence at task stations,
- hide or expose violence through body reports and meetings,
- manage cover and suspicion as the undercover role,
- let AI characters sabotage, report, and vote back,
- make the police, gang, and undercover roles feel different,
- grow the prototype into a mature 2.5D online Hong Kong police-and-gang deduction game.

Unreal can be reconsidered later if the project commits to a realistic third-person production with advanced animation, lighting, chase scenes, and cinematic presentation.

## Production Plan

The mature-version plan is tracked in:

`Assets/_Project/Docs/ProductionPlan.zh-CN.md`

That plan is now the main reference for turning the current demo into an online multiplayer game. The current runtime demo remains useful as an offline test bed, but new work should move toward rooms, networked players, synchronized tasks, hidden-role authority, meetings, voting, voice/text, and authored online maps.

## Reference Boundary

Social deduction games are a useful reference for hidden motives, task pressure, incomplete information, reports, and meetings, but this project should not become a clone. The differentiator is the Hong Kong police-and-gang fantasy: evidence chain, undercover pressure, informants, sabotaged power, corrupt routes, witness pressure, anti-surveillance, and meeting-based deduction.

## Fun Pillars

- Suspicion over combat: actions create evidence, alibis, and cover risk rather than direct firefights.
- Readable spaces: every room should tell the player why it matters to police or gang operations.
- Civilian cost: heavy-handed policing and gang intimidation both damage public trust.
- Undercover tension: passing evidence should always threaten cover.
- Route control: blackouts, blocked paths, checkpoints, and bribes should create visible pressure without copying another game's spaceship sabotage.

## Current Playable Online Loop

- Play opens an online-first harbor scene with Host / Client controls.
- Host starts a match through Netcode for GameObjects and Unity Transport.
- If fewer than five players are present, the Host auto-fills AI participants so a full local match is immediately playable.
- The server/Host assigns hidden roles and privately sends the local player role.
- Each match starts with an opening case briefing / role reveal phase before movement is enabled.
- The lobby exposes player name, room name, player-count, evidence target, AI fill, role reveal, and proximity voice rules.
- WASD moves the local player through rooms and lanes.
- Evidence tasks take multiple interactions to complete and are synchronized across 12 large-map task stations.
- Gang can sabotage stations, trigger blackouts, and knock down investigators.
- Professions now create distinct play: Inspector marks suspicion, Forensics advances evidence, Tech repairs blackout/sabotage, Undercover uploads risky intel, Enforcer controls violence pressure, Fixer contaminates evidence, and Driver repositions through back routes.
- Bodies start meetings when reported by players or AI.
- Meetings support discussion countdown, voting countdown, skip vote, ejection, role reveal, and return to action or result.
- Victory checks evaluate configurable evidence target, living gang members, living non-gang players, and public role reveal on result.
- The runtime scene now uses Unity built-in models, materials, labels, point lights, generated audio, and props for a readable large Hong Kong harbor city play space.
- The procedural map is intentionally replaceable: a real Hong Kong port 3D tile / OSM / Cesium base can later be placed under the same gameplay zones.
- The HUD now shows room rules, player/AI state, professions, suspicion, cooldowns, task progress, bodies, vote counts, blackout timer, release coverage, and synced case log.
- Results support restart in the same room or returning to lobby.

## Next Production Additions

- Promote the Host / Join / Ready / Start flow from local IP testing to Lobby / Relay sessions.
- Add Authentication and player names.
- Add Vivox voice/text with living, dead, meeting, and gang channels.
- Harden Host authority and validation for movement, task state, body reports, meetings, votes, blackouts, evidence, and role data.
- Keep AI fill as a development and low-player fallback, but tune it separately from real 6-10 player online rules.
- Replace primitive shapes with authored 2.5D harbor art and editable prefabs after the online loop works.
- Add online-safe police-and-gang task minigames for evidence collection, sabotage, surveillance, and repair.
- Add meeting UI for voice/text discussion, evidence board, routes, and vote results.

## Avoid For Now

- Full realistic city.
- Guns as the main mechanic.
- Large open world.
- Complex inventory.
- Treating AI fill as a replacement for real multiplayer testing.
