# Session: Public Docs and Dashboard fixes

## Date
2026-09-06 13:15 +07:00

## Summary
Fixed the OrgAdmin Dashboard GraphQL failure, completed dialog/Kiosk resilience patches and built a public multi-route IceBot documentation portal with an AI chat preview ready for a future adapter.

## What changed
- Fixed the missing GraphQL closing brace in the conditional Dashboard order selection.
- Prevented shared dialog fields and Staff scope controls from overflowing.
- Handled missing Kiosk configuration/schema version values safely.
- Added six public Docs URLs with search, navigation, TOC, code examples and responsive layout.
- Added a bubble-style Docs AI chat preview with an optional asynchronous adapter.
- Linked Docs from the public Landing header and footer.

## Files touched
- `src/lib/services/dashboard/overview.ts`
- `src/lib/services/dashboard/overview.test.ts`
- `src/components/features/identity/staff/staff-workforce-view.tsx`
- `src/components/ui/dialog.tsx`
- `src/components/ui/select.tsx`
- `src/components/ui/textarea.tsx`
- `src/components/features/kiosks/kiosk-detail-view.tsx`
- `src/components/features/kiosks/kiosk-detail-view.test.tsx`
- `src/lib/adapters/kiosk-fleet.ts`
- `src/lib/adapters/kiosk-fleet.test.ts`
- `src/types/kiosks/detail.ts`
- `src/types/kiosks/management.ts`
- `src/app/(docs)/docs/`
- `src/components/features/docs/`
- `src/lib/docs/`
- `src/components/features/service-registration/public-header.tsx`
- `src/components/features/service-registration/public-footer.tsx`
- `e2e/public-auth-docs.spec.ts`

## Decisions made
- Keep Docs public by isolating it in `(docs)` without auth/session/RBAC dependencies.
- Use a calm, minimal framework-documentation layout rather than an AI-template visual direction.
- Keep Docs content typed and centralized.
- Expose chat integration through `DocsChatAdapter`; do not invent or call an AI backend.
- Validate UI behavior at 375, 768, 1024 and 1440 px before delivery.

## Bugs / risks / unresolved issues
- Docs prose is not yet a complete backend API reference.
- Real chat requires decisions for API transport, streaming, auth, rate limiting and privacy.
- Authenticated Production and Service Registration workflows still need live-data smoke tests.
- Full authenticated redesign remains incomplete.

## Commands / checks run

```txt
npx vitest run src/lib/services/dashboard/overview.test.ts: 4/4 PASS
npx vitest run Kiosk detail and adapter tests: 7/7 PASS
npx vitest run Docs content and chat tests: 5/5 PASS
npx playwright test e2e/public-auth-docs.spec.ts across four public projects: 12/12 PASS
npx tsc --noEmit: PASS
targeted npx eslint: PASS
npm run check:architecture: PASS, 464 files, 0 violations
npm run build: PASS, 39 pages
git diff --check: PASS
```

## Commits
- `4536ecd4fbc885a25cc33d8484f9779b3b10b4ad fix(dashboard): close order overview query`
- `e3e4911 fix(ui): prevent dialog field overflow`
- `95cf2fda18b4a00d327624f8b37db5ab77dc10ab fix(kiosks): handle missing config versions`
- `b4455acfcfd463930ac9d2515be5d5794702b9d5 feat(docs): add public documentation portal`

## Next recommended steps
- Connect a reviewed AI API through `DocsChatAdapter` when its contract is available.
- Expand Docs only from confirmed current contracts.
- Resume authenticated live smoke tests and the page-by-page redesign roadmap.

## Notes for future agents
- Do not move Docs under the authenticated dashboard layout.
- The chat currently sends nothing externally.
- Memory is advisory; verify all commit and runtime facts against the repository.
