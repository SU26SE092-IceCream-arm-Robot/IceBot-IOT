# Session: H15-H19 audit correction

## Date
2026-09-05 17:56 +07:00

## Summary
Implemented H15 Public/Auth and broad shared UI hardening, then audited the result after the user found that many roadmap items were marked complete without page-specific changes. The audit confirmed the concern: H16-H19 are not complete in the intended per-page/task-flow sense. The previous Goal completion was incorrect and must not be used as delivery evidence.

## What changed during the session
- Built a shared enterprise auth shell and password field for all four authentication routes.
- Improved public content retry, focus/skip behavior and root-qualified Landing anchors while preserving API-rendered `bodyHtml`.
- Applied global/shared control touch, focus, table, dialog and responsive changes.
- Added Public/Auth responsive E2E and ran the full automated verification suite successfully.
- Performed a read-only diff audit and diagnosed the mismatch between roadmap checkboxes and actual source changes.
- Diagnosed a Dashboard SignalR start/stop lifecycle race from the user's terminal log.
- No code correction for Roles or SignalR was implemented after diagnosis; these are first actions for the next session.

## Files touched
The live worktree is authoritative. Main valid implementation groups:

- `src/app/(auth)/*`
- `src/components/features/identity/auth/*`
- `src/components/features/content-pages/public-content-page-view.tsx`
- `src/components/features/service-registration/public-header.tsx`
- `src/app/globals.css`
- `src/app/(dashboard)/layout.tsx`
- selected `src/components/ui/*` primitives
- `e2e/public-auth.spec.ts`
- `playwright.config.ts`

Known formatting-only churn:

- `src/components/ui/card.tsx`
- `src/components/ui/label.tsx`
- `src/components/ui/scroll-area.tsx`
- `src/components/ui/separator.tsx`
- `src/components/ui/sonner.tsx`
- `src/components/ui/textarea.tsx` status-only/no patch content

## Decisions made
- Reopen H16-H19. Treat H15 as implemented but pending user visual acceptance.
- Never treat Goal status, passing tests or a shared primitive diff as proof that named pages were redesigned.
- Require page/feature-specific audit, Patch Contract, observable implementation and visual/task-flow acceptance before checking a roadmap item.
- Preserve all backend role/policy codes and RBAC behavior; translate them only in a centralized presentation layer.
- Do not commit or push the current patch until formatting churn is cleaned and the user accepts the result.
- Do not ingest this session.

## Bugs / risks / unresolved issues
- `/roles` is not understandable for operators: raw role/policy codes, English backend descriptions, duplicate label maps and ambiguous scope/system columns.
- Role semantics must be consistent across Roles, Users, account dialogs, profile and topbar.
- `use-dashboard-realtime` can call `stop()` while `start()` is pending, producing `Failed to start the HttpConnection before stop() was called.`
- Shared 44px controls and larger table padding may conflict with the accepted operations-dense direction.
- Authenticated visual automation remains unavailable without a safe test account/storage state.
- `docs/API-COVERAGE-AND-GSAP-ROADMAP.md` currently contains stale `[x]` states for H16-H19.

## Commands / checks run

```txt
git status --short / git diff --stat / git diff --numstat
34 tracked diff files, 4 untracked files

Dashboard route inventory versus git diff
30 page.tsx files, only transactions/page.tsx directly changed

npm test
95 files, 344 tests passed

npx tsc --noEmit
PASS

npm run check:architecture
449 files, 0 violations

npm run build
PASS, 33 routes/pages

Playwright public desktop/mobile/tablet/laptop --workers=1
40 tests passed

npm test -- --run src/hooks/realtime/use-dashboard-realtime.test.tsx
3 tests passed; pending-start cleanup case is missing
```

## Next recommended steps
1. Inspect current diff and remove only formatting churn; do not reset the whole worktree.
2. Correct roadmap statuses to H15 pending acceptance, H16 partial, H17 partial, H18 pending, H19 partial.
3. Draft and obtain approval for a concrete `/roles` semantic redesign contract.
4. Implement centralized role presentation without changing role codes, policy codes, API requests or authorization logic.
5. Fix the Dashboard SignalR cleanup race and add the missing lifecycle regression test.
6. Create contracts and implement every remaining H16-H19 route/module, with visual evidence for each.

## Notes for future agents
- The user explicitly asked that this correction be recorded clearly so the next session fixes all remaining work, not merely the two visible issues.
- Do not say H1-H19 are complete. H14 is accepted; H15 is implemented/pending acceptance; H16-H19 require reopening.
- Current repository files override this note. Preserve user changes and local-only API snapshots.
- No ingest was requested or run.
