# Phase 2 Art & Audio Asset Inventory

**Project:** Gangland Undercover / 港区潜线
**Generated:** 2026-06-16
**Basis:** File system scan of `Assets/_Project/` and `TempAssets/`

---

## 1. Available Asset Inventory

### 1.1 2D Characters (Runtime Sprite Sheets)

All runtime characters live under `Assets/_Project/Resources/Sprites/Characters/`.
Each profession has 4 directional folders (Back / Front / Left / Right), each containing 1 idle + 3 walk frames = **4 frames per direction, 16 frames total per profession**.

| Profession | Directory | Frames | Resolution |
|---|---|---|---|
| Driver | `Resources/Sprites/Characters/Driver/` | 16 (4 dir x 4 frames) | ~32x32 px per frame (pixel art) |
| Enforcer | `Resources/Sprites/Characters/Enforcer/` | 16 | ~32x32 px |
| Fixer | `Resources/Sprites/Characters/Fixer/` | 16 | ~32x32 px |
| Forensics | `Resources/Sprites/Characters/Forensics/` | 16 | ~32x32 px |
| Inspector | `Resources/Sprites/Characters/Inspector/` | 16 | ~32x32 px |
| Mole | `Resources/Sprites/Characters/Mole/` | 16 | ~32x32 px |
| Tech | `Resources/Sprites/Characters/Tech/` | 16 | ~32x32 px |
| UndercoverAgent | `Resources/Sprites/Characters/UndercoverAgent/` | 16 | ~32x32 px |

**Total:** 8 professions, 128 character sprites + 1 `cc0_manifest.json`.

**Source art (high-res sheets for re-export):** `Assets/_Project/Art/2D/Characters/` contains 7 professions (driver, enforcer, inspector, medic, mole, tech, undercover), each with 10 PNG sheets (idle/walk x 5 directional: default, down, left, right, up). These are the authoring originals at higher resolution, not directly loaded at runtime.

> **Note:** The Art source directory has a `medic` profession that does not have a corresponding runtime sprite in Resources. The runtime directory has `Fixer`, `Forensics`, and `UndercoverAgent` professions not present in the Art source directory. This naming mismatch should be reconciled.

### 1.2 2D Tiles / Maps

Three themed tile sets under `Assets/_Project/Art/2D/Tiles/`:

| Theme | Directory | File Count | Coverage |
|---|---|---|---|
| **Harbour** | `Art/2D/Tiles/Harbour/` | 27 PNGs | Floors (concrete, metal, checker, wet, electrical, metal grid, tile), walls (brick, corner, side, top), props (barrels, crates, containers, desks, doors, machines, neon signs, puddles, vents, cable floor) |
| **Kowloon** | `Art/2D/Tiles/Kowloon/` | 22 PNGs | Floors (concrete old, herb, tile old, wood, wood dark), walls (brick old, concrete), props (bamboo scaffold, herb cabinet, lanterns, mahjong table, neon signs, puddles, shop signs, vents, wires, old crates, rusty doors) |
| **Police** | `Art/2D/Tiles/Police/` | 17 PNGs | Floors (armory, cell, interrogation, linoleum, tile blue, tile white), walls (blue stripe, white), props (bars, desks, filing cabinet, locker, mirror, whiteboard, doors, reception desk) |

**Runtime tilesets (actively loaded):** `Assets/_Project/Resources/Sprites/Tilesets/` -- 291 PNG files total including:
- **LimeZu curated slices:** ~60 PNGs from Modern Interiors, Modern Exteriors, and Modern Office Revamped (see `limezu_runtime_manifest.json`)
- Additional free/CC0 tilesets for floor/wall/prop generation

### 1.3 2D VFX

All VFX under `Assets/_Project/Art/2D/VFX/`. Each effect has both individual frames and a combined sprite sheet.

