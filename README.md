# TiHiY StreamControl Center

Windows WPF application for multichat, OBS audio control, Donatello donations, Twitch/YouTube integrations, Discord notifications, overlays and music.

## Current development status

This repository is the working source base for the Cyber Amber rebuild. The current UI is **not yet accepted as visually matching** `docs/Cyber-Amber-TARGET.png`.

- Target design: `docs/Cyber-Amber-TARGET.png`
- Current failed visual reference: `docs/v0.9.0-CURRENT-FAILED.png`
- Visual changes must preserve existing functional services and event handlers.
- Do not call a release complete until Windows CI succeeds and a real local screenshot is compared with the target.

## Requirements

- Windows 10/11 x64
- .NET 9 SDK x64
- OBS Studio with OBS WebSocket 5.x for OBS audio integration

## Local build

Run:

```bat
BUILD-AND-RUN.bat
```

The published application is created in:

```text
Release\TiHiY.StreamControlCenter.exe
```

## Local verification and screenshot

Run:

```bat
VERIFY-LOCAL.bat
```

It builds the application, launches it, checks that the process stays alive, and saves a real screenshot to:

```text
Verification\TiHiY-StreamControl-Center.png
```

## GitHub Actions

Open **Actions → Build Windows Portable → Run workflow**.

After a successful run, download the artifact:

```text
TiHiY-StreamControl-Center-win-x64
```

The workflow performs restore, Release build, self-contained publish, and an 8-second startup smoke test on `windows-latest`.

## Security

Do not commit API tokens, Discord bot tokens, OAuth client secrets, OBS passwords, local settings, `Release`, `bin`, `obj`, or `BuildLogs`. Runtime credentials are expected to be stored through Windows Credential Manager.

## Safe GitHub upload

Run `UPLOAD-TO-GITHUB.bat`. It clones the existing repository, creates `agent/cyber-amber-rebuild`, replaces that branch contents with this package, pushes it, and opens the Pull Request page. It does not force-push `main`.
