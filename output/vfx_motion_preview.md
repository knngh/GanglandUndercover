# Gangland Undercover VFX Motion Preview

## Status

READY

## Contact Sheet

output/vfx_contact_sheet.png

## Gameplay Context Preview

output/vfx_gameplay_context_sheet.png

## Motion Profiles

| Effect | Runtime Use | Frames | Size | FPS | Duration | Layer | Mode |
|---|---|---:|---:|---:|---:|---:|---|
| blackout | Global sabotage blackout overlay | 12/12 | 96x96 | 6 | 2s | 500 | Loop |
| comms_jam | Communication sabotage glitch overlay | 8/8 | 64x64 | 14 | 0.57s | 502 | Loop |
| door_lock | Lockdown door-state overlay | 6/6 | 48x48 | 10 | 0.6s | 501 | Loop |
| emergency_light | Blackout emergency-light pulse | 8/8 | 48x48 | 12 | 0.67s | 505 | Loop |
| evidence_leak | Evidence leak clue pulse | 12/12 | 48x48 | 9 | 1.33s | 499 | Loop |
| hit | Instant hit impact feedback | 4/4 | 32x32 | 18 | 0.22s | 506 | OneShot |
| kill | Kill blood impact and body drop accent | 10/10 | 128x128 | 15 | 0.67s | 504 | OneShot |
| patrol_alert | Patrol alert warning overlay | 4/4 | 64x64 | 6 | 0.67s | 503 | Loop |

## Polish Priority

| Priority | Effect | Focus | First Adjustment |
|---|---|---|---|
| P2 | blackout | map readability and emergency-light contrast | Use a full-field dim pass with readable player silhouettes and cyan power arcs. |
| P2 | comms_jam | glitch cadence and screen noise density | Use deterministic sparse bands and noise so interference does not hide task prompts. |
| P2 | door_lock | door icon silhouette and red warning edge | Use a compact lock plate plus warning edge instead of only a full-screen X. |
| P3 | emergency_light | secondary pulse contrast | Tune red pulse so it supports blackout without becoming a combat cue. |
| P1 | evidence_leak | evidence pulse visibility over floor props | Check the first and brightest frames against busy evidence rooms. |
| P1 | hit | short one-shot readability at character scale | Confirm the 32px flash is visible over every character profession skin. |
| P1 | kill | top-layer combat scale and opacity | Check scale against corpse marker and local player silhouette. |
| P2 | patrol_alert | warning cadence and color separation | Use amber patrol-search iconography separated from red lockdown and emergency cues. |

## Issues

- None

## Next Checks

1. Inspect row order against the motion table: blackout, comms_jam, door_lock, emergency_light, evidence_leak, hit, kill, patrol_alert.
2. Start with P2 rows, then verify blackout, comms_jam, door_lock, and patrol_alert against busy gameplay backgrounds.
3. Compare the gameplay context preview against a live scene capture before replacing another asset batch.