| VFX Name | Directory | Frames | Sprite Sheet |
|---|---|---|---|
| blackout | `VFX/blackout/` | 12 (blackout_00..11) | `vfx_blackout_sheet.png` |
| comms_jam | `VFX/comms_jam/` | 8 (comms_jam_00..07) | `vfx_comms_jam_sheet.png` |
| door_lock | `VFX/door_lock/` | 6 (door_lock_00..05) | `vfx_door_lock_sheet.png` |
| emergency_light | `VFX/emergency_light/` | 8 (emergency_light_00..07) | `vfx_emergency_light_sheet.png` |
| evidence_leak | `VFX/evidence_leak/` | 12 (evidence_leak_00..11) | `vfx_evidence_leak_sheet.png` |
| hit | `VFX/hit/` | 4 (hit_00..03) | `vfx_hit_sheet.png` |
| kill | `VFX/kill/` | 10 (kill_00..09) | `vfx_kill_sheet.png` |
| patrol_alert | `VFX/patrol_alert/` | 4 (patrol_alert_00..03) | `vfx_patrol_alert_sheet.png` |

**Total:** 8 VFX effects, 64 individual frames + 8 sprite sheets.

### 1.4 2D UI

All UI sprites under `Assets/_Project/Art/2D/UI/`:

| Category | Files | Details |
|---|---|---|
| **Buttons (Blue)** | btn_blue_normal/hover/pressed/disabled | 4 states |
| **Buttons (Purple)** | btn_purple_normal/hover/pressed/disabled | 4 states |
| **Buttons (Red)** | btn_red_normal/hover/pressed/disabled | 4 states |
| **Badges** | badge_gangster, badge_mole, badge_police, badge_undercover | 4 role badges |
| **Icons** | icon_back, icon_close, icon_confirm, icon_info, icon_lock, icon_pause, icon_play, icon_question, icon_refresh, icon_settings, icon_skull, icon_warning | 12 icons |
| **Panels** | panel_small, panel_medium, panel_large | 3 sizes |
| **Progress bars** | progress_000, progress_025, progress_050, progress_050_red, progress_075, progress_100 | 6 stages |
| **Tabs** | tab_active_1, tab_inactive_2, tab_inactive_3 | 3 tab states |
| **Misc** | checkbox_off/on, toggle_off/on, portrait_frame | 5 items |

**Total:** ~46 unique UI sprites covering a complete UI kit.

### 1.5 2D Props (Task Stations)

All task station props under `Assets/_Project/Art/2D/Props/TaskStations/`:

| Task Type | States | Files |
|---|---|---|
| calibrate | idle / active / complete / sabotaged | 4 |
| download | idle / active / complete / sabotaged | 4 |
| evidence | idle / active / complete / sabotaged | 4 |
| keypad | idle / active / complete / sabotaged | 4 |
| memory | idle / active / complete / sabotaged | 4 |
| radar | idle / active / complete / sabotaged | 4 |
| scan | idle / active / complete / sabotaged | 4 |
| sort | idle / active / complete / sabotaged | 4 |
| swipecard | idle / active / complete / sabotaged | 4 |
| tap | idle / active / complete / sabotaged | 4 |
| wire | idle / active / complete / sabotaged | 4 |

**Total:** 11 task types x 4 states = **44 task station tiles**.

### 1.6 3D Models (Legacy)

All 3D models are classified as Legacy and are **not loaded at runtime** in the current 2D pipeline. They remain available for potential future use or 2.5D transition.

#### 1.6.1 SimplePoly City - Low Poly Assets
- **Path:** `Assets/_Project/Legacy3D/SimplePoly City - Low Poly Assets/`
- **Format:** FBX models with PNG textures and `.mat` materials
- **Categories:**
  - **Buildings** (38 prefabs): Sky big/small (3 colors each), Auto Service, Bakery, Bar, Books Shop, Chicken Shop, Clothing, Coffee Shop, Drug Store, Factory, Fast Food, Fruits Shop, Gas Station, Gift Shop, House_01-04 (3 colors each), Music Store, Pizza, Residential (3 colors), Restaurant, Shoes Shop, Stadium, Super Market
  - **Vehicles** (38 prefabs, 2 variants each): Ambulance, Bus (3 colors), Car (3 colors), Container (3 colors), Pick up Truck (3 colors), Police Car, SUV (3 colors), Taxi, Truck (3 colors) -- available in both "Separated Wheels" and "Static Wheels" variants
  - **Roads** (16 prefabs): Concrete Tile, Corner, Intersection, Lane variants, Sidewalk, Split Line, T-Intersection
  - **Nature** (15 prefabs): Big Tree, Bushes, Cube Tree, Fir Tree, Grass variants, House Floor, Pot Bush, Rock
  - **Props** (20 prefabs): Bench, BillBoard, Bus Stop, Cafe furniture, Dustbin, Fence, Hydrant, Roof props, Street Light, Traffic signs/signals/cone, Windmill

