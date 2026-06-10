# Gangland Undercover - Windows Steam PC Closure Runbook

> Date: 2026-06-10
> Priority: Steam/PC first. macOS and mobile remain later-stage flows.
> Goal: one command verifies the Windows candidate package path from tests through build, archive, zip, size gate, sha256, and report.

---

## 1. Closure Command

Run from the Unity project root:

```bash
bash build_steampc_windows_closure.sh --version 0.1.0-dev
```

Optional fast precheck when C# did not change:

```bash
bash build_steampc_windows_closure.sh --skip-tests --version 0.1.0-dev
```

The script checks whether `Windows Build Support (Mono)` is installed before it starts Unity tests or builds. If the module is missing, it writes a report and exits as `PARTIAL_BLOCKED` without launching Unity.

Expected full-pass output:

- `Builds/SteamPC-YYYYMMDD/StandaloneWindows64/GanglandUndercover.exe`
- `Builds/SteamPC-YYYYMMDD/StandaloneWindows64/SteamVisualArchive/MANIFEST.md`
- `Builds/SteamPC-YYYYMMDD/GanglandUndercover-SteamPC-Windows-YYYYMMDD-HHMMSS.zip`
- `Builds/SteamPC-YYYYMMDD/WINDOWS_CLOSURE_REPORT.md`

The zip must be at least 600 MiB and must be built from real project art/audio sources, not dummy filler.

---

## 2. Exit Codes

| Exit | Meaning | Action |
|------|---------|--------|
| `0` | Full closure passed: tests, Windows exe, visual archive, zip size, sha256. | Ready for Windows smoke and friend-test distribution. |
| `1` | Tests or packaging failed. | Read `WINDOWS_CLOSURE_REPORT.md` and the listed logs. |
| `2` | Windows build blocked, usually because Windows Build Support is missing. | Install Windows Build Support, then rerun the command. |

On the current Mac, exit `2` is expected until Unity 6000.4.9f1 has `Windows Build Support (Mono)` installed.

---

## 3. Current Machine Boundary

Current blocker:

- Unity editor: `/Applications/Unity/Hub/Editor/6000.4.9f1/Unity.app/Contents/MacOS/Unity`
- Installed playback engines currently show `WebGLSupport`, but not Windows Standalone support.
- Windows `.exe` cannot be produced on this machine until the module is installed.

Install in Unity Hub:

```text
Unity 6000.4.9f1 -> Add modules -> Windows Build Support (Mono)
```

After installation, rerun:

```bash
bash build_steampc_windows_closure.sh --version 0.1.0-dev
```

---

## 4. What The Script Verifies

The closure script performs:

1. Local Windows Build Support preflight.
2. Optional EditMode test run.
3. Windows x64 player build through `GanglandUndercover.Editor.BuildScript.Build`.
4. Windows-only `SteamVisualArchive` attachment beside the player.
5. Fallback archive export if Unity starts but the player build fails after preflight.
6. Zip packaging of `StandaloneWindows64`.
7. 600 MiB size gate.
8. SHA-256 generation.
9. Single Markdown report.

Important interpretation:

- `PASS` means a runnable Windows Steam candidate package exists.
- `PARTIAL_BLOCKED` means the runnable Windows exe is still missing; check the report to see whether this run produced a fresh archive or only found prior archive evidence.
- `PARTIAL_BLOCKED` must not be treated as a Steam-ready Windows build.

---

## 5. Next Windows Work After PASS

Once the Windows exe exists:

- Launch Windows build on an actual Windows PC.
- Verify anonymous login and Lobby/Relay status.
- Host creates a Relay room code.
- Client joins by room code.
- Both players Ready and enter match.
- Verify movement, task completion, chat, meeting/report, and disconnect recovery.
- Capture Steam screenshots at 1920x1080.
- Record result in the generated `WINDOWS_CLOSURE_REPORT.md` or a follow-up QA note.
