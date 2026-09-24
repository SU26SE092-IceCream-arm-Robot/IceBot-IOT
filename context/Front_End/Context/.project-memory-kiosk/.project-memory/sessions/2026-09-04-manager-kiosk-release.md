# Session: Manager Kiosk Release

## Date
2026-09-04 12:28

## Summary
Implemented, verified, committed, and released Manager-driven kiosk setup for the Windows kiosk app.

## What changed
- Replaced compile-time kiosk binding with Manager login and secure session persistence.
- Added kiosk selection for stores with multiple kiosks.
- Removed hardcoded kiosk ID configuration from build and installer flows.
- Created documentation and backend confirmation notes.
- Published Windows MSI GitHub Release `v1.0.1`.

## Files touched
- `lib/features/setup/`
- `lib/main.dart`
- `lib/config/app_config.dart`
- `lib/features/kiosk/presentation/screens/menu_screen.dart`
- `scripts/`, `installer/`, `README.md`, and E2E documentation

## Decisions made
- Manager can choose any kiosk in their single managed store.
- Multiple kiosk results are a setup selection state, not an error.
- Release artifacts are distributed as MSI through GitHub Releases, not committed to Git.

## Bugs / risks / unresolved issues
- Confirm API schemas and device-token policy with backend.
- Perform physical kiosk E2E validation for multi-kiosk selection.

## Commands / checks run

```txt
flutter analyze - passed
flutter test - 80 tests passed
.\scripts\build-production.ps1 - passed
.\installer\build-msi.ps1 -ApiBaseUrl https://api.icebot.io.vn -Version 1.0.1 - passed
```

## Next recommended steps
- Test the MSI on a clean Windows kiosk machine.
- Test login and selection with a store that has multiple kiosk records.
- Resolve backend contract questions in `BACKEND_KIOSK_SETUP_CONFIRMATION.md`.

## Notes for future agents
- GitHub Release `v1.0.1` contains `IceBot_Kiosk_1.0.1.msi`.
- Keep build output and release assets out of Git.