#### 1.6.2 ModularLowpolyStreetsFree
- **Path:** `Assets/_Project/Legacy3D/ModularLowpolyStreetsFree/`
- **Format:** FBX with AlbedoGloss/Normal/Metall/AO texture sets
- **Categories:**
  - **Roads** (7 prefabs): Crossroads, Road lines, Pavement, Road turns
  - **Complex** (6 prefabs): Crossroads variants, Road line segments, Road turns
  - **Other** (15 prefabs): Bench, Cafe furniture, Hydrant, Poles, Sewer grills/hatches, Traffic cone/light, Trash can, Tree, Pole components
- **FBX source files:** 7 (Bench, Cafe, Nature, Other, Poles, Roads, TrafficLights)

#### 1.6.3 Synty PolygonStarter
- **Path:** `Assets/_Project/Legacy3D/Synty/PolygonStarter/`
- **Format:** FBX models, 4 texture sets (PolygonStarter_01-04), plane textures
- **Categories:**
  - **Characters** (6 prefabs): SM_Bean_Cowboy, SM_Bean_Cop, SM_Bean_Female, SM_Bean_Town_Female, SM_Chr_Female, SM_Chr_Male
  - **Buildings** (7 prefabs): Block, Column, DoorFrame, Floor (1x1, 5x5), Ramp (25/45 degree), Stairs (1x1, 1x3), WallDoor, WallWindow
  - **Vehicles** (2 prefabs): SM_PolygonCity_Veh_Car_Small, SM_Veh_Plane_Stunt
  - **Weapons** (4 prefabs): SM_Wep_Shield, SM_Wep_Watergun (2), SM_Wep_WaterPistol
  - **Props** (6 prefabs): Coin, Crate, Cone, Ladder, Sword, Target, Arrow
  - **Primitives** (4 prefabs): Cylinder, Sphere, Tube, Cone
  - **Environment** (12 prefabs): Cloud, CloudRing, Ground variants, Mountains, Rocks, Sky Dome, Trees, TreeStump, House
  - **Materials:** 4 base + 4 plane + 4 misc (Clouds, Sky, Glass)

#### 1.6.4 DenysAlmaral CityPeople (Free Samples)
- **Path:** `Assets/_Project/Legacy3D/DenysAlmaral/CityPeople/`
- **Format:** FBX characters with palette textures (23 skin variants), animation FBX
- **Categories:**
  - **Characters** (8 prefabs): casual_Female_G, casual_Male_G, casual_Female_K, casual_Male_K, elder_Female_A, little_boy_B, Doctor_Male_B, police_Female_A, worker_Male_constructor_B, prostheticLeg_girl
  - **Animations** (20+ FBX): idle (male/female variants), walk/jog/run (male/female), dance (afro, flossing, hype, riverdance), construction tool use (drill, hammer, handsaw, pipewrench, screwdriver, wrench), exercise, phone talking
  - **Props** (13 prefabs): backdrop, building blocks (A-D), bicycle, chair, plantPot, roundTable, streetSign, trashBin, plane_floor, santa_hat
  - **Tools** (6 prefabs): drill, hammer, handSaw, pipewrench, screwdriver, wrench

#### 1.6.5 Quaternius ModularSciFiMegaKit (In Resources)
- **Path:** `Assets/_Project/Resources/Quaternius/ModularSciFiMegaKit/`
- **Format:** FBX models with PBR texture sets (BaseColor, Normal, ORM, Emissive, DetailMask)
- **FBX Subdirectories:** Aliens, Columns, Decals, Platforms, Props, Walls
- **Walls:** ~80+ wall variants (Top/Bottom/ShortWall/Wall subtypes, Straight/Corner Inner/Outer, Round/Square/Curve, multiple material themes)
- **Textures:** 3 trim sets (BaseColor + Normal + ORM + Emissive + DetailMask + color variants), PaddedWall set, Decals
- **FBX count:** 189 FBX files
- **Status:** In Resources path (could be loaded at runtime), but **not currently referenced** by any 2D gameplay code

