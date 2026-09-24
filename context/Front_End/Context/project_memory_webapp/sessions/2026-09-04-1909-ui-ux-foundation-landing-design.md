# Session: UI/UX Foundation and Landing Design

## Date
2026-09-04 19:09 +07:00

## Summary

Revalidated API coverage Hạng mục 7, then completed the supervised UI/UX roadmap through Hạng mục 10. The user clarified that UI proposals must be derived from actual IceBot functions and UX goals rather than the current/old visual design. The resulting direction is a professional `Operational Intelligence` system and a fully specified Landing Page redesign/motion plan. No application source was changed.

## What changed

- Rechecked every API matrix total and corrected stale Hạng mục 7 status text.
- Completed Hạng mục 8 as a function-driven UI/UX direction covering real product jobs, operating risk and responsive strategy.
- Completed Hạng mục 9 as a Landing Page functional, accessibility, responsive, form/API, performance and test audit.
- Completed Hạng mục 10 with new information architecture, visual language, responsive layouts, registration UX, GSAP motion, reduced-motion and acceptance criteria.
- Updated the roadmap to show Hạng mục 1-10 complete and Hạng mục 11 pending approval.

## Files touched

- `docs/API-COVERAGE-AND-GSAP-ROADMAP.md`
- `docs/API-FUNCTIONAL-CHAIN-MATRIX.md`
- `docs/API-UI-UX-FOUNDATION-AUDIT.md`
- `docs/LANDING-PAGE-FUNCTIONAL-AUDIT.md`
- `docs/LANDING-PAGE-DESIGN-AND-MOTION-PLAN.md`
- `.project-memory/active-context.md`
- `.project-memory/handoff.md`
- `.project-memory/sessions/2026-09-04-1909-ui-ux-foundation-landing-design.md`

## Decisions made

- Use actual system functions and user outcomes as the design input; the current UI is only a behavior-preservation constraint.
- Adopt `Operational Intelligence` rather than generic SaaS, playful retail or neon sci-fi styling.
- Keep public storytelling spacious and authenticated operations dense/evidence-led.
- Never fabricate product metrics, customer evidence, deployment promises or third-party relationships.
- Preserve the public registration API contract and make consent, validation, idempotency and success evidence more trustworthy.
- Use GSAP only where sequencing explains the system: short hero timeline, bounded reveals and selective workflow storytelling.
- Require reduced-motion, scoped selectors, `useGSAP`, `gsap.matchMedia()` and cleanup for all GSAP work.
- Hạng mục 11 must define the Patch Contract before any source implementation.

## Bugs / risks / unresolved issues

- Public registration retry currently creates a new idempotency key after an uncertain request outcome.
- Validation/error/success states require field association, focus and live announcement improvements.
- Consent text is not linked to policy pages, and the fallback policy revision ID may drift.
- Public `/` currently runs authenticated-app providers and listeners.
- Current Landing animation has no reduced-motion handling; narrow-screen ecosystem layout is fragile.
- Business inputs remain unknown: response-time/channel promise, deployment criteria, official contacts, approved proof points and imagery.
- Production build TypeScript validation reports existing realtime type errors in `use-kiosk-operations-realtime.ts` and `signalr-client.ts`.
- Existing lint/architecture/E2E/RAG environment limitations remain documented in active context.

## Commands / checks run

```txt
Direct OpenAPI/GraphQL/matrix recount
PASS: 401 REST, 11 GraphQL, unique matrix rows and matching verdict totals

npm run test
PASS: 91 test files, 334 tests

npx vitest run src/components/features/service-registration/registration-form.test.tsx src/lib/services/service-registrations.test.ts
PASS: 2 test files, 12 tests

npm run build
PARTIAL: optimized compilation passed; TypeScript failed on four existing realtime typing errors outside Landing Page

Documentation assertions
PASS: Hạng mục 8, 9 and 10 reports contain required contracts and roadmap statuses
```

## Next recommended steps

1. Ask for explicit approval to begin Hạng mục 11.
2. Produce a file-scoped Patch Contract for six proposed slices; do not edit application source during that item.
3. Obtain business content decisions where available, but use neutral language rather than inventing missing claims.
4. After the Patch Contract is approved, implement one slice at a time and wait for approval after each report.

## Notes for future agents

- Read `docs/LANDING-PAGE-FUNCTIONAL-AUDIT.md` and `docs/LANDING-PAGE-DESIGN-AND-MOTION-PLAN.md` first.
- Do not return to the old UI as a style reference.
- Preserve user-owned staged API snapshots.
- Documentation and project-memory Markdown files are ignored locally; their absence from `git status` does not mean they were not updated.
- Memory is advisory; verify current code before implementation.
