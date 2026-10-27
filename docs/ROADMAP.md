# DealerSync development roadmap

I started DealerSync after encountering inventory reconciliation problems at Ottawa Goodtime Centre. It is also one of my Computer Science portfolio projects. I want to build a useful application and understand its code and architecture well enough to maintain and explain it.

The roadmap contains **15 milestones**, grouped into six phases. Version numbers describe planned scope, not a delivery schedule. Later features remain planned until their implementation and behavior have been verified.

## Current position

Repository inspection on October 18, 2026 found:

- Branch: `feature/multi-supplier-inventory`, with a clean working tree before this documentation task and no ahead/behind count relative to its local origin reference. No remote fetch was performed.
- Latest commit: `c5c1fda`, adding frontend mapping verification actions.
- Local tags: `v0.1.0` and `v0.2.0`. There is no local `v0.3.0` tag. Tags do not establish deployment status.
- The v0.3 dashboard, multi-supplier backend, review diagnostics, CSV diagnostics, and mapping verification are implemented.
- The History and Settings routes are placeholders. The dashboard already displays recent reconciliation summaries from the API.
- No automated frontend/browser test runner or GitHub Actions workflow was found in the inspected repository.

Earlier development checks recorded 104 passing backend tests, successful frontend build/lint, and manual checks for aggregation, blocked updates, approved zero quantities, diagnostics CSV output, and verification followed by reconciliation. These are historical results, not tests rerun for this documentation task.

Status convention: checked items have supporting repository implementation. Unchecked items are not complete or have not been verified. A feature being checked does not imply the whole version is released, production-ready, or free of defects. Release acceptance requires tests and review as well as implementation.

## Phase 1: Reconciliation foundation

### v0.1.0 — CSV Reconciliation MVP

Status: implemented; local version tag exists.

Purpose: compare Lightspeed EVO and Shopify exports without manually reconciling every inventory row.

- [x] Lightspeed EVO CSV imports.
- [x] Shopify CSV imports.
- [x] Exact and normalized SKU matching.
- [x] Inventory quantity comparison.
- [x] Duplicate and invalid quantity protection.
- [x] Shopify inventory update CSV generation through the CLI/exporter.
- [x] Automated backend tests.

Dependencies: compatible inventory headers and explicit matching and quantity rules.

Expected outcome: a repeatable CSV comparison that produces reviewable inventory changes and excludes unsafe matches. This is a file-based workflow, not a live Shopify integration.

Evidence: `backend/DealerSync.Runner/Program.cs`, Core matching services, Infrastructure importers/exporters, and importer/matching/exporter tests in `backend/DealerSync.Tests`.

### v0.2.0 — Backend API and Persistence

Status: implemented; local version tag exists. The README still has an unchecked final-release item. Confirm what that item covers before marking it complete.

Purpose: make reconciliation accessible over HTTP and retain verified SKU relationships between runs.

- [x] ASP.NET Core API.
- [x] PostgreSQL database integration.
- [x] Entity Framework Core migrations.
- [x] Persistent SKU mappings.
- [x] Manual mapping verification through the API.
- [x] Saved reconciliation history summaries and retrieval endpoints.
- [x] Diagnostic JSON/CSV exports.
- [x] Automated integration tests using Testcontainers.

Dependencies: v0.1 matching/export behavior and a configured PostgreSQL database.

Expected outcome: clients can reconcile uploads, manage mappings, and retrieve saved run summaries. Individual historical match records are not persisted; advanced history belongs to v1.2.0.

Evidence: `backend/DealerSync.Api/Endpoints`, `backend/DealerSync.Infrastructure/Persistence`, and `backend/DealerSync.Tests/Api`.

## Phase 2: Dashboard and release reliability

### v0.3.0 — React Dashboard and Multi-Supplier Support

Status: release-candidate acceptance passed for `9fb74ea`; final documentation review, merge, and tag approval remain open.

Purpose: let staff reconcile files and verify supplier mappings without manual API calls, including products supplied by more than one Lightspeed part number.

- [x] React/TypeScript dashboard.
- [x] Inventory reconciliation interface and summaries.
- [x] CSV file selection and uploads.
- [x] Searchable results and filtering.
- [x] SKU mapping list and creation.
- [x] Supplier mapping verification from the UI.
- [x] Multi-supplier quantity aggregation.
- [x] Supplier contribution diagnostics in results and diagnostics CSVs.
- [x] Mapping-aware approved Shopify inventory download with preserved variant identity and current-stock validation fields.
- [x] Inventory safety rules for unverified/missing suppliers, invalid quantities, ambiguous data, and approved-update separation.
- [x] Complete scoped local end-to-end acceptance: fresh clone, synthetic browser workflow, 24 automated browser tests, populated upgrade, and isolated backup/restore drill.
- [ ] Finish release preparation, documentation, branch review/merge, and an approved `v0.3.0` tag.