#### 1.6.6 FreePackUnused (Legacy Audio/3D Misc)
- **Path:** `Assets/_Project/Legacy3D/FreePackUnused/`
- **Format:** WAV audio files
- **Content:** ~36 miscellaneous sound effects (Autocannon, Bullet Impact, Cannon impact, Carriage, Cavern Atmosphere, Computer explosion, Dragon Spit Fire, Earthquake, Explosion, Flare gun, Forest, Ghost, Hand Gun, Heavy Object Impact, Ice Cavern, Laser Gun, Machine Gun, Magic Spell, Metal Impact, Missile, Monster Bite, Railgun, Row Boat, Spaceship, Tsunami, Walking in ChainMail)
- **Status:** Not referenced in current build; stored as unused legacy

### 1.7 Audio -- BGM

| File | Path | Format | Purpose |
|---|---|---|---|
| BGM_MainMenu.ogg | `Resources/Audio/BGM/` | OGG | Main menu background music |
| BGM_InGame.ogg | `Resources/Audio/BGM/` | OGG | In-game exploration/tension BGM |
| BGM_Meeting.ogg | `Resources/Audio/BGM/` | OGG | Meeting/discussion phase BGM |

Additional BGM in `Assets/_Project/Audio/BGM/` (non-Runtime source):
| File | Format | Purpose |
|---|---|---|
| bgm_explore.ogg | OGG | Exploration phase |
| bgm_lobby.ogg | OGG | Lobby/waiting room |
| bgm_meeting.ogg | OGG | Meeting phase (alt) |
| bgm_menu.ogg | OGG | Menu (alt) |
| bgm_threat.ogg | OGG | Threat/tension escalation |
| bgm_victory_gang.ogg | OGG | Gang victory |
| bgm_victory_police.ogg | OGG | Police victory |
| bgm_vote.ogg | OGG | Voting phase |

**Runtime-loaded BGM:** 3 tracks (via `Resources/Audio/BGM/`). The additional 8 source files in `_Project/Audio/BGM/` are available but not currently auto-loaded by `AudioManager`.

### 1.8 Audio -- Ambience

| File | Path | Format |
|---|---|---|
| amb_harbour_rain.ogg | `Resources/Audio/Ambience/` and `_Project/Audio/Ambience/` | OGG |
| amb_kowloon_neon.ogg | `Resources/Audio/Ambience/` and `_Project/Audio/Ambience/` | OGG |
| amb_police_interior.ogg | `_Project/Audio/Ambience/` only | OGG |

### 1.9 Audio -- SFX

#### Core SFX (Runtime, via Kenney curated in Resources)

All 16 core SFX are loaded from `Resources/Audio/SFX/Kenney/` (OGG) with WAV fallbacks in `Resources/Audio/SFX/`:

| SFX Name | AudioManager Enum | Purpose |
|---|---|---|
| SFX_UIClick | UIClick | UI button click |
| SFX_Footstep | Footstep | Character movement |
| SFX_Kill | Kill | Kill action |
| SFX_BodyReport | BodyReport | Body discovered |
| SFX_Report | Report | Report filed |
| SFX_MeetingStart | MeetingStart | Meeting begins |
| SFX_VoteCast | VoteCast | Vote submitted |
| SFX_PlayerEliminated | PlayerEliminated | Player ejected |
| SFX_TaskComplete | TaskComplete | Task finished |
| SFX_Sabotage | Sabotage | Sabotage triggered |
| SFX_Victory | Victory | Match victory |
| SFX_Defeat | Defeat | Match defeat |
| SFX_Emergency | Emergency | Emergency button |
| SFX_VentOpen | VentOpen | Ventilation open |
| SFX_VentClose | VentClose | Ventilation close |
| SFX_ButtonHover | ButtonHover | UI hover feedback |

#### Extended SFX (Source directory, not auto-loaded)

Under `Assets/_Project/Audio/SFX/`:

