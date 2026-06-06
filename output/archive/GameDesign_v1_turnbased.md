# Game Design: Gangland Undercover

## High Concept

A compact district-control and identity-pressure game. The player chooses Gang, Police, or Undercover. Each side uses the same map but has different information, risk, and victory logic.

## First Playable Scope

Map: Port District.

Districts:

- Dockyard: high crime value, high police pressure.
- Warehouse Row: gang logistics hub.
- Night Market: civilian-rich social node.
- Police Precinct: police base.
- Clinic: recovery and witness protection node.
- Tenement Block: rumor and informant node.

## Factions

Gang:

- Goal: control territory and complete a major shipment.
- Strength: fast influence gains.
- Risk: police heat and informants.

Police:

- Goal: assemble a complete evidence chain and arrest the boss.
- Strength: legal pressure and raids.
- Risk: low public trust from heavy-handed actions.

Undercover:

- Goal: preserve cover while passing evidence to police.
- Strength: access to gang actions and hidden intel.
- Risk: suspicion exposure.

## Core Resources

- Gang Influence: control over districts.
- Police Heat: immediate law-enforcement pressure.
- Evidence: case progress against gang leadership.
- Cover: undercover credibility inside the gang.
- Suspicion: risk that the undercover identity is exposed.
- Public Trust: limits police aggression and affects endgame quality.

## Turn Structure

1. Player chooses a district.
2. Player chooses an action available to their role.
3. Action mutates district and global resources.
4. AI simulates opposing factions.
5. Story events react to the current board state.
6. Victory evaluator checks end conditions.
7. If no winner, the day advances.

## Story Events

Events are one-time pressure beats that make the campaign feel less mechanical:

- Dockyard Witness: after early shipment movement, a witness appears and adds evidence.
- Public Backlash: excessive police heat damages public trust and cools pressure.
- Loyalty Test: high suspicion forces the undercover role to absorb cover damage.
- Boss Convoy: strong gang control accelerates the shipment through Warehouse Row.

## Initial Victory Conditions

Gang wins if:

- Gang controls 4 districts, or
- shipment progress reaches 3 while evidence is below 5, or
- undercover suspicion reaches 100.

Police wins if:

- evidence reaches 8 and police heat reaches 6, or
- gang control drops to 1 or fewer districts after day 4.

Undercover wins if:

- evidence reaches 8,
- cover remains at 35 or more,
- suspicion remains below 100.

## Next Milestone

Build a vertical slice:

- One generated scene.
- Clickable district/action UI.
- Clickable map nodes with controller colors.
- Role select.
- Turn log.
- Simple AI.
- Win/loss result screen.

After that, replace generated UI with designed screens and add path movement, NPC witnesses, and online lobby support.