Dependencies: v0.2 API/persistence, the `AllowMultipleSuppliersPerShopifySku` migration, and consistent API/frontend contracts. The developer reported successfully applying the migration locally; other environments must be checked separately.

Expected outcome: a staff-facing local reconciliation workflow showing current Shopify quantity, trusted supplier contributions, calculated combined quantity, and a separate backend-approved quantity. A missing approval is blank; an approved zero remains `0`.

Evidence: `frontend/src/features/dashboard`, `frontend/src/features/reconciliation`, `frontend/src/features/mappings`, `frontend/src/features/inventory`, `frontend/src/lib/csvExport.ts`, API DTOs, Core matching/diagnostic services, and backend regression tests. Related commits are `e17b4c6`, `296300f`, `36270c3`, and `c5c1fda`.

#### Playwright Step 1

The first browser test now runs one exact-match reconciliation against a separate API and disposable PostgreSQL container. It verifies current quantity `2`, calculated quantity `7`, and approved quantity `7`. The smoke test, its TypeScript check, frontend build/lint, and all 104 existing backend tests passed during implementation. SIGTERM cleanup was also checked, and no E2E runner containers remained afterward.

Step 1 established the environment and exact-match smoke test. Steps 2A, 2B, and 3A cover supplier safety, quantity validation, diagnostic downloads, and mapping creation/verification workflows as described below. Later milestones below record broader UI behavior and completed CI. See [TESTING.md](TESTING.md) for local commands and isolation details. The baseline inventory above describes the repository before this infrastructure was added.

#### Playwright Step 2A: Multi-supplier inventory safety

Three additional browser tests use the real frontend, API, and disposable PostgreSQL database. They create mappings through the existing API, verify the intended suppliers, and reconcile synthetic CSVs through the browser.

- Two verified suppliers with quantities 3 and 4 produce combined and approved quantities of 7 and one eligible update.
- An unverified second supplier is excluded from the combined quantity. The verified contribution is 3, the approved quantity is null, and `UnverifiedSupplier` blocks updates.
- A missing verified supplier leaves the available contribution at 3, with a null approved quantity and `MissingSupplierInventory` blocking updates.

Each scenario checks the actual API contract, visible supplier contributions and quantities, and inclusion or exclusion under Approved Updates. The missing-supplier response flags the reason but does not name the missing part; the test verifies the omitted supplier's persisted mapping as part of setup.

Per-test SKU namespaces keep mappings independent without reset SQL. The existing runner still owns the separate API, frontend, and temporary database. All four browser tests, 104 existing backend tests, the frontend production build, lint, and test TypeScript checks passed during this step. The supplier scenarios also passed in reverse order against a fresh isolated stack. The full end-to-end acceptance milestone remains open. See [TESTING.md](TESTING.md) for fixtures, expected values, and commands.

#### Playwright Step 2B: Quantity validation and diagnostics

Eight additional browser tests cover positive and negative supplier stock together, approved zero, four representative malformed inputs, fully verified aggregate overflow, and CSV text/formula protection. The three Step 2A supplier tests also check actual frontend downloads for calculated quantities, blank blocked approvals, statuses, reasons, and contributions. A test-only CSV parser checks the downloaded file independently of the exporter.

The negative-stock case returns combined 5 and null approval for contributions of 7 and -2. Approved zero remains 0 in the API, table, Approved Updates filter, and export. Malformed quantities return HTTP 400. Fully verified quantities of 2147483647 and 1 also return HTTP 400 rather than generating an unsafe update; existing xUnit coverage separately handles overflow in a product blocked by an unverified mapping.

The current overflow message describes conflicting data, the frontend displays errors as raw JSON text, and negative stock has `InvalidQuantity` with an empty mapping-reason array. These behaviors are documented in [TESTING.md](TESTING.md); production validation and business logic are unchanged. Full acceptance and release preparation remain open.

Validation passed all 12 browser tests and 104 existing backend tests, the frontend production build with environment-file loading disabled, lint, test TypeScript checks, and `git diff --check`. The runner removed its owned container; a separate Docker check found no remaining E2E containers. These results establish the tested cases, not completion of v0.3 acceptance.