| Category | File Count | Contents |
|---|---|---|
| **Core** | 24 OGG | sfx_body_report, sfx_countdown, sfx_defeat, sfx_eliminated, sfx_emergency, sfx_kill, sfx_meeting_start, sfx_player_ejected, sfx_round_start, sfx_task_complete, sfx_task_start, sfx_ui_back/click/confirm/error/notify/toggle, sfx_victory_draw/gang/mole/police/undercover, sfx_vote_cast/skip |
| **DesertShooter** | 37 OGG | coin (4), error (3), explosion (3), fall (2), hurt (5), jump (6), lose (4), move (4), select (1), shoot (8) |
| **Footsteps** | 4 OGG | sfx_step_concrete/metal/wet/wood |
| **LevelUp** | 14 AIF | Alarm, Coin01, Downer01, FX01-02, Rise01-07, Upper01 |
| **Sabotage** | 14 OGG | sfx_alarm_loop, sfx_blackout, sfx_comms_jam, sfx_door_creak/lock/slam/unlock, sfx_evidence_leak, sfx_glass_break, sfx_metal_clang, sfx_patrol_alert, sfx_player_hit, sfx_power_restore, sfx_water_drip |
| **Tasks** | 15 OGG | sfx_task_calibrate, download_tick, fail, keypad_press, memory_light, page_flip, progress, radar_ping, repair, scan, sort, swipe_card, tap, unlock, wire_cut |

#### UI Sound Library

Under `Assets/_Project/Audio/UI/`: **77 OGG files** covering:
- back (4), bong (1), click (5), close (4), confirmation (4), drop (4), error (8), glass (6), glitch (4), maximize (9), minimize (9), open (4), pluck (2), question (4), scratch (5), scroll (5), select (8), switch (7), tick (3), toggle (4)

#### SFX Event Mapping (Juhani Junkala 512 Retro SFX -- CC0)

The file `sfx_event_mapping.json` maps 50 game events to 651 candidate WAV files from the "Juhani Junkala - 512 Retro SFX" CC0 pack. Categories include: Weapons, Death Screams, Explosions, Movement (footsteps, jumping, doors, ladders, vehicles, portals), General Sounds (buttons, bleeps, coins, fanfares, interactions, menus, alarms, damage, impacts, pause, positive, negative, weird).

### 1.10 Prefabs (Non-Legacy, Active)

| Prefab | Path | Purpose |
|---|---|---|
| Stage1VerticalSliceWorld | `Prefabs/` | Stage 1 world prefab |
| OnlineMiniGameBridge | `Resources/Network/` | Networked mini-game bridge |
| OnlineSecurityCamera | `Resources/Network/` | Networked security camera |
| Stage2_Civilian | `Resources/Stage2/Characters/` | Stage 2 civilian character |
| Stage2_Gang | `Resources/Stage2/Characters/` | Stage 2 gang member |
| Stage2_Police | `Resources/Stage2/Characters/` | Stage 2 police character |
| Stage2_Undercover | `Resources/Stage2/Characters/` | Stage 2 undercover agent |

**Total active prefabs:** 7 (excluding ~200+ Legacy3D prefabs).

### 1.11 Third-Party Sprites (Kenney, in Sprites/)

Under `Assets/_Project/Sprites/Kenney/` -- directories exist but contain only `.meta` files (no actual PNGs):
- `Buildings/LowPoly/`
- `Buildings/Details/`
- `Roads/`
- `Characters/` and `Characters/Accessories/`

**Status:** Directory structure is scaffolded but actual asset files are not present. Kenney sprite references in the project are loaded from `.asset_cache/free/kenney` or from `Resources/Sprites/` instead.

### 1.12 Animators

Under `Assets/_Project/Art/Animators/`:
- `GanglandCharacter.controller` -- Main character animation controller
- `GanglandCharacter_Override.controller` -- Override layer variant
- `GanglandCharacter.controller.backup` -- Backup copy

---

## 2. Gap Analysis

Based on comparing available assets against typical game requirements for a social deduction game with Among Us-style mechanics:

### 2.1 Character Gaps

| Gap | Priority | Recommendation | Est. Cost/Effort |
|---|---|---|---|
| Death/kill animation frames (per profession) | **P0** | Current `kill` VFX is generic; per-profession death sprites improve feedback | 2-3 days art / self-made |
| Vent crawl animation | **P1** | No dedicated vent movement sprite; Footstep SFX exists but no visual | 1 day / self-made |
| Emote/reaction sprites | **P2** | No chat bubble icons or reaction emotes for meeting phase | 1-2 days / self-made or Kenney |
| `medic` Art source -> runtime mismatch | **P1** | Art/2D/Characters has `medic` but Resources has no Medic runtime sprite; Resources has `Fixer`/`Forensics` not in Art source | 0.5 day / rename & reconcile |

