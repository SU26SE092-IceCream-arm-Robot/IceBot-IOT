# Active Context

## Last updated
2026-09-04 12:28

## Current focus
Manager-based kiosk setup and Windows release workflow are complete.

## Current status
- Manager login resolves a store and allows selecting a kiosk when the store has multiple kiosks.
- Session and selected kiosk are persisted securely; logout resets the device to setup state.
- Source commits `83a6dc3` and `7545340` are pushed to `origin/main`.
- GitHub Release `v1.0.1` publishes `IceBot_Kiosk_1.0.1.msi`.

## Important decisions
- One Manager manages one store and may select any kiosk within that store.
- Use `roles[].kioskId` when supplied; otherwise list kiosks with `storeId`.
- One returned kiosk is auto-selected; multiple kiosks require a touch-friendly Manager selection screen.

## Open issues / risks
- Confirm backend permission and response schema for the kiosk list endpoint in OpenAPI.
- Confirm whether Manager tokens should eventually be exchanged for a device credential.
- Runtime API migration remains pending backend contract confirmation.

## Next steps
- Run a real-device E2E test with a store containing multiple kiosks.
- Verify MSI installation, shortcuts, Manager login, selection, restart, and logout reset.

## Source of truth warning
Memory is advisory. Current repository files are the source of truth.
