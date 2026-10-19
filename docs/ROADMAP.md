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

Status: core features implemented; final end-to-end acceptance and release preparation remain open.

Purpose: let staff reconcile files and verify supplier mappings without manual API calls, including products supplied by more than one Lightspeed part number.

- [x] React/TypeScript dashboard.
- [x] Inventory reconciliation interface and summaries.
- [x] CSV file selection and uploads.
- [x] Searchable results and filtering.
- [x] SKU mapping list and creation.
- [x] Supplier mapping verification from the UI.
- [x] Multi-supplier quantity aggregation.
- [x] Supplier contribution diagnostics in results and diagnostics CSVs.
- [x] Inventory safety rules for unverified/missing suppliers, invalid quantities, ambiguous data, and approved-update separation.
- [ ] Complete end-to-end acceptance: retain the manual workflow checks and add repeatable automated browser coverage for critical paths.
- [ ] Finish release preparation, documentation, branch review/merge, and an approved `v0.3.0` tag.

Dependencies: v0.2 API/persistence, the `AllowMultipleSuppliersPerShopifySku` migration, and consistent API/frontend contracts. The developer reported successfully applying the migration locally; other environments must be checked separately.

Expected outcome: a staff-facing local reconciliation workflow showing current Shopify quantity, trusted supplier contributions, calculated combined quantity, and a separate backend-approved quantity. A missing approval is blank; an approved zero remains `0`.

Evidence: `frontend/src/features/dashboard`, `frontend/src/features/reconciliation`, `frontend/src/features/mappings`, `frontend/src/features/inventory`, `frontend/src/lib/csvExport.ts`, API DTOs, Core matching/diagnostic services, and backend regression tests. Related commits are `e17b4c6`, `296300f`, `36270c3`, and `c5c1fda`.

#### Playwright Step 1

The first browser test now runs one exact-match reconciliation against a separate API and disposable PostgreSQL container. It verifies current quantity `2`, calculated quantity `7`, and approved quantity `7`. The smoke test, its TypeScript check, frontend build/lint, and all 104 existing backend tests passed during implementation. SIGTERM cleanup was also checked, and no E2E runner containers remained afterward.

Step 1 established the environment and exact-match smoke test. Step 2A adds the supplier scenarios described below. Mapping UI workflows, broader UI behavior, invalid quantities, and CI remain pending. See [TESTING.md](TESTING.md) for local commands and isolation details. The baseline inventory above describes the repository before this infrastructure was added.

#### Playwright Step 2A: Multi-supplier inventory safety

Three additional browser tests use the real frontend, API, and disposable PostgreSQL database. They create mappings through the existing API, verify the intended suppliers, and reconcile synthetic CSVs through the browser.

- Two verified suppliers with quantities 3 and 4 produce combined and approved quantities of 7 and one eligible update.
- An unverified second supplier is excluded from the combined quantity. The verified contribution is 3, the approved quantity is null, and `UnverifiedSupplier` blocks updates.
- A missing verified supplier leaves the available contribution at 3, with a null approved quantity and `MissingSupplierInventory` blocking updates.

Each scenario checks the actual API contract, visible supplier contributions and quantities, and inclusion or exclusion under Approved Updates. The missing-supplier response flags the reason but does not name the missing part; the test verifies the omitted supplier's persisted mapping as part of setup.

Per-test SKU namespaces keep mappings independent without reset SQL. The existing runner still owns the separate API, frontend, and temporary database. All four browser tests, 104 existing backend tests, the frontend production build, lint, and test TypeScript checks passed during this step. The supplier scenarios also passed in reverse order against a fresh isolated stack. The full end-to-end acceptance milestone remains open. See [TESTING.md](TESTING.md) for fixtures, expected values, and commands.

#### Remaining v0.3 work

1. Extend the existing Playwright suite to cover mapping verification through the UI followed by a new reconciliation, invalid quantities, approved zero, diagnostics download, and loading/failure states. Complete search/filter/pagination coverage and decide the History page scope. Upload-to-results and the three Step 2A supplier scenarios already have real-service coverage.
2. Run the final backend and frontend checks against the intended release revision. Confirm that fresh-database setup and upgrade instructions include the multi-supplier migration.
3. Update the README/current-version information and record release notes, known limitations, setup steps, and synthetic test instructions.
4. Review the pull request, obtain approval to merge and tag, and verify the target environment if deployment is part of this release. Do not infer deployment from a Git push.

Scope decisions: the supplied v0.3 milestone says CSV uploads, not drag-and-drop specifically, and does not explicitly require a dedicated History page. The older README proposed both drag-and-drop and history UI. Confirm whether those older proposals remain release requirements before adding them. Settings is not an agreed v0.3 requirement. A History page can initially show saved summaries without promising historical item-level details.

Suggested next task: agree on the next small acceptance-testing step, such as invalid-quantity and approved-zero coverage. Explain the fixtures and expected backend behavior before implementation, then obtain approval. If the author confirms History is a v0.3 requirement, plan that page as a separate task using the existing summaries API.

### v0.4.0 — Reliability, Security and Workflow Improvements

Status: planned; some validation and error handling already exist, but the milestone is not complete.

Purpose: make the dashboard dependable and establish access controls before a dealership pilot.

- [ ] Improve validation and error handling across frontend/API boundaries.
- [ ] Improve frontend workflows based on v0.3 findings.
- [ ] Add authentication and authorization.
- [ ] Introduce a consistent structured logging approach. Framework logging alone does not complete this work.
- [ ] Add CI/CD with GitHub Actions.
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
