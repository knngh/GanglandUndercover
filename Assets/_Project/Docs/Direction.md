# Direction: Social Deduction With Action

## Engine Decision

Continue in Unity for the current milestone.

Reason: the immediate risk is not graphics quality. The immediate risk is whether the core loop is fun:

- move through a compact map,
- complete or fake tasks,
- kill or report bodies,
- trigger meetings,
- vote with incomplete information,
- use sabotage to create suspicious routes.

Unreal can be reconsidered later if the project commits to a realistic third-person production with advanced animation, lighting, chase scenes, and cinematic presentation.

## Fun Pillars

- Suspicion over combat: action should create evidence and lies, not replace deduction.
- Incomplete information: clues point toward suspects but should not be absolute proof.
- Risky tasks: doing tasks reveals where players were and creates alibis.
- Sabotage pressure: blackout, doors, alarms, and fake tasks force players to split up.
- Role tension: gang wants chaos, police wants proof, undercover wants to survive while shaping the vote.

## Current Prototype Loop

- Choose Police, Undercover, or Gang.
- Move with WASD.
- Press E to complete tasks, repair blackout, or use the emergency button.
- Press R to report a nearby body.
- Press Q to kill if playing Gang.
- Meetings let players vote, but roles are hidden.
- Blackout shrinks camera view and slows bots.
- Bodies generate imperfect clues for meetings.

## Next Good Additions

- Door locks and vents/shortcuts for gang.
- Camera room that shows recent movement, not live truth.
- Footprint trails that decay after a few seconds.
- Undercover-only ability: plant a soft clue or privately inspect one task station.
- Task minigames with short animations instead of instant completion.

## Avoid For Now

- Full realistic city.
- Guns as the main mechanic.
- Large open world.
- Complex inventory.
- Online multiplayer before the local loop is fun.
