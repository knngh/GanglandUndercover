# Phase 2 -- Gameplay Balance, Pacing & Test Scenarios

**Project:** Gangland Undercover / 港区潜线  
**Date:** 2026-06-16  
**Scope:** 4-8 player online match pacing parameters, test scenarios, and stalemate detection  
**Sources:** All "current values" extracted from codebase as of this date

---

## Table of Contents

1. [Section 1: Numerical Parameter Table](#1-numerical-parameter-table)
2. [Section 2: Three In-Match Test Scenarios](#2-three-in-match-test-scenarios)
3. [Section 3: Stalemate / Failure Detection Rules](#3-stalemate--failure-detection-rules)

---

## 1. Numerical Parameter Table

> **Column legend:**
> - **Current Value** = literal default from code (file + line referenced)
> - **4P / 6P / 8P** = recommended values for that player count
> - A dash "---" means "use the same value as Current"

### 1.1 Kill System

| Parameter | Code Current Value | Source | 4P Suggestion | 6P Suggestion | 8P Suggestion | Design Rationale |
|-----------|-------------------|--------|---------------|---------------|---------------|-----------------|
| Kill Range (world units) | `0.9f` | `OnlineRuleSet.cs:78` | 0.9 | 1.0 | 1.1 | Larger lobbies need slightly wider kill zones to compensate for crowded corridors; 4P map is quieter so tighter range forces stalking |
| Kill Cooldown (seconds) | `25f` | `OnlineRuleSet.cs:82` | 30 | 25 | 22 | 4P has few targets so longer CD prevents snowball; 8P has more targets so shorter CD keeps pressure up |
| KillSystem local killRange (UI button display) | `1.5f` | `KillSystem.cs:20` | 1.5 | 1.5 | 1.5 | UI display range; keep aligned with server kill range + buffer |
| KillSystem local killCooldown (UI timer) | `18f` | `KillSystem.cs:23` | Sync to RuleSet | Sync to RuleSet | Sync to RuleSet | Should read from `ruleSet.KillCooldownSeconds` at runtime; currently a duplicate constant -- flag for code cleanup |
| Report Range (world units) | `1.25f` | `OnlineRuleSet.cs:80` | 1.25 | 1.35 | 1.5 | Larger maps mean bodies are further apart; wider report range prevents "missed body" frustration in 8P |
| Report Cooldown (seconds) | `5f` | `OnlineRuleSet.cs:70` | 5 | 5 | 6 | Slightly longer in 8P to prevent meeting spam from multiple corpse discoveries |
| Post-Meeting Kill Grace (seconds) | `3f` | `OnlineRuleSet.cs:62` | 3 | 3 | 4 | Longer grace in 8P since more players respawn at same meeting hub |
| First Kill Min Delay (seconds) | `8f` | `OnlineRuleSet.cs:66` | 12 | 10 | 8 | 4P needs longer safe period to spread out; 8P can keep the current 8s |
| Kill screen flash duration | `0.35f` | `KillSystem.cs:39` | 0.35 | 0.35 | 0.35 | Cosmetic; no balance impact |
| Kill screen flash peak alpha | `0.35f` | `KillSystem.cs:43` | 0.35 | 0.35 | 0.35 | Cosmetic |
| Kill suspicion increase | `+2` | `OnlineMatchController.cs:1565` | +2 | +2 | +2 | Fixed per-kill suspicion; balanced with meeting discussion |

### 1.2 Meeting System

| Parameter | Code Current Value | Source | 4P Suggestion | 6P Suggestion | 8P Suggestion | Design Rationale |
|-----------|-------------------|--------|---------------|---------------|---------------|-----------------|
| Meeting Intro / Discussion (seconds) | `35f` | `OnlineRuleSet.cs:86` | 30 | 35 | 45 | 4P has fewer suspects so shorter discussion; 8P needs more time for deduction |
| Voting Duration (seconds) | `40f` | `OnlineRuleSet.cs:88` | 30 | 40 | 50 | 4P few voters = fast resolution; 8P more voters + SecretVote needs more time |
| Emergency Meeting Cooldown (seconds) | `75f` | `OnlineRuleSet.cs:90` | 60 | 75 | 90 | Longer cooldown in 8P to prevent meeting chains that stall the action phase |
| Max Emergency Meetings (global cap) | `3` | `OnlineRuleSet.cs:92` | 2 | 3 | 4 | Scale with player count; `EmergencyMeetingLimitFor()` already does `Clamp(N/3, 1, Max)` |
| Effective meeting limit (computed) | `Clamp(N/3, 1, Max)` | `OnlineRuleSet.cs:256-258` | 1 | 2 | 2 | 4P=1, 6P=2, 8P=2 -- this is per-match shared among all players |
| Skip vote target sentinel | `ulong.MaxValue` | `OnlineMatchController.cs:42` | --- | --- | --- | Internal constant; not player-facing |
| Tie vote handling | No one ejected | `OnlineMatchController.cs:2000-2030` | --- | --- | --- | Correct: tie = safe round, encourages discussion |
| Reveal role on eject | `true` | `OnlineRuleSet.cs:118` | true | true | true | Default on; good for learnability in all lobby sizes |
| Role reveal display duration | `6.5f` seconds | `OnlineMatchController.cs:47` | 6.5 | 6.5 | 6.5 | Fixed; sufficient for reading |
| Meeting count tracking | `_meetingCount` incremented in `BeginMeeting()` | `OnlineMatchController.cs:1879` | --- | --- | --- | Already tracked; exposed for stats |

### 1.3 Task System

| Parameter | Code Current Value | Source | 4P Suggestion | 6P Suggestion | 8P Suggestion | Design Rationale |
|-----------|-------------------|--------|---------------|---------------|---------------|-----------------|
| Tasks Per Non-Gang Player | `4` (range 2-8) | `OnlineRuleSet.cs:54` | 5 | 4 | 3 | 4P has fewer task-doers so more per person; 8P has many contributors so fewer each prevents too-fast completion |
| Total tasks (computed) | `(N - gangCount) * tasksPerPlayer` | `OnlineRuleSet.cs:261-265` | ~15 | ~16 | ~18 | 4P: 3 non-gang x 5 = 15; 6P: 4 x 4 = 16; 8P: 6 x 3 = 18 |
| Evidence Per Task (base) | `3` (range 1-6) | `OnlineRuleSet.cs:58` | 3 | 3 | 3 | Base value; actual gain modified by `EvidenceGainFor()` |
| Default Evidence Target | `44` | `OnlineRuleSet.cs:108` | 34 | 44 | 50 | 4P lower target (fewer task-doers); 8P higher (more contributors, needs longer game) |
| Min Evidence Target (slider) | `34` | `OnlineRuleSet.cs:110` | 28 | 34 | 40 | Floor should scale with player count |
| Max Evidence Target (slider) | `56` | `OnlineRuleSet.cs:112` | 44 | 56 | 64 | Cap should scale too |
| Scaled Evidence Target (computed) | `Clamp(Round(44 * Clamp(N/8, 0.6, 1.3)), 34, 56)` | `OnlineRuleSet.cs:268-275` | 26 (clamped to 34) | 33 (clamped to 34) | 44 | Formula: 4P=26->34, 6P=33->34, 8P=44. 4P/6P both hit the floor -- suggest lowering MinEvidenceTarget |
| Task evidence value (per taskId) | 1 / 2 / 3 depending on task | `OnlineMatchController.cs:2812-2834` | --- | --- | --- | Task IDs 0,3,11,15,16,21,22,26 give 2; IDs 4,8,18,24,27 give 3; rest give 1 |
| Forensics bonus | `+1` per task | `OnlineMatchController.cs:2795-2798` | +1 | +1 | +1 | Good differentiation for the Forensics profession |
| Undercover/Agent bonus | `+1` per task | `OnlineMatchController.cs:2804-2807` | +1 | +1 | +1 | Rewards double-agent play |
| Tech EvidenceChainBonus | `1.3x` multiplier | `OnlineRuleSet.cs:197` | 1.3 | 1.3 | 1.3 | Core identity of Tech profession |
| Evidence gain clamp | `[1, 5]` | `OnlineMatchController.cs:2809` | [1,5] | [1,5] | [1,5] | Prevents degenerate single-task completions |
| Task charge rate (per taskId) | 0.56 - 0.76 /sec | `OnlineMatchController.Gameplay.cs:943-959` | --- | --- | --- | 5 difficulty tiers; average completion ~1.5-2.0s per step |
| Task step correct bonus | `+0.28` charge | `OnlineMatchController.Gameplay.cs:891` | 0.28 | 0.28 | 0.28 | 3 steps + charge = task done in ~4-6s for skilled players |
| Task step mistake penalty | `-0.18` charge | `OnlineMatchController.Gameplay.cs:912` | -0.18 | -0.18 | -0.18 | Forgiving enough for learning |
| Max mistakes before reset | `3` | `OnlineMatchController.Gameplay.cs:918` | 3 | 3 | 3 | 3 strikes = full reset; fair |
| Undercover task progress bonus | `+2` per interaction (vs `+1` normal) | `OnlineMatchController.cs:1481` | +2 | +2 | +2 | Undercover progresses faster to support double-agent gameplay |
| Bot task complete time | `2.5f` seconds | `OnlineBotController.cs:29` | 3.5 | 2.5 | 2.0 | 4P bots should be slower (fewer bots, each does more); 8P bots faster to keep pace |
| Bot repair time | `2.0f` seconds | `OnlineBotController.cs:30` | 2.0 | 2.0 | 2.0 | Adequate |

### 1.4 Sabotage / Disruption System

| Parameter | Code Current Value | Source | 4P Suggestion | 6P Suggestion | 8P Suggestion | Design Rationale |
|-----------|-------------------|--------|---------------|---------------|---------------|-----------------|
| Blackout Duration (seconds) | `28f` | `OnlineRuleSet.cs:124` | 20 | 28 | 35 | 4P short duration (fewer people to repair); 8P longer for more chaos |
| Lockdown Duration (seconds) | `32f` | `OnlineRuleSet.cs:126` | 24 | 32 | 40 | Same scaling logic |
| Communication Jam Duration (seconds) | `30f` | `OnlineRuleSet.cs:128` | 22 | 30 | 38 | Comm jam blocks emergency meetings -- very powerful in 8P |
| Evidence Leak Duration (seconds) | `36f` | `OnlineRuleSet.cs:130` | 25 | 36 | 45 | Evidence leak ticks -1/sec (per OnlineMatchController); longer in 8P = more damage potential |
| Patrol Alert Duration (seconds) | `30f` | `OnlineRuleSet.cs:132` | 22 | 30 | 38 | Cosmetic + suspicion pressure |
| Blackout vision multiplier | `0.4f` (60% reduction) | `OnlineMatchController.Gameplay.cs:1253` | 0.5 | 0.4 | 0.35 | Stronger blindness in larger games for more tension |
| Blackout interaction multiplier | `0.5f` (50% reduction) | `OnlineMatchController.Gameplay.cs:1256` | 0.6 | 0.5 | 0.4 | Tighter interaction in 8P blackout = harder to repair |
| Lockdown rooms locked | `3` rooms | `OnlineMatchController.Gameplay.cs:1146` | 2 | 3 | 4 | More rooms in 8P maps to lock |
| Lockdown move speed multiplier | `0.72f` | `OnlineMatchController.cs:873` | 0.72 | 0.72 | 0.72 | Fixed penalty; adequate |
| Patrol Alert gang speed multiplier | `0.9f` | `OnlineMatchController.cs:873` | 0.9 | 0.9 | 0.9 | Slight slowdown for gang during patrol |
| Comm jam emergency cooldown override | `max(current, 30f)` | `OnlineMatchController.Gameplay.cs:1153` | 30 | 30 | 30 | Blocks meetings for at least 30s |
| Sabotage evidence penalty -- EvidenceLeak | `-2` | `OnlineMatchController.Gameplay.cs:1313` | -2 | -2 | -3 | More punishing in 8P to make sabotage impactful |
| Sabotage evidence penalty -- Blackout/Lockdown/Comm | `-1` | `OnlineMatchController.Gameplay.cs:1314-1316` | -1 | -1 | -1 | Moderate |
| Sabotage evidence penalty -- None/other | `0` | `OnlineMatchController.Gameplay.cs:1318` | 0 | 0 | 0 | --- |
| SabotageSync hint display duration | `4f` seconds | `SabotageSync.cs:27` | 4 | 4 | 5 | Slightly longer in 8P for visibility |
| SabotageSync refresh interval | `0.5f` seconds | `SabotageSync.cs:29` | 0.5 | 0.5 | 0.5 | Adequate polling rate |

### 1.5 Character Movement

| Parameter | Code Current Value | Source | 4P Suggestion | 6P Suggestion | 8P Suggestion | Design Rationale |
|-----------|-------------------|--------|---------------|---------------|---------------|-----------------|
| Base Move Speed | `4.5f` (units/sec) | `OnlineMatchController.cs:44` | 4.5 | 4.5 | 4.5 | Constant across all modes; map size scales instead |
| Player Collision Radius | `0.22f` | `OnlineMatchController.cs:45` | 0.22 | 0.22 | 0.22 | Prevents overlap stacking |
| Collision Trace Step | `0.08f` | `OnlineMatchController.cs:46` | 0.08 | 0.08 | 0.08 | Physics resolution granularity |
| Driver Move Speed Bonus | `1.08x` | `OnlineRuleSet.cs:237` | 1.08 | 1.08 | 1.10 | Slightly more in 8P where map traversal takes longer |
| Ghost Move Speed Multiplier | `1.2x` | `GhostMode.cs:33` | 1.2 | 1.2 | 1.2 | Ghosts move 20% faster; good for spectating + helping teammates |
| Ghost Transparency (alpha) | `0.35f` | `GhostMode.cs:26` | 0.35 | 0.35 | 0.35 | Visible to dead players, invisible to alive |
| Ghost Z-offset | `0.6f` | `GhostMode.cs:37` | 0.6 | 0.6 | 0.6 | Visual floating effect |
| Vent / Underworld Transit Range | `1.15f` | `OnlineRuleSet.cs:154` | 1.15 | 1.15 | 1.15 | Fixed |
| Vent Cooldown (seconds) | `10f` | `OnlineRuleSet.cs:156` | 12 | 10 | 8 | Shorter in 8P for more dynamic play; longer in 4P to prevent abuse |
| Underworld Passage Count (nodes) | `4` | `OnlineRuleSet.cs:158` | 3 | 4 | 5 | More nodes in larger maps |
| General Interaction Range | `1.08f` | `OnlineRuleSet.cs:152` | 1.08 | 1.08 | 1.10 | Slightly wider in 8P for crowded scenes |

### 1.6 Role Distribution

| Parameter | Code Current Value | Source | 4P Suggestion | 6P Suggestion | 8P Suggestion | Design Rationale |
|-----------|-------------------|--------|---------------|---------------|---------------|-----------------|
| Role Distribution Table entry (5P) | 1 gang, 1 undercover, 0 mole, 3 police | `OnlineRuleSet.cs:21` | --- | --- | --- | Fallback for 4P |
| Role Distribution Table entry (6P) | 1 gang, 1 undercover, 1 mole, 3 police | `OnlineRuleSet.cs:22` | --- | --- | --- | Used for 6P |
| Role Distribution Table entry (7P) | 2 gang, 1 undercover, 0 mole, 4 police | `OnlineRuleSet.cs:23` | --- | --- | --- | --- |
| Role Distribution Table entry (8P) | 2 gang, 1 undercover, 1 mole, 4 police | `OnlineRuleSet.cs:24` | --- | --- | --- | Used for 8P |
| **4P actual distribution** | Falls back to 5P entry: **Gang=1, Undercover=1, Mole=0, Police=2** | `GetRoleDistribution()` line 32-45 | **Gang=1, UC=0, Mole=0, Police=3** | --- | --- | 4P without undercover/mole is simpler and more focused. 1 killer vs 3 task-doers is classic social deduction ratio |
| **5P effective** | Gang=1, UC=1, Mole=0, Police=3 | (from table) | --- | --- | --- | OK as-is |
| **6P effective** | Gang=1, UC=1, Mole=1, Police=3 | (from table) | --- | --- | --- | Good mix; Mole adds paranoia |
| **7P effective** | Gang=2, UC=1, Mole=0, Police=4 | (from table) | --- | --- | --- | 2 killers is threatening |
| **8P effective** | Gang=2, UC=1, Mole=1, Police=4 | (from table) | --- | --- | --- | Full 4-faction game |
| Minimum Room Players | `4` | `OnlineRuleSet.cs:96` | 4 | 4 | 4 | Keep |
| Maximum Room Players | `10` | `OnlineRuleSet.cs:98` | 4 | 6 | 8 | Server-enforced per lobby |
| Minimum Playable Players (to start) | `5` | `OnlineRuleSet.cs:104` | 4 | 5 | 5 | **Must lower to 4** if 4P games are desired; currently 4P cannot start without bots |
| Auto-fill AI bots | `true` | `OnlineRuleSet.cs:116` | true | true | true | Default on; essential for small lobbies |

### 1.7 Victory Conditions

| Parameter | Code Current Value | Source | 4P Suggestion | 6P Suggestion | 8P Suggestion | Design Rationale |
|-----------|-------------------|--------|---------------|---------------|---------------|-----------------|
| **Police Victory -- Evidence** | `EvidenceScore >= EvidenceTarget` | `OnlineMatchController.cs:2081` | 34 | 34 | 44 | Tied to ScaledEvidenceTarget |
| **Police Victory -- Gang Wipe** | All Gang-role players dead (aliveGang==0 && N>=2) | `OnlineMatchController.cs:2107-2110` | --- | --- | --- | Correct |
| **Gang Victory -- Control** | aliveGang>0 && (aliveNonGang==0 OR (N>=4 && aliveGang>=aliveNonGang)) | `OnlineMatchController.cs:2111` | aliveGang >= aliveNonGang | Same | Same | Parity rule: if gang equals or outnumbers non-gang at 4+ players, gang wins |
| **Gang Victory -- Time Limit** | Evidence < 82% target AND completed tasks < 72% when time runs out | `OnlineMatchController.cs:2171` | 82% | 82% | 82% | Threshold is reasonable |
| **Police Victory -- Time Limit (partial)** | Evidence >= 82% target OR completed tasks >= 72% when time expires | `OnlineMatchController.cs:2171` | 75% | 82% | 85% | Lower threshold in 4P since fewer task-doers; higher in 8P |
| Match Target Min (seconds) | `600f` (10 min) | `OnlineRuleSet.cs:140` | 480 (8min) | 600 (10min) | 720 (12min) | Scale with player count |
| Match Hard Limit (seconds) | `1200f` (20 min) | `OnlineRuleSet.cs:142` | 720 (12min) | 1080 (18min) | 1200 (20min) | 4P should end faster; 8P can use full 20 min |
| Undercover Betrayal threshold | Evidence >= 75% of target | `OnlineMatchController.cs:1621` | 75% | 75% | 75% | Good trigger point |
| Undercover Betrayal evidence bonus | `+3` | `OnlineMatchController.cs:1627` | +3 | +3 | +3 | Significant boost for dramatic reveal |
| Mole Betrayal (public switch to Gang) | Suspicion >= 60 | `OnlineMatchController.cs:1685` | 60 | 60 | 55 | Slightly lower in 8P for more dramatic plays |
| Mole Sabotage Intel evidence penalty | `-2` EvidenceScore | `OnlineMatchController.cs:1714` | -2 | -2 | -3 | More impactful in 8P |
| Mole Sabotage Intel cooldown | `20f` seconds | `OnlineMatchController.cs:1729` | 25 | 20 | 18 | Longer in 4P to limit spam |

### 1.8 AI / Bot Configuration

| Parameter | Code Current Value | Source | 4P Suggestion | 6P Suggestion | 8P Suggestion | Design Rationale |
|-----------|-------------------|--------|---------------|---------------|---------------|-----------------|
| AI Action Grace (seconds) | `22f` | `OnlineRuleSet.cs:146` | 18 | 22 | 25 | Longer grace in 8P to let humans spread out first |
| Preview AI Grace (seconds) | `55f` | `OnlineRuleSet.cs:148` | 55 | 55 | 55 | Local preview; keep generous |
| Bot Think Timer Min (seconds) | `1.2f` | `OnlineBotController.cs:26` | 1.5 | 1.2 | 1.0 | 4P bots should think slower (more human-like); 8P faster to keep pace |
| Bot Think Timer Max (seconds) | `3.4f` | `OnlineBotController.cs:27` | 4.0 | 3.4 | 2.8 | Same scaling |
| Bot Interact Distance | `0.45f` | `OnlineBotController.cs:28` | 0.45 | 0.45 | 0.45 | Fixed |
| Bot body report probability | `0.42` (42%) | `OnlineBotController.cs:254` | 0.55 | 0.42 | 0.35 | 4P bots should report more often (fewer players, bodies go unnoticed); 8P less to prevent meeting flood |
| Bot sabotage probability | `0.03` per tick | `OnlineBotController.cs:361` | 0.02 | 0.03 | 0.04 | More aggressive sabotage in larger games |
| Bot profession ability use probability | `0.10-0.12` | `OnlineBotController.cs:393,427` | 0.10 | 0.12 | 0.15 | 8P bots should use abilities more |
| Bot vote skip probability | `0.15` (15%) | `OnlineBotController.cs:784` | 0.10 | 0.15 | 0.20 | 8P more skip votes creates uncertainty |
| Bot stuck detection threshold | `0.03f` distance | `OnlineBotController.cs:40` | 0.03 | 0.03 | 0.03 | Adequate |
| Bot stuck reroute time | `5.0f` seconds | `OnlineBotController.cs:41` | 5.0 | 5.0 | 5.0 | Adequate |
| Bot Client ID base | `900000` | `OnlineBotController.cs:25` | --- | --- | --- | Internal constant |

### 1.9 Ability Cooldowns

| Parameter | Code Current Value | Source | 4P Suggestion | 6P Suggestion | 8P Suggestion | Design Rationale |
|-----------|-------------------|--------|---------------|---------------|---------------|-----------------|
| Global Ability Cooldown | `13f` seconds | `OnlineRuleSet.cs:136` | 15 | 13 | 11 | Shorter in 8P for more dynamic play |
| Enforcer KillCooldownReduce multiplier | `0.75x` | `OnlineRuleSet.cs:207` | 0.80 | 0.75 | 0.70 | More aggressive in 8P |
| Inspector ReportCooldownReduce multiplier | `0.80x` | `OnlineRuleSet.cs:178` | 0.85 | 0.80 | 0.75 | Stronger in 8P where report frequency matters |
| Driver VentSpeedBonus multiplier | `1.5x` | `OnlineRuleSet.cs:235` | 1.3 | 1.5 | 1.8 | More valuable in larger maps |
| Fixer SabotageCooldownReduce multiplier | `0.80x` | `OnlineRuleSet.cs:217` | 0.85 | 0.80 | 0.75 | More aggressive in 8P |
| Mole SabotageCooldownReduce multiplier | `0.90x` | `OnlineRuleSet.cs:245` | 0.95 | 0.90 | 0.85 | Mole needs subtlety; lower = more powerful |
| Forensics CorpseExamine bonus | `+2` clues | `OnlineRuleSet.cs:187` | +2 | +2 | +3 | More info in 8P games with more bodies |
| Tech EvidenceChainBonus multiplier | `1.3x` | `OnlineRuleSet.cs:197` | 1.2 | 1.3 | 1.4 | Scale importance with player count |
| Mole suspicion threshold for betrayal | `60` | `OnlineMatchController.cs:1685` | 65 | 60 | 55 | See 1.7 |
| Max Case Log Entries | `8` | `OnlineRuleSet.cs:162` | 6 | 8 | 10 | More log entries for more complex games |

---

## 2. Three In-Match Test Scenarios

### Scenario A: "Speed Kill Blitz" -- 4 Player Stress Test

**Objective:** Validate that 4-player matches have enough tension despite small player count, and that the kill cooldown + first-kill delay prevent opening-round snowballing.

#### Configuration

| Setting | Value |
|---------|-------|
| Player Count | 4 (1 human + 3 bots) |
| Map | HarbourDistrict (smaller configuration) |
| Role Distribution | Gang=1 (Enforcer), Undercover=0, Mole=0, Police=3 (Inspector, Forensics, Tech) |
| Evidence Target | 34 |
| Kill Cooldown | 30s |
| First Kill Delay | 12s |
| Match Hard Limit | 720s (12 min) |
| Emergency Meetings | Max 2 |

#### Timeline Script

| Time | Event | Expected State |
|------|-------|---------------|
| 0:00 | Match starts, Opening phase (6.5s role reveal) | All players at spawn, full map preview |
| 0:07 | Action phase begins | Players disperse |
| 0:07-0:12 | AI grace period active (18s) | Bots idle, human can explore freely |
| 0:12 | AI grace expires | Bots begin patrolling toward tasks |
| 0:12 | **First kill window opens** (FirstKillMinDelay=12s) | Gang bot can now kill |
| 0:30-1:00 | Police bots begin completing tasks | EvidenceScore starts climbing (expect +3-6) |
| 0:42 | Gang bot first kill opportunity (KillCD=30s from start) | First kill expected around 0:30-1:00 |
| 1:00-1:30 | Body discovered by police bot (42-55% chance) | Meeting triggered |
| 1:30 | Meeting discussion phase (30s) | Chat active, suspicion analysis |
| 2:00 | Voting phase (30s) | Bot voting with suspicion weights |
| 2:30 | Vote resolved, post-meeting grace (3s) | Kill cooldowns reset for grace period |
| 2:33 | Action resumes | Gang kill cooldown starts ticking (30s) |
| 3:00-4:00 | Second kill window | EvidenceScore should be ~12-18 |
| 4:00-5:00 | Evidence target 50% milestone | Inspector uses ReportCooldownReduce |
| 5:00-6:00 | Third kill or meeting | If Gang killed twice: aliveGang=1, aliveNonGang=1 --> Gang wins by parity |
| 6:00-8:00 | Endgame | Either Police hits 34 evidence or Gang achieves parity |

#### Expected Outcome

- **Primary path:** Police victory via evidence (60% probability) -- 3 task-doers vs 1 killer in a small map gives Police an edge.
- **Secondary path:** Gang victory via parity (25% probability) -- if Gang gets 2 kills and 1 police is ejected by vote.
- **Edge case:** Time limit Police victory (15%) -- if Evidence >= 82% of 34 = 28 at 12 minutes.

#### Key Validation Items

1. First kill does not happen before 12 seconds
2. Kill cooldown of 30s prevents rapid double-kill
3. Bot report probability of 55% ensures bodies are found promptly in 4P
4. Evidence target of 34 is achievable in 6-8 minutes with 3 task-doers
5. Parity rule (aliveGang >= aliveNonGang) works correctly at 4P threshold
6. Ghost mode activates correctly for killed players

---

### Scenario B: "Sabotage Cascade" -- 6 Player Mid-Game Chaos

**Objective:** Validate that sabotage systems create meaningful disruption without being overwhelming, and that the Mole role adds enough paranoia to the meeting phase.

#### Configuration

| Setting | Value |
|---------|-------|
| Player Count | 6 (2 human + 4 bots) |
| Map | PoliceStation (compact, high-tension) |
| Role Distribution | Gang=1 (Enforcer), Undercover=1 (UndercoverAgent), Mole=1 (Mole profession), Police=3 (Inspector, Tech, Forensics) |
| Evidence Target | 34 (scaled) |
| Kill Cooldown | 25s |
| First Kill Delay | 10s |
| Match Hard Limit | 1080s (18 min) |
| Emergency Meetings | Max 2 (6/3=2) |

#### Timeline Script

| Time | Event | Expected State |
|------|-------|---------------|
| 0:00 | Match starts | Role reveal: human 1 is Inspector, human 2 is Mole |
| 0:10 | Action phase begins | AI grace expires at 22s |
| 0:22 | AI bots start acting | Gang bot heads for nearest non-gang target |
| 0:35 | **Gang bot first kill** (~25s after grace) | First body drops |
| 0:40-1:00 | Mole bot uses SabotageIntel (EvidenceScore -2) | EvidenceScore setback; suspicion reduction on gang |
| 1:00 | Body reported (42% bot report rate) | Meeting #1 triggered |
| 1:00-1:35 | Meeting #1 discussion | Mole bot participates in chat, votes with Police |
| 1:35-2:15 | Voting #1 (40s) | Someone ejected (maybe innocent due to Mole cover) |
| 2:15 | Post-meeting action | Gang kill cooldown refreshed |
| 2:30 | **Sabotage cascade begins:** | Mole bot sabotages a task (Blackout or Comm Jam) |
| 2:30-3:00 | Blackout active (28s) | Vision reduced to 40%, interaction halved |
| 2:45 | Gang bot kills during blackout | Second body -- hard to find in dark |
| 3:00 | Blackout repaired by Tech bot | Vision restored |
| 3:15 | Second body found | Meeting #2 triggered |
| 3:15-3:50 | Meeting #2 -- heightened suspicion | UndercoverAgent may betray if evidence >= 75% |
| 3:50-4:30 | Voting #2 | Critical vote; if Undercover betrays, gang suspicion +2 |
| 4:30 | Post-meeting | Final phase |
| 5:00-7:00 | Endgame push | Evidence chain vs remaining gang kills |
| 7:00-10:00 | Late game | Either side can win depending on meeting outcomes |

#### Expected Outcome

- **Primary path:** Police victory via evidence (45% probability) -- 3 police + undercover make steady progress despite sabotage.
- **Secondary path:** Gang victory via kills + time (30% probability) -- 2 kills + Mole sabotage delays evidence enough.
- **Tertiary path:** Undercover betrayal (15% probability) -- dramatic mid-meeting reveal if evidence reaches 75%.
- **Edge case:** Mole survives to endgame with Suspicion < 60 (10%) -- Mole wins by not being detected.

#### Key Validation Items

1. Mole's SabotageIntel correctly reduces EvidenceScore by 2 and reduces gang suspicion by 1
2. Blackout reduces vision to 40% and interaction range to 50%
3. Kill during blackout is possible but body is harder to find
4. Meeting discussion with Mole present creates correct paranoia (Mole appears as Police)
5. UndercoverAgent betrayal triggers at 75% evidence and switches public role correctly
6. Emergency meeting cooldown of 75s prevents meeting chains
7. Communication jam blocks emergency meeting calls for 30s minimum

---

### Scenario C: "Full Faction War" -- 8 Player Balanced Endurance

**Objective:** Validate the complete 4-faction experience (Police, Gang, Undercover, Mole) in a full 8-player match. Test endgame conditions, time pressure, and all ability interactions.

#### Configuration

| Setting | Value |
|---------|-------|
| Player Count | 8 (3 human + 5 bots) |
| Map | HarbourDistrict (full size) |
| Role Distribution | Gang=2 (Enforcer, Fixer), Undercover=1 (Driver), Mole=1 (Mole), Police=4 (Inspector, Forensics, Tech, civilian) |
| Evidence Target | 44 |
| Kill Cooldown | 22s |
| First Kill Delay | 8s |
| Match Hard Limit | 1200s (20 min) |
| Emergency Meetings | Max 2 (8/3=2 clamped) |

#### Timeline Script

| Time | Event | Expected State |
|------|-------|---------------|
| 0:00 | Match starts, Opening | 8 players see roles; humans get Inspector, Enforcer, Driver |
| 0:08 | Action phase begins | AI grace 25s |
| 0:25 | AI bots activate | 2 Gang bots hunt; Fixer bot looks for sabotage targets |
| 0:30 | **First kill** (Enforcer bot, KillCD * 0.75 = ~16.5s) | Body #1 drops |
| 0:45 | Driver (human, Undercover) uses VentSpeedBonus to navigate | 1.5x vent speed, 1.10x move speed |
| 0:50 | Body #1 discovered by Forensics bot (55% report rate) | CorpseExamine gives +3 clues |
| 0:50-1:25 | Meeting #1 discussion (45s) | Inspector reveals highest-suspicion player; chat active |
| 1:25-2:15 | Voting #1 (50s) | Complex vote with SecretVote (UndercoverAgent) |
| 2:15 | Vote result | Someone ejected; role reveal if enabled |
| 2:15 | Post-meeting grace (4s) | Kill cooldowns restarted |
| 2:30 | Fixer bot sabotages task (Blackout) | Blackout for 35s, vision 35%, interaction 40% |
| 2:30-3:05 | **Kill spree window** during blackout | Gang #2 (Fixer) kills with BodyDrag to hide corpse |
| 3:05 | Blackout repaired by Tech bot | Vision restored |
| 3:10 | Mole bot uses SabotageIntel | EvidenceScore -3 (8P value), gang suspicion -1 |
| 3:30-4:30 | Mid-game task grinding | EvidenceScore climbing ~3/min with 4-5 task-doers |
| 4:30 | Evidence milestone 25% (11/44) | "Preliminary lock" notification |
| 5:00 | Body #2 found (delayed by BodyDrag) | Meeting #2 triggered |
| 5:00-5:45 | Meeting #2 (45s) | Heated discussion; UndercoverAgent considers betrayal |
| 5:45-6:35 | Voting #2 (50s) | Critical ejection |
| 6:35 | Action resumes | 4-6 players remaining |
| 7:00 | Evidence milestone 50% (22/44) | "Key interrogation" notification |
| 8:00 | Evidence milestone 75% (33/44) | UndercoverAgent can now betray |
| 8:00-8:30 | **Undercover betrayal decision point** | If UndercoverAgent betrays: +3 evidence, gang suspicion +2 |
| 9:00 | Mole suspicion check | If Mole suspicion >= 55 (8P threshold), Mole can betray to Gang |
| 9:00-12:00 | Endgame | Evidence race vs time limit |
| 12:00 | Evidence milestone 100% (44/44) or time limit | Police victory if evidence closed; Gang wins if not |
| 20:00 | Hard time limit (if reached) | ResolveTimeLimitOutcome: 82% evidence or 72% tasks = Police win |

#### Expected Outcome

- **Primary path:** Police victory via evidence completion (40% probability) -- 4 police + undercover agent make steady progress.
- **Secondary path:** Gang victory via kills + parity (25% probability) -- 2 killers with Enforcer cooldown reduction can snowball.
- **Tertiary path:** Time limit resolution (20% probability) -- close match goes to 20-minute check.
- **Dramatic path:** Undercover betrayal at 75% evidence (10% probability) -- shifts momentum.
- **Edge case:** Mole betrayal + Gang coordination (5% probability) -- rare but spectacular.

#### Key Validation Items

1. All 8 professions activate their abilities correctly in the same match
2. Enforcer's KillCooldownReduce (0.70x at 8P) creates noticeable kill pressure
3. Fixer's BodyDrag relocates bodies to vent positions correctly
4. Driver's combined VentSpeedBonus (1.8x) + MoveSpeedBonus (1.10x) provides real mobility advantage
5. Two simultaneous killers (Gang=2) don't cause race conditions in KillSystem
6. Meeting capacity (max 2) prevents meeting abuse in 8P
7. ScaledEvidenceTarget correctly returns 44 for 8 players
8. Parity victory condition (aliveGang >= aliveNonGang at 4+ players) fires correctly
9. Ghost mode works for up to 4 dead players simultaneously
10. MatchStatsCollector logs all 8 players' data correctly

---

## 3. Stalemate / Failure Detection Rules

### 3.1 Stalemate Definitions

A match is considered **stalemated** ("卡局") when any of the following conditions are met:

| ID | Condition | Detection Method | Threshold |
|----|-----------|-----------------|-----------|
| S1 | **Global Idle** | Sum of all alive players' `Input.sqrMagnitude` over a rolling window | Sum < 0.01 for all alive players for > 60 consecutive seconds |
| S2 | **No Events** | Time since last kill, body report, meeting, task completion, or sabotage | No qualifying event for > 180 seconds (3 minutes) |
| S3 | **Evidence Stall** | `EvidenceScore` unchanged for extended period | No evidence change for > 300 seconds (5 minutes) during Action phase |
| S4 | **Bot Loop** | All bots stuck in movement loops (same target, no progress) | All bot `_stuckTimers` > `StuckTimeBeforeReroute * 3` (15 seconds) simultaneously |
| S5 | **Meeting Deadlock** | 3+ consecutive meetings with skip-vote majority and no ejection | Track `skipVoteCount / totalVotes` for last 3 meetings; all > 50% skip |
| S6 | **Phase Stuck** | Phase timer fails to advance | `phaseTimer` unchanged for > 2x expected duration (e.g., Meeting phase stuck > 70s when intro=35s) |

### 3.2 Automated Detection Mechanism

The stalemate detector should be implemented as a component on `OnlineMatchController`, running only on the Host:

```
StalemateDetector.Tick(deltaTime):
    if phase != Action: 
        reset all stalemate timers
        return
    
    // S1: Global idle
    float totalInput = sum(p.Input.sqrMagnitude for p in alivePlayers)
    if totalInput < 0.01:
        globalIdleTimer += deltaTime
    else:
        globalIdleTimer = 0
    
    // S2: No events
    noEventTimer += deltaTime
    // Reset on: kill, report, meeting begin, task complete, sabotage
    
    // S3: Evidence stall
    if EvidenceScore != lastEvidenceScore:
        evidenceStallTimer = 0
        lastEvidenceScore = EvidenceScore
    else:
        evidenceStallTimer += deltaTime
    
    // S4: Bot loop
    int stuckBotCount = count(bots where stuckTimer > 15s)
    if stuckBotCount == aliveBotCount and aliveBotCount > 0:
        botLoopTimer += deltaTime
    else:
        botLoopTimer = 0
    
    // S5: Meeting deadlock
    // Tracked in ResolveVotes(): record skip ratio per meeting
    
    // S6: Phase stuck
    if phaseTimer != lastPhaseTimer:
        phaseStuckTimer = 0
        lastPhaseTimer = phaseTimer
    else:
        phaseStuckTimer += deltaTime
    
    // Check thresholds and trigger recovery
    if any threshold exceeded:
        TriggerStalemateRecovery()
```

### 3.3 Recovery Strategies

| Priority | Condition | Recovery Action | Implementation |
|----------|-----------|----------------|---------------|
| 1 | S1: Global Idle > 60s | **Nudge**: Send chat system message "港区沉寂过久，各方开始行动..." + force all bots to pick new random targets | `OnlineBotController.PickBotTarget()` for all bots |
| 2 | S2: No Events > 180s | **Forced Encounter**: Move 2 bots toward the same task location; reduce kill cooldown of gang bots by 50% temporarily | Temporary cooldown override |
| 3 | S4: Bot Loop > 15s | **Bot Reset**: Clear all bot targets and stuck timers, re-pick targets | `_targets.Clear(); _stuckTimers.Clear()` |
| 4 | S3: Evidence Stall > 300s | **Sabotage Injection**: Force a gang-side bot to sabotage the nearest task; trigger evidence leak | `TryInteractWithTask()` on gang bot |
| 5 | S5: Meeting Deadlock (3 rounds) | **Forced Vote**: Next meeting has mandatory voting (skip vote disabled) and shortest player is auto-ejected if tie | Flag in `ResolveVotes()` |
| 6 | S6: Phase Stuck > 2x duration | **Force Advance**: Manually advance phase timer to 0 and trigger the normal phase transition | `phaseTimer = 0f` |
| 7 | **Global Timeout** | If match exceeds `MatchHardLimitSeconds` (already implemented), resolve via `ResolveTimeLimitOutcome()` | Already in code at line 2156-2179 |
| 8 | **Nuclear Option** | If stalemate persists > 5 minutes after any recovery, force game over with draw result | `ForceGameOver("卡局超时：港区行动陷入僵局，本局判定为平局。")` |

### 3.4 Disconnect / Reconnection Rules

| Scenario | Current Implementation | Recommended Enhancement |
|----------|----------------------|------------------------|
| **Client disconnect (non-host)** | `HandleClientDisconnected()` fires; player state preserved in `players` dict | Add 30-second reconnect window. If client reconnects within 30s, restore position and cooldowns. After 30s, convert to bot (set `IsBot=true`) |
| **Host disconnect** | `HostMigrationManager` detects via heartbeat timeout (5s), elects new host by lowest clientId, restores from `GameStateSnapshot` | Current implementation is solid. Add: if migration fails within 30s, fallback to friendly game over (already implemented) |
| **All clients disconnect** | Host is alone; `GetRemainingPlayerCount() <= 1` triggers `FallbackToGameOver()` | Current implementation is correct. Add: auto-save match stats even on abandoned match |
| **Client reconnect during meeting** | No explicit handling currently | Restore vote state from `votes` dict; if player hadn't voted, give them 15s to vote or auto-skip |
| **Client reconnect during action** | No explicit handling currently | Restore position from `players[clientId].Position`; apply 3s invulnerability grace; restore kill/ability/vent cooldowns from server state |
| **Client reconnect during voting** | No explicit handling currently | Same as meeting reconnect; show remaining vote timer |

### 3.5 Host Migration Failure Handling

| Failure Mode | Current Handling | Recommendation |
|-------------|-----------------|----------------|
| Only 1 player remains after host leaves | `FallbackToGameOver("主机已离线，剩余玩家不足。")` | Correct; keep as-is |
| No valid new host found | `FallbackToGameOver("无法选举新主机。")` | Correct; keep as-is |
| Migration timeout (30s) | `FallbackToGameOver("主机迁移超时（30s），自动结算。")` | Correct; 30s is reasonable |
| Snapshot version mismatch | Warning logged, best-effort restore | Add: if >3 critical fields fail to restore, force game over instead of corrupted state |
| New host also disconnects during migration | Not explicitly handled | Add: if `migrationInProgress && hostDisconnectedDetected` fires again within 10s, immediately `FallbackToGameOver("连续主机断连，对局终止。")` |

### 3.6 Anomaly Detection Metrics (for MatchStatsCollector)

Add these fields to `MatchLogEntry` for post-match analysis:

| Metric | Type | Purpose |
|--------|------|---------|
| `StalemateRecoveryCount` | int | How many times recovery was triggered |
| `LongestIdleSeconds` | float | Maximum consecutive idle time |
| `LongestNoEventSeconds` | float | Maximum time without any game event |
| `DisconnectCount` | int | Total client disconnections |
| `MigrationAttempts` | int | Host migration attempts |
| `ForcedEnding` | bool | Whether match ended via stalemate force-end |

### 3.7 Edge Case: Zero-Player / All-Bot Matches

When all human players disconnect and only bots remain:

1. **Detection:** `CountHumanPlayers() == 0` checked every 10 seconds during Action phase
2. **Grace period:** Wait 30 seconds (allowing for reconnection)
3. **Action:** If still zero humans, call `ForceGameOver("所有玩家已离线，对局自动终止。")`
4. **Stats:** Still log the match via `MatchStatsCollector.LogMatch()` but mark `WinningFaction = "Abandoned"`

---

## Appendix A: Source File Index

| File | Path | Key Contents |
|------|------|-------------|
| OnlineRuleSet.cs | `Assets/_Project/Scripts/Online/OnlineRuleSet.cs` | All ScriptableObject parameters: role distribution, pacing, kill, meeting, sabotage, abilities |
| OnlineMatchController.cs | `Assets/_Project/Scripts/Online/OnlineMatchController.cs` | Main game loop, role assignment, kill logic, meeting/voting, win conditions |
| OnlineMatchController.Gameplay.cs | `Assets/_Project/Scripts/Online/OnlineMatchController.Gameplay.cs` | Gameplay partial: smoke tests, sabotage effects, task system, evidence |
| KillSystem.cs | `Assets/_Project/Scripts/Online/KillSystem.cs` | Kill button UI, cooldown, body management, report button, screen flash |
| SabotageSync.cs | `Assets/_Project/Scripts/Online/SabotageSync.cs` | Client-side sabotage UI sync, repair hints, color coding |
| OnlineBotController.cs | `Assets/_Project/Scripts/Online/Bots/OnlineBotController.cs` | Bot AI: movement, kill, task, sabotage, voting, stuck detection |
| HostMigrationManager.cs | `Assets/_Project/Scripts/Online/HostMigrationManager.cs` | Host heartbeat, migration election, snapshot restore |
| MatchStatsCollector.cs | `Assets/_Project/Scripts/Online/MatchStatsCollector.cs` | Match logging, JSON export, statistical fields |
| GhostMode.cs | `Assets/_Project/Scripts/Gameplay/GhostMode.cs` | Post-death ghost behavior: transparency, speed, collision |
| VictoryEvaluator.cs | `Assets/_Project/Scripts/Gameplay/VictoryEvaluator.cs` | Offline dual-infiltration victory conditions |
| GameState.cs | `Assets/_Project/Scripts/Core/GameState.cs` | Core state model: evidence, suspicion, days, districts |
| OnlineVictoryBridge | (via syncManager.EvaluateVictory) | Online victory bridge combining online + offline rules |

## Appendix B: Computed Scaling Formulas

```
// Evidence Target Scaling (OnlineRuleSet.ScaledEvidenceTarget)
ScaledTarget(N) = Clamp(Round(44 * Clamp(N/8, 0.6, 1.3)), MinTarget, MaxTarget)

  4P: 44 * 0.6 = 26.4 -> Clamp(26, 34, 56) = 34  (hits floor)
  5P: 44 * 0.625 = 27.5 -> Clamp(28, 34, 56) = 34  (hits floor)
  6P: 44 * 0.75 = 33 -> Clamp(33, 34, 56) = 34  (hits floor)
  7P: 44 * 0.875 = 38.5 -> Clamp(39, 34, 56) = 39
  8P: 44 * 1.0 = 44 -> Clamp(44, 34, 56) = 44
  9P: 44 * 1.125 = 49.5 -> Clamp(50, 34, 56) = 50
 10P: 44 * 1.25 = 55 -> Clamp(55, 34, 56) = 55

// Emergency Meeting Limit (OnlineRuleSet.EmergencyMeetingLimitFor)
MeetingLimit(N) = Clamp(N/3, 1, MaxEmergencyMeetings)

  4P: Clamp(1, 1, 3) = 1
  5P: Clamp(1, 1, 3) = 1
  6P: Clamp(2, 1, 3) = 2
  7P: Clamp(2, 1, 3) = 2
  8P: Clamp(2, 1, 3) = 2
  9P: Clamp(3, 1, 3) = 3
 10P: Clamp(3, 1, 3) = 3

// Total Task Count (OnlineRuleSet.TotalTaskCount)
TotalTasks(N, gang) = Max(1, N - gang) * TasksPerNonGangPlayer

  4P(gang=1): 3 * 5 = 15 (with 4P suggestion of 5 tasks/person)
  6P(gang=1): 5 * 4 = 20 (but with UC, actual non-gang=4: 4*4=16)
  8P(gang=2): 6 * 3 = 18 (with 8P suggestion of 3 tasks/person)

// Enforcer Effective Kill Cooldown
EffectiveKillCD = KillCooldownSeconds * Enforcer.KillCooldownReduce

  4P: 30 * 0.80 = 24s
  6P: 25 * 0.75 = 18.75s
  8P: 22 * 0.70 = 15.4s
```

## Appendix C: Glossary

| Term | Definition |
|------|-----------|
| EvidenceScore | Current accumulated evidence points (team-shared among Police faction) |
| EvidenceTarget | Threshold for Police victory via evidence chain completion |
| ScaledEvidenceTarget | EvidenceTarget adjusted by player count using formula above |
| Parity Rule | Gang wins if aliveGang >= aliveNonGang when total alive >= 4 |
| Post-Meeting Grace | Brief invulnerability period after meeting to prevent spawn-killing |
| BodyDrag | Fixer profession ability: relocate corpses to vent positions |
| SabotageIntel | Mole ability: reduce EvidenceScore by 2-3, reduce gang suspicion |
| Undercover Betrayal | UndercoverAgent switches public role to Police at 75% evidence |
| Mole Betrayal | Mole switches public role to Gang at suspicion >= 55-65 |
| Stalemate | Game state where no meaningful progress is being made |
| Host Migration | Process of electing a new host when current host disconnects |

---

> **Review note:** All "Code Current Value" entries in Section 1 are extracted directly from source files with line references. Suggested values for 4P/6P/8P are design recommendations based on the existing system architecture and established pacing targets (8-15 minute matches games, 45-55% faction win rate). Implementation priority should follow: 1) MinimumPlayablePlayers fix for 4P support, 2) ScaledEvidenceTarget floor adjustment, 3) per-player-count kill/meeting cooldowns, 4) stalemate detection system.
