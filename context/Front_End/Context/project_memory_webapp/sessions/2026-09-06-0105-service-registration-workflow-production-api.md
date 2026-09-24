# Session: Service Registration workflow and Production API wiring

## Date
2026-09-06 01:05 +07:00

## Summary
Redesigned selected authenticated operational surfaces to reduce generated UI noise, fixed the Service Registration review workspace and connected the existing Production package installation workflow. Work was split into one UI commit and one API-wiring commit.

## What changed
- Removed non-essential PageHeader descriptions and several KPI strips that distracted from primary operational work.
- Rebuilt Service Registration detail into a wide review workspace with primary evidence, secondary audit disclosure and direct Duyệt/Từ chối actions.
- Fixed the dialog's inherited 384px desktop cap and corrected footer alignment, spacing and button sizing.
- Hid the `start-review` lifecycle transition behind the user's Duyệt/Từ chối intent while preserving backend audit and optimistic revision behavior.
- Added the Production Packages operations workspace using existing kiosk installation, install preview/action and retry APIs.

## Files touched
- `src/app/(dashboard)/inventory/page.tsx`
- `src/app/(dashboard)/kiosks/page.tsx`
- `src/app/(dashboard)/transactions/page.tsx`
- `src/components/features/catalog/products/products-management-view.tsx`
- `src/components/features/platform/service-registrations/service-registration-detail-drawer.tsx`
- `src/components/features/platform/service-registrations/service-registrations-view.tsx`
- `src/hooks/platform/use-service-registrations.ts`
- `src/components/shared/metric-strip.tsx`
- `src/components/shared/page-header.tsx`
- `src/components/features/production/production-operations-workspace.tsx`
- `src/components/features/production/production-workspace-view.tsx`

## Decisions made
- Design from the user's job and decision sequence; do not generate UI by mapping Swagger endpoints.
- Preserve all existing contracts and call `start-review` internally rather than making it a separate user step.
- Keep UI and API wiring in exactly two separate commits.
- Skip E2E/responsive suites for this pass; use lint, TypeScript and diff checks.

## Bugs / risks / unresolved issues
- Final footer alignment has code-level verification but still needs user visual confirmation.
- Production package operations need a live authenticated smoke test.
- Full authenticated-screen redesign remains incomplete and must continue workflow by workflow.

## Commands / checks run

```txt
npx eslint <service-registration files>: PASS
npx tsc --noEmit: PASS
git diff --check: PASS
git diff --cached --check: PASS for both commits
Tailwind cn merge inspection: PASS
```

## Commits
- `1a210a274fa05489debf4d46ddb4dc0b4fc59134 refactor(ui): prioritize operational workspaces`
- `526db0607a7fd943683c25fb8269ef1c509264d2 feat(production): connect package operations workflow`

## Next recommended steps
1. Visually verify the final Service Registration footer.
2. Smoke-test Submitted -> approve/reject and confirm revision handling after automatic `start-review`.
3. Smoke-test Production package installation and retry with authenticated data.
4. Resume the Group A workflow checklist page by page.

## Notes for future agents
- Auth and Landing Page are excluded from this redesign cycle unless scope changes.
- The backend API is fixed; connect existing APIs only.
- `openapi.json` and `schema.graphql` remain local-only.
- Do not treat passing checks or these commits as full visual acceptance.

## Source of truth warning
Memory is advisory. Current repository files are the source of truth.
