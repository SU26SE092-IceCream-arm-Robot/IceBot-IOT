# Session Note: UI checkpoint unaccepted and SignalR lifecycle fix

- **Date**: 2026-09-05 19:29 +07:00
- **Branch**: `refactorUI`
- **Commit pushed**: `b633a78933010709568de1552ca9dbd12833d27d`
- **Ingest**: Not run

## Outcome

Committed and pushed the broad H15-H19 UI patch as a technical checkpoint. The commit message explicitly records that the visual direction remains unaccepted: it is still too similar to the previous UI and does not satisfy the user's design expectations.

The next UI cycle must be desktop-first and must materially redesign task flow and information architecture. Do not treat mobile polish, shared component hardening, green tests or this commit as visual acceptance.

## SignalR

Fixed the pending-start cleanup race in dashboard, order and kiosk realtime hooks. Added pending-start unmount regression coverage. Targeted verification passed: 3 files and 13 tests, targeted ESLint, and TypeScript.

Live authenticated Hub verification was not possible without safe credentials. The fix is confirmed at code/test level only.

## Technical verification

- Full lint: PASS, 0 warnings.
- Architecture check: PASS, 453 files, 0 violations.
- Full unit/component tests: PASS, 97 files, 348 tests.
- Production build: PASS, 33 routes/pages.
- Public/Auth Playwright rerun: 24/24 PASS.
- Final staged diff check: PASS.

## Durable decisions

- Current UI is not accepted and must not be presented as complete.
- Mobile is not a priority for the next redesign.
- Raw backend role codes remain unchanged; only permission presentation is translated/mapped.
- Preserve RBAC, routes, API contracts, CRUD behavior and realtime semantics.
- `openapi.json` and `schema.graphql` remain local-only.

## Suggested next move

Choose one representative desktop operations workflow, create a concrete wireframe and interaction contract, and obtain explicit approval before another broad implementation pass. Production was suggested as a pilot but was not approved.

## Source of truth warning

Memory is advisory. Current repository files are the source of truth.