#### Playwright Step 3A: SKU mapping workflows

Two additional browser tests create and verify mappings through React controls, independently check persisted state, and reconcile before and after verification. With supplier quantities of 3 and 4 and Shopify stock of 2, the pending supplier leaves combined inventory at 3 and blocks approval. Verifying it clears stored results; a new upload produces combined and approved quantities of 7 and one eligible update.

The regression test reproduced a creation-invalidation defect before the fix: 13 tests passed, but the Inventory page retained an old approval after a new unverified mapping was saved. `MappingsPage` now clears the shared reconciliation result after successful creation, then refreshes the mappings list. A duplicate creation returning HTTP 409 preserves the existing result and download. Backend approval rules and verification's existing invalidation remain unchanged.

The tests reuse existing CSVs, unique test identifiers, and the disposable database runner. Sidebar navigation and uploads on the current page preserve React state so page reloads cannot conceal stale results. No dependencies were added. Cross-tab invalidation and in-flight reconciliation races remain outside this coverage. Full v0.3 acceptance remains open.

Validation passed 14 browser tests, all 104 existing backend tests, the frontend production build with environment-file loading disabled, lint, test TypeScript checks, and `git diff --check`. Cleanup ran after the failing and passing suites; a separate check found no E2E containers or test servers remaining. No Docker volumes were removed.

#### Release milestone 1: Safe Shopify inventory export

The importer retains exact source handles, option values/names, SKU, title, and location separately from normalized
matching fields. Approved updates carry that identity without changing supplier trust or quantity rules. The API generates
the complete All states update CSV from the same mapping-aware reconciliation result returned to React. The web page
offers separate approved-update and diagnostics downloads; table filters cannot change approval or download scope.

Export validation blocks the whole file for incomplete or ambiguous source identity, missing applicable option values,
formula-like text, inconsistent source data, or excessive size. No identifiers are fabricated or apostrophe-prefixed in
machine-import files. Backend diagnostics escape formula-like text. Legacy CLI output remains standalone and does not
load PostgreSQL mappings; it is explicitly excluded from the mapping-aware web workflow.

Five new browser tests cover mixed approved/blocked products, default and multi-option variants, zero, current/new stock,
actual downloads, invalid identity, and late-response invalidation. The browser cancels/discards pending reconciliation
responses on navigation so they cannot restore an obsolete export. Existing invalidation tests also check the new
download. No database migrations, dependencies, external Shopify calls, or live-store imports are part of this milestone.
Contract verification follows Shopify's published schema and synthetic examples; a live import has not been validated.

Four additional API regression cases confirm that distinct variants sharing a SKU/location are already rejected,
including case and surrounding-whitespace differences. No production safeguard change was needed.

Validation passed 19 Playwright tests, 143 backend tests, the backend build, frontend production build, lint, test
TypeScript checks, and whitespace review. The existing 14 browser and 104 backend tests remain included. The test
container and server processes were cleaned up without removing volumes. These checks do not mark v0.3.0 released or
finish the remaining setup, upgrade, CI, and release-candidate gates.

#### Release milestone 2: Reliability and safety stabilization

Status: complete, committed and pushed as `2f093ca`.

Five additional browser tests cover corrected uploads, HTTP/network failure recovery, loading and duplicate-submission
controls, a mapping saved during an in-flight reconciliation, and search/filter/pagination with 57 synthetic products.
Existing navigation cancellation, disabled upload inputs, and completed-mapping invalidation remain in place. A reproduced
race allowed an old approval to return after a new mapping was saved. Shared-state version checking now discards that
response and asks the user to reconcile again. Server failures no longer display raw response bodies.

Seven backend cases cover missing/blank configuration, safe database-outage responses without credentials or approvals,
untrusted Host rejection, and lack of CORS permission for an untrusted Origin. The API now returns 503 for database
failures, rejects blank connection settings at startup, and restricts accepted hosts to loopback names/addresses.
The reviewed Compose, API launch-profile, Vite, and E2E bindings are local; deployment overrides still need review on
the release machine. Host filtering and CORS do not provide authentication.

