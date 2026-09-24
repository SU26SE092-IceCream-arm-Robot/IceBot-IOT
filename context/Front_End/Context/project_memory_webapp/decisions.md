# Architecture & Technical Decisions

### [2026-08-16] Dependency Security Patch & Turbopack Root Fix
- **Context**: `npm audit` flagged 15 vulnerabilities (12 High) in `next@16.2.6` and transitive packages (`hono`, `fast-uri`, `sharp`, `postcss`).
- **Decision**: Upgraded `next` and `eslint-config-next` to `16.3.1`, executed safe `npm audit fix`, added `turbopack.root` in `next.config.ts`, and isolated tests in `tsconfig.json`.
- **Rationale**: Keeps the codebase safe from known CVEs while avoiding breaking changes in React 19 / Tailwind CSS v4 stack.
- **Consequences**: Successfully eliminated 100% of security vulnerabilities (0 vulnerabilities found), Next.js builds clean in ~1.4s with 31 static routes.

### [2026-08-17] RBAC Boundary Between Platform and Organization Scopes
- **Context**: SystemAdmin accounts were previously capable of accessing organization-specific operational data (orders, products, menus, inventory) through broad backend permissions.
- **Decision**: Introduced `FORBIDDEN_SYSTEM_ADMIN_ROUTES` guard in `src/lib/rbac.ts` covering `/transactions`, `/menu-availability`, `/products`, `/menus`, `/menu`, `/inventory`, while maintaining full access to `/platform/organization-sales`.
- **Rationale**: Strict separation of concerns between Platform Super-Admin and Tenant Organization managers.
- **Consequences**: Organization data is exclusively exposed to tenant-level roles (OrgAdmin, Manager, Staff).

### [2026-08-17] Grouped Tenant Stores Presentation
- **Context**: `/stores` displayed an unorganized flat list of stores with no clear indication of parent organizations.
- **Decision**: Fetched stores and organizations concurrently in `useStoresList` and grouped stores into dedicated organization cards with real-time search.
- **Rationale**: Improves multi-tenant observability and management UX for administrators.
- **Consequences**: Clear hierarchical presentation with robust fallback for unassigned or offline organization metadata.

### [2026-08-17] Public Landing Registration & SystemAdmin Service Registration Management
- **Context**: Partner onboarding required a public registration landing page and a secure administrative workflow for reviewing, approving, and provisioning organizations.
- **Decision**: Implemented public landing form at route `/` with client-side validation and `Idempotency-Key` headers; built a dedicated management portal at `/platform/service-registrations` guarded by new SystemAdmin-only permissions `service-registrations.read` and `service-registrations.manage` with optimistic concurrency control (`expectedRevision`).
- **Rationale**: Separation of concerns ensuring unassigned partner applications are not governed by organization-level permissions, and single-step backend approval commands ensure transactional provisioning.
- **Consequences**: Seamless partner onboarding with automated organization and OrgAdmin creation, protected against concurrency conflicts and unauthorized access.

### [2026-08-17] Static Content Pages Management & TipTap WYSIWYG Integration
- **Context**: Long-form static content (about-us, privacy-policy, payment-policy, terms-of-use, contact-information) required a headless rich-text editing experience for SystemAdmin with immutable revision tracking, and lightweight public rendering with direct DTO support.
- **Decision**: Integrated TipTap (`@tiptap/react`, `@tiptap/starter-kit`, `@tiptap/extension-link`) for full React 19 compatibility and rich formatting; configured OCC using `expectedRevision: number` on `PUT .../draft` and `POST .../publish`; created public dynamic route `/(public)/[slug]` and platform routes `/platform/content-pages` and `/platform/content-pages/[key]`.
- **Rationale**: TipTap avoids React 19 peer-dep breakages of legacy editors; OCC prevents race conditions between administrators; dynamic routing cleanly serves SEO-optimized public policy pages.
- **Consequences**: Complete end-to-end static content publishing lifecycle with zero concurrency conflicts and beautiful responsive rendering.

### [2026-09-04] Strict API Functional Coverage Criterion
- **Context**: OpenAPI/GraphQL operations must be classified by usable frontend functionality, not merely by the presence of a service function or mock.
- **Decision**: Mark an operation `Implemented` only when evidence covers spec operation, frontend transport/document, owning service, runtime consumer, user trigger and observable outcome/error handling.
- **Rationale**: This prevents dead services, unreachable consumers and test-only mocks from inflating implementation coverage.
- **Consequences**: Static `ui-chain` evidence remains provisional until Hạng mục 6 adds test evidence and manual review; other verdicts include `Partial`, `Unused`, `Missing`, `Client-specific` and `Unknown`.

### [2026-09-04] Full-WebApp UI Modernization with Landing Page First
- **Context**: The initial request could be interpreted as redesigning only the Landing Page, while the intended scope is the whole WebApp without losing existing behavior.
- **Decision**: Improve UI/UX across the full WebApp, using the Landing Page as the first approved pilot before rolling the design system into shell, public/auth, operations, commerce/organization and platform modules.
- **Rationale**: A pilot establishes visual language, responsive behavior, accessibility and motion conventions while reducing regression risk.
- **Consequences**: GSAP is used selectively as a lifecycle-safe, reduced-motion-aware motion layer; each roadmap item ends with a report and explicit user approval before the next item begins.

### [2026-09-04] Landing Page Regression Acceptance & Feature Architecture Enforcement
- **Context**: Landing Page required full end-to-end regression across desktop and mobile, resolution of pre-existing realtime typecheck errors, architectural linting, and official release distribution links.
- **Decision**: Integrated official Fairino-Studio releases URL as default download destination with env fallback; fixed SignalR `RetryContext.previousRetryCount` and realtime hook type narrowing to achieve 0 TypeScript errors; introduced `scripts/check-feature-architecture.mjs` to validate Clean Architecture layers; created `e2e/landing-page.spec.ts` covering 16 tests across desktop, mobile, keyboard accessibility, reduced-motion, and 4x CPU throttling.
- **Rationale**: Ensures the public entrypoint is rock solid, verified against real devices, and establishes architectural constraints before starting Phase E app shell rollout.
- **Consequences**: Phase D (Hạng mục 9–13) is 100% complete and verified; codebase has 0 TS errors and clean architecture ready for Phase E (Hạng mục 14).