### 2.2 Tile/Map Gaps

| Gap | Priority | Recommendation | Est. Cost/Effort |
|---|---|---|---|
| 4th map theme (e.g., Rooftop / Underground) | **P1** | Only 3 themes (Harbour, Kowloon, Police); variety improves replayability | 3-5 days / self-made or purchase additional LimeZu pack |
| Animated tiles (neon flicker, water ripple) | **P2** | Current neon/water tiles are static; animated versions add atmosphere | 2-3 days / self-made |
| Transition tiles between themes | **P2** | No dedicated transition pieces between Harbour/Kowloon/Police zones | 1-2 days / self-made |

### 2.3 VFX Gaps

| Gap | Priority | Recommendation | Est. Cost/Effort |
|---|---|---|---|
| Vent open/close VFX | **P1** | AudioManager has VentOpen/VentClose SFX but no matching visual VFX sheet | 1 day / self-made |
| Sabotage activation VFX (per type) | **P1** | Sabotage SFX exist (blackout, comms_jam, door_lock) but no all have dedicated VFX | 2 days / self-made |
| Task success/fail flash | **P1** | Generic hit VFX exists but no task-specific success/fail flash | 0.5 day / self-made |
| Footstep dust particles | **P2** | No movement particle effect | 0.5 day / self-made |

### 2.4 UI Gaps

| Gap | Priority | Recommendation | Est. Cost/Effort |
|---|---|---|---|
| Meeting/voting screen role cards | **P1** | Badges exist (4 roles) but no full role reveal card layout | 1-2 days / self-made |
| Minimap overlay sprites | **P1** | No minimap or tactical overlay icons | 1 day / self-made |
| Loading/transition screen art | **P1** | No dedicated loading screen or scene transition art | 2-3 days / self-made or AI-generated |
| Settings/option screen icons (audio sliders, language) | **P2** | Only generic icon_settings exists; no volume/language specific icons | 0.5 day / self-made |
| Chat/message bubble UI | **P2** | No chat bubble sprite for in-meeting text chat | 0.5 day / self-made |

### 2.5 Audio Gaps

| Gap | Priority | Recommendation | Est. Cost/Effort |
|---|---|---|---|
| Dedicated victory BGM per faction (runtime) | **P0** | Source files exist (bgm_victory_gang/police.ogg) but AudioManager only maps 1 generic Victory SFX; BGM not wired per-faction | 0.5 day code + audio editing |
| Vent crawl SFX | **P1** | No dedicated vent movement sound; VentOpen/VentClose exist but no crawl loop | Find from 512 Retro SFX CC0 candidates or record |
| Ambient loops per map theme | **P1** | Only 2 ambient loops (harbour_rain, kowloon_neon); Police and future maps need their own | 1 day / source from CC0 libraries or record |
| Meeting countdown/tension SFX | **P1** | sfx_countdown.ogg exists in source but not mapped in AudioManager SoundEffect enum | 0.5 day code wiring |
| BGM for lobby, threat, vote phases | **P1** | Source files exist but not wired to AudioManager MusicTrack enum | 0.5 day code wiring |
| Alarm SFX (police/facility) | **P2** | sfx_event_mapping.json shows alarm_police and alarm_facility have **0 candidates** in Juhani Junkala pack | Source from Freesound CC0 or self-record |
| Voice lines / callouts | **P2** | No voice acting or synthesized callouts | Outsource or use TTS; low priority |

### 2.6 3D Model Gaps (If 2.5D Transition Planned)

| Gap | Priority | Recommendation | Est. Cost/Effort |
|---|---|---|---|
| Low-poly Hong Kong harbor environment | **P2** | SimplePoly City has generic buildings but no HK-specific landmarks | Purchase dedicated HK/Asian city pack or custom model |
| Police station interior 3D | **P2** | No dedicated police station 3D model set | Combine SimplePoly interiors + custom |
| Gang hideout 3D | **P2** | No gang-specific interior set | Custom model or Asset Store purchase |

---

## 3. Prefab Usage Recommendations

### 3.1 Current Organization Assessment