Validation passed 150 backend tests and 24 Playwright tests, backend and frontend builds, lint, TypeScript checks, Git
whitespace checks, and test-container/process cleanup. No supplier approval rules, CSV contract, dependencies, or
migrations changed. See [TESTING.md](TESTING.md#milestone-2-recovery-stale-results-and-local-safety) for reproduced defects,
verified existing behavior, architecture, and limits. Cross-tab/direct-database changes and ambiguous mutation outcomes
are not tracked by the browser's local version. Health is liveness only; live Shopify import remains unverified.
This records Milestone 2 completion; subsequent milestone status is recorded below.

#### Release milestone 3: Reproducible setup, populated upgrades, and CI

Status: complete, committed and pushed as `4b67ae2`, with the shallow-history whitespace correction in `9fb74ea`. CI passed for `9fb74ea`.

The README defines one loopback-only workstation workflow, tool versions, configuration, migrations, synthetic browser
upload/download, stopping without deleting volumes, recovery, and backup/downgrade limits. `global.json`, `.node-version`,
and the local EF tool manifest select .NET 10.0.401 (same-feature-band patch roll-forward), Node 24.22.1, and EF 10.0.12.
Vite explicitly binds `127.0.0.1:5173`, fails on a busy port, proxies to `127.0.0.1:5233`, and does not load `.env` files.
Backend test databases also bind to loopback. No application dependency or migration was upgraded.

A new Testcontainers regression migrates a real v0.2 database, inserts synthetic verified/unverified mappings, applies
the remaining migration, and verifies all mapping fields survive. It confirms the old Shopify uniqueness constraint,
new multi-supplier support, and retained identifier/primary-key integrity. The four historical migrations match tag
`v0.2.0`. This test uses only temporary data and does not test destructive downgrade or a real backup restore.

A SHA-pinned, read-only GitHub Actions job on Ubuntu 24.04 runs backend checks, locked frontend installation/build/lint/
types, and the existing isolated Chromium runner. It retains synthetic diagnostics and checks cleanup without deleting
volumes. Actionlint and shell parsing pass locally.
[GitHub run 40578983483](https://github.com/AtiqueAqtab/DealerSync/actions/runs/40578983483) independently passed Linux
browser installation, application checks, the corrected whitespace step, cleanup, and artifact upload for `9fb74ea`.

Local validation passed 151 backend tests, 24 Playwright tests, backend and frontend builds, clean npm installation,
lint, TypeScript, whitespace, and cleanup checks using the pinned Node version. A separate temporary-database exercise
verified the documented API/frontend listeners, normal Vite proxy, root sample reconciliation, and three-row approved
CSV download. The development database, user-secret entry, and private inventory were not used.

Milestone 3 is complete. Milestone 4 below records final acceptance; operator recovery for important data and the
local-only/health/CLI/Shopify limits remain separate from synthetic validation.

#### Release milestone 4: Final release-candidate validation

Status: PASS for the scoped local release candidate at `9fb74ea`, awaiting review of uncommitted release documentation.
No blocking production defect was found; no feature, dependency, application-code, or migration change was needed.

A fresh temporary HTTPS clone restored the pinned tools and dependencies. Both builds, all 151 backend tests, all 24
Playwright tests, lint, TypeScript, whitespace, and cleanup checks passed. The backend run included the actual populated
v0.2 upgrade regression. The exact committed revision also passed GitHub CI on Ubuntu 24.04.5.

A separate temporary Compose deployment verified loopback listeners, the normal Vite proxy, the complete synthetic
mapping/verification/reconciliation/export workflow, blocked inputs, stale approval clearing, and real API/database
outage handling. An actual archive/list/restore drill preserved three mappings, seven summaries, and five migration
records, then generated a fresh approved file through the restored API. No development database, private inventory,
normal secrets, or persistent volume was used. Supported mapping changes are creation and verification; identifier
editing/deletion was not invented as part of acceptance.

[RELEASE_READINESS.md](RELEASE_READINESS.md) records PASS/NOT VERIFIED criteria, evidence, commands, known limits, and the
approval/commit/CI/merge/tag sequence. [RELEASE_NOTES_v0.3.0.md](RELEASE_NOTES_v0.3.0.md) is the proposed release text.
Live Shopify import remains unverified, invalidation does not synchronize across browser tabs or external changes,
and authentication/cloud/multi-user operation are outside this release. Real dealership recovery requires its own
operator-reviewed restore verification. No merge, release, or tag has been created.

#### Remaining v0.3 work

1. Review the Milestone 4 report, proposed release notes, and every unverified/unsupported criterion.
2. After approval, commit and push only the reviewed release documentation; require successful CI for that exact SHA.
3. Review/approve the release PR and require checks on the resulting release commit; repeat affected acceptance if application code changes.
4. Confirm the intended target commit and absent `v0.3.0` tag, obtain explicit merge/tag/release approval, and follow the report's proposed sequence.

Scope decisions: the supplied v0.3 milestone says CSV uploads, not drag-and-drop specifically, and does not explicitly require a dedicated History page. The older README proposed both drag-and-drop and history UI. Confirm whether those older proposals remain release requirements before adding them. Settings is not an agreed v0.3 requirement. A History page can initially show saved summaries without promising historical item-level details.

Suggested next task: review the final release-candidate report and documentation, then approve specific Git/release actions separately. Passing validation does not authorize them. Dedicated History/Settings screens and unrestricted network deployment are outside the local CSV MVP.

### v0.4.0 — Reliability, Security and Workflow Improvements

Status: planned; some validation and error handling already exist, but the milestone is not complete.

Purpose: make the dashboard dependable and establish access controls before a dealership pilot.

- [ ] Improve validation and error handling across frontend/API boundaries.
- [ ] Improve frontend workflows based on v0.3 findings.
- [ ] Add authentication and authorization.
- [ ] Introduce a consistent structured logging approach. Framework logging alone does not complete this work.
- [ ] Extend the v0.3 GitHub Actions CI baseline into an approved deployment workflow.
- [ ] Measure and improve performance.

Dependencies: a reviewed v0.3 workflow, representative synthetic test data, and decisions about staff roles and deployment.

Expected outcome: controlled access, useful operational errors/logs, repeatable build/test automation, and measured performance. Preserve existing inventory safeguards while improving the workflow.

### v0.5.0 — Dealership Pilot

Status: planned; the synthetic demonstrations do not constitute a dealership pilot.

Purpose: validate the application against representative dealership work before production release.

- [ ] Test representative dealership inventory exports with permission.
- [ ] Measure reconciliation performance.
- [ ] Improve workflows based on actual usage.
- [ ] Create operating documentation.
- [ ] Validate backup and recovery procedures, including a restore exercise.

Dependencies: v0.4 access controls/reliability, an agreed pilot environment, authorized data access, and staff participation.

Expected outcome: documented pilot findings, usable operating procedures, measured limits, and evidence that data can be recovered. Keep dealership exports and database backups out of Git.

### v1.0.0 — First Production Release

Status: planned.

Purpose: release the validated reconciliation workflow for staff use.

- [ ] Deliver a stable inventory reconciliation platform.
- [ ] Provide secure staff access.
- [ ] Validate reliable Shopify inventory CSV exports in the production workflow.
- [ ] Validate safety controls against agreed acceptance cases.
- [ ] Complete production deployment and documentation.

Dependencies: v0.5 pilot outcomes, security checks, tested backup/recovery, and operational ownership.

Expected outcome: a supported, documented production CSV workflow. An existing CSV exporter does not by itself establish production export reliability. Direct Shopify API updates remain outside v1.0 scope.

## Phase 3: Approved Shopify updates and traceability

### v1.1.0 — Shopify API Integration

Status: planned.

Purpose: retrieve Shopify data and apply explicitly approved updates without moving CSV files manually.

- [ ] Integrate the Shopify Admin GraphQL API.
- [ ] Retrieve inventory and product information.
- [ ] Preview proposed changes.
- [ ] Add a human approval workflow.
- [ ] Apply approved inventory updates.
- [ ] Handle API failures and uncertain update outcomes.

Dependencies: v1.0 reliability/security, authorized Shopify access, an agreed approval model, and a safe test-store workflow.

Expected outcome: staff can inspect and authorize live updates. Retain safeguards and reconcile uncertain outcomes before retrying writes.

### v1.2.0 — Audit Logging and Advanced History

Status: planned. Current saved run summaries are a starting point, not a completed audit trail.

Purpose: explain which changes were proposed, approved, rejected, and applied.

- [ ] Record inventory changes.
- [ ] Track approvals and rejections.
- [ ] Record previous quantities.
- [ ] Preserve reconciliation details.
- [ ] Support investigation of synchronization problems.

Dependencies: v1.1 update/approval events, staff identity, and a retention/storage design. Basic safety logging needed for v1.1 must not wait for this richer milestone.

Expected outcome: traceable inventory changes and historical details sufficient to investigate an incident.

## Phase 4: Supplier catalogs and publishing

### v2.0.0 — Supplier Import Framework

Status: planned. Inventory CSV importers are not a supplier product-catalog framework.

Purpose: normalize varied supplier catalogs before adding supplier-specific integrations.

- [ ] Define a standardized supplier product model.
- [ ] Build reusable supplier adapters.
- [ ] Identify products and variants.
- [ ] Validate catalogs.
- [ ] Support selective product importing.

Dependencies: sample supplier formats, agreed product identity rules, and established validation/persistence boundaries. Publishing later depends on Shopify access; parsing itself need not.

Expected outcome: validated common product records that retain their supplier identifiers and source context.

### v2.1.0 — KLIM Supplier Integration

Status: planned.

Purpose: implement the first supplier adapter against actual KLIM catalog formats.

- [ ] Process KLIM ZIP archives.
- [ ] Parse spreadsheets.
- [ ] Extract product images.
- [ ] Associate images with products.
- [ ] Compare imported products against Shopify.

Dependencies: v2.0 framework, authorized example archives, and Shopify product retrieval for comparison.

Expected outcome: a repeatable KLIM import with reviewed product/image associations and differences against Shopify.

### v2.2.0 — JSON/XML Supplier Feeds

Status: planned.

Purpose: extend the shared framework to structured supplier feeds.

- [ ] Parse JSON and XML.
- [ ] Map supplier-specific fields.
- [ ] Normalize products.
- [ ] Validate feeds.

Dependencies: v2.0 product model/adapters and representative feed contracts. Lessons from KLIM should inform reusable behavior without making unrelated feeds depend on KLIM-specific rules.

Expected outcome: consistent validated records from multiple feed formats without discarding supplier-specific meaning.

### v2.3.0 — Shopify Product Publishing

Status: planned.

Purpose: turn reviewed supplier catalog differences into selectively published Shopify products.

- [ ] Identify missing Shopify products.
- [ ] Generate product drafts and variants.
- [ ] Import descriptions, images, and pricing.
- [ ] Allow staff to select and publish products.

Dependencies: validated supplier adapters, Shopify API access, product/variant identity rules, and staff approval/audit workflows.

Expected outcome: staff-reviewed product creation without duplicate listings or automatic publication of every supplier record.

## Phase 5: Matching assistance and scheduled work

### v3.0.0 — Advanced Product Matching

Status: planned. Existing deterministic SKU normalization does not complete this milestone.

Purpose: suggest relationships when identifiers alone do not establish a match.

- [ ] Compare product title similarity.
- [ ] Match manufacturer part numbers.
- [ ] Compare brand, size, and color.
- [ ] Suggest SKU mappings.
- [ ] Provide interpretable confidence scores.
- [ ] Require manual verification of uncertain matches.

Dependencies: normalized catalog data, reviewed matching examples, and the existing mapping/approval workflow.

Expected outcome: useful suggestions with enough evidence for staff review. A score must not silently authorize inventory updates.

### v3.1.0 — Scheduled Reconciliation

Status: planned.

Purpose: run recurring comparisons and feed checks with visible job status.

- [ ] Run background jobs.
- [ ] Schedule supplier feed checks.
- [ ] Send inventory discrepancy alerts.
- [ ] Monitor jobs and failures.
- [ ] Generate automated reports.

Dependencies: reliable adapters and reconciliation, operational logging, safe retries, and agreed notification/update authorization rules.

Expected outcome: monitored recurring work and actionable reports. Scheduling comparisons must not implicitly authorize live inventory writes.

## Phase 6: Direct inventory source integration

### v4.0.0 — Direct Lightspeed EVO Integration

Status: exploratory; feasibility depends on the access and capabilities Lightspeed EVO makes available.

Purpose: reduce reliance on manually exported Lightspeed inventory files where supported.

- [ ] Investigate available API access and usage terms.
- [ ] Retrieve inventory directly.
- [ ] Support incremental synchronization if the source exposes reliable change information.
- [ ] Detect conflicts.
- [ ] Evaluate automated Lightspeed-to-Shopify reconciliation.

Dependencies: a completed access/feasibility investigation, stable Shopify integration, auditability, and reliable scheduling/approval controls.

Expected outcome: a validated direct integration if feasible. Do not promise automated synchronization, a delivery date, or unsupported API capabilities before the investigation.

## Maintaining this plan

Consult [../AGENTS.md](../AGENTS.md) before changes. Keep the author involved: explain proposed work, affected files, algorithms, and important trade-offs before implementation. Update status with repository/test evidence and record scope decisions explicitly. Do not mark later milestones complete because a related foundation exists. Commit, push, merge, database changes, and live Shopify writes require the appropriate explicit authorization.
