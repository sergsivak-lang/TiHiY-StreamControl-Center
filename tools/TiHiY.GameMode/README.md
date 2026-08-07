# TiHiY Game Mode

Portable Windows EXE for Star Citizen.

## STREAM MODE
Keeps OBS, Discord, SteelSeries/Sonar, NVIDIA and Stream Control running. Temporarily closes a conservative list of optional background apps and pauses DiagTrack, Windows Search and MapsBroker.

## GAME ONLY
Uses the STREAM MODE list plus OBS and common browsers/launchers.

## Restore
Every stopped service and restartable process is recorded to `%ProgramData%\TiHiY\GameMode\state.json` and can be restored from the UI. With auto-restore enabled, the app restores the state after `StarCitizen.exe` exits.

The app requires administrator rights because it temporarily controls Windows services.