**Strengths:**
- Clean separation: active prefabs (7) in `Assets/_Project/Prefabs/` and `Resources/Stage2/`, legacy in `Legacy3D/`
- Network prefabs properly isolated in `Resources/Network/`
- Stage-based character prefabs clearly named (`Stage2_Civilian`, `Stage2_Gang`, etc.)

**Weaknesses:**
- No dedicated `Prefabs/Props/`, `Prefabs/VFX/`, `Prefabs/UI/` subdirectories for future expansion
- Legacy3D prefabs (~200+) still in the project tree, increasing import time and search noise
- No `Addressables` or `AssetBundle` strategy; everything runtime-loaded via `Resources.Load`

### 3.2 Recommended Prefab Naming Convention

```
# Characters
Character_{Profession}_{Variant}.prefab
  Example: Character_Inspector_Default.prefab

# Props
Prop_{Category}_{Name}.prefab
  Example: Prop_TaskStation_Keypad.prefab

# VFX
VFX_{EffectName}.prefab
  Example: VFX_Blackout.prefab

# Network
Network_{Role}_{Name}.prefab
  Example: Network_Player_Avatar.prefab

# UI
UI_{Screen}_{Element}.prefab
  Example: UI_Meeting_VotePanel.prefab
```

### 3.3 NetworkPrefab Registration Recommendations

1. **Register `OnlineMiniGameBridge` and `OnlineSecurityCamera`** in the NetworkManager's NetworkPrefab list if not already done.
2. **Add `Stage2_*` character prefabs** as spawnable network objects with `NetworkObject` component.
3. **Consider a `NetworkSpawnRegistry` ScriptableObject** that maps Profession enum -> Prefab reference, avoiding hardcoded `Resources.Load` paths.

### 3.4 2D/3D Hybrid Transition Strategy

If the project ever moves toward 2.5D or uses 3D backgrounds with 2D characters:

1. **Phase 1 (Current):** Pure 2D. All gameplay uses sprites. Legacy3D is dormant.
2. **Phase 2 (Optional):** Use Quaternius ModularSciFiMegaKit walls as 3D background dioramas with 2D sprite overlays for characters and interactable props. The SciFi kit is already in Resources with PBR textures.
3. **Phase 3 (Full 3D, if ever):** Promote SimplePoly City or DenysAlmaral CityPeople to active, replace 2D tiles with 3D meshes. This would require significant rework of `OnlineWorldBuilder` and collision systems.

**Immediate recommendation:** Stay in Phase 1. Do not activate Legacy3D assets in the current build.

---

## 4. License / Credits Draft

### 4.1 Third-Party Asset Sources

| Source | Asset Type | License | Status | Files in Project |
|---|---|---|---|---|
| **Kenney** (kenney.nl) | UI SFX, Impact SFX, Buildings, Roads, Characters sprites | CC0 1.0 Universal | Active runtime (SFX via curated selection) | 16 curated OGG in Resources; sprite dirs scaffolded |
| **LimeZu** (itch.io/limezu) | 2D tile sprites (Interiors, Exteriors, Office) | CC0 | Active runtime (~60 curated PNGs) | Purchased packs cached in `.asset_cache/purchased/limezu` |
| **Quaternius** (quaternius.com) | Modular SciFi Mega Kit (3D FBX + PBR textures), Modular Lowpoly Streets | CC0 | Legacy3D (not in runtime build); SciFi kit in Resources but unreferenced | 189 FBX + textures in Resources; street kit in Legacy3D |
| **Synty Studios** (syntystudios.com) | PolygonStarter (characters, vehicles, buildings, props) | Unity Asset Store Free | Legacy3D only | ~50 FBX + prefabs |
| **DenysAlmaral** (Asset Store) | CityPeople free samples (characters, animations, props) | Unity Asset Store Free | Legacy3D only | 8+ character FBX, 20+ animation FBX |
| **SimplePoly City** (Asset Store) | Low Poly Assets (buildings, vehicles, roads, nature, props) | Unity Asset Store Free | Legacy3D only | ~38 building + ~38 vehicle + ~50 misc prefabs |
| **Juhani Junkala** (OpenGameArt) | 512 Retro SFX (weapons, explosions, movement, UI, etc.) | CC0 | Event mapping JSON exists; candidates not yet imported | 0 (mapping only, no files imported yet) |
| **ModularLowpolyStreetsFree** (Asset Store) | Street furniture and road segments | Unity Asset Store Free | Legacy3D only | 7 FBX + ~30 prefabs |
| **FreePackUnused** (Asset Store) | Miscellaneous sound effects | Unity Asset Store Free | Legacy3D, unused | ~36 WAV files |
| **Unity Asset Store "Free Pack"** | Mixed (exact contents TBD) | Pending confirmation | In Resources, usage unclear | Needs audit |

