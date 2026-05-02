# Gangland Undercover

Unity prototype for a three-faction social deduction game set in a compact port district.

## Open In Unity

1. Open Unity Hub.
2. Add this folder as an existing project: `C:\Users\Admin\GanglandUndercover`.
3. In Unity, choose `Gangland > Create Prototype Scene`.
4. Press Play.

The prototype builds its UI and game state at runtime. If the menu script does not compile because your Unity version is older, create an empty scene manually, add an empty GameObject named `PrototypeBootstrap`, attach `Assets/_Project/Scripts/Gameplay/PrototypeBootstrap.cs`, then press Play.

Chinese setup notes are in `Assets/_Project/Docs/UnitySetup.zh-CN.md`.

## Current Loop

- Pick Gang, Police, or Undercover.
- Move with WASD.
- Press E to do tasks or call an emergency meeting.
- Press R to report a body.
- Press Q to kill if playing Gang.
- Vote during meetings with incomplete clues.
- Use sabotage and blackout to create suspicion.

## Visual Prototype

- Red districts are gang-controlled.
- Blue districts are police-controlled.
- Brown districts are contested.
- The selected district is highlighted and drives the available action target.