### 4.2 Pending License Confirmations

| Item | Question | Action Needed |
|---|---|---|
| Free Pack (Asset Store) | Exact license type? EULA? | Check Asset Store purchase receipt and EULA |
| CJKPixelFallback font | Open source or commercial? | Verify font file origin and license |
| Juhani Junkala 512 Retro SFX | CC0 confirmed on OpenGameArt | Safe to import; no attribution required but credited |
| LimeZu packs | CC0 confirmed on itch.io | Safe for commercial use |
| Kenney assets | CC0 1.0 confirmed | Safe for commercial use |

### 4.3 Credits Draft (for Steam Store Page / In-Game Credits)

```
========================================================
  GANGELAND UNDERCOVER / 港区潜线
  Art & Audio Credits
========================================================

VISUAL ASSETS
-------------
2D Environment Tiles:
  LimeZu (Modern Interiors, Modern Exteriors,
  Modern Office Revamped) -- itch.io/limezu (CC0)

2D Sprite Art:
  Kenney (Buildings, Roads, Characters) -- kenney.nl (CC0 1.0)
  Original project art by the Gangland Undercover team

3D Models (Legacy / Prototype):
  Quaternius (Modular SciFi Mega Kit,
  Modular Lowpoly Streets) -- quaternius.com (CC0)
  Synty Studios (PolygonStarter) -- syntystudios.com
  DenysAlmaral (CityPeople Free Samples)
  SimplePoly City -- Unity Asset Store (Free)

AUDIO
-----
Background Music & Sound Effects:
  Kenney (Interface Sounds, Impact Sounds) -- kenney.nl (CC0 1.0)
  Juhani Junkala (512 Retro SFX) -- opengameart.org (CC0)
  Original sound design by the Gangland Undercover team

ENGINE & TECHNOLOGY
-------------------
  Unity -- unity.com
  Unity Netcode for GameObjects

FONTS
-----
  [CJK Pixel Fallback -- license pending confirmation]

SPECIAL THANKS
--------------
  All open-source and Creative Commons artists whose
  work made this project possible.

"No AI-generated assets were used in the creation of
 this game's art, music, or writing."
========================================================
```

---

## 5. Integration Priority Summary

| Priority | Action | Impact |
|---|---|---|
| **P0 -- Do Now** | Wire per-faction victory BGM (source files already exist) | Fixes blocking audio gap for win conditions |
| **P0 -- Do Now** | Reconcile Art source vs. Runtime character naming (medic vs. Fixer/Forensics) | Prevents character load failures |
| **P1 -- This Sprint** | Add VentOpen/VentClose VFX sheets to match existing SFX | Audio-visual sync for vent mechanic |
| **P1 -- This Sprint** | Wire ambient loops for Police map theme | Map 3 currently has no ambient audio |
| **P1 -- This Sprint** | Import selected Juhani Junkala SFX for alarm/door/weapon gaps | Fills 0-candidate event mapping gaps |
| **P1 -- This Sprint** | Add death/kill animation frames per profession | Core gameplay feedback |
| **P1 -- This Sprint** | Create meeting role reveal card sprites | Core meeting phase UX |
| **P1 -- This Sprint** | Create loading/transition screen art | Required for scene transitions |
| **P1 -- This Sprint** | Wire existing BGM source files (lobby, threat, vote) to AudioManager | Enriches audio variety at zero art cost |
| **P2 -- Backlog** | 4th map theme tileset | Replayability improvement |
| **P2 -- Backlog** | Animated neon/water tiles | Visual polish |
| **P2 -- Backlog** | Emote/reaction sprites | Social feature enhancement |
| **P2 -- Backlog** | Voice lines / callouts | Immersion; requires budget |
| **P2 -- Backlog** | 2.5D background dioramas from Quaternius kit | Experimental visual upgrade |

---

*End of document.*
