# Working on DealerSync

## Purpose and planning

DealerSync grew out of inventory reconciliation problems the author encountered at Ottawa Goodtime Centre. It compares Lightspeed EVO inventory exports with Shopify inventory and helps staff review proposed changes. It is also a Computer Science portfolio project: the author needs to understand, maintain, and explain the code.

Read [docs/ROADMAP.md](docs/ROADMAP.md) before planning significant work. Use its version scope when planning work. A placeholder screen does not establish a release requirement. Check the implementation before marking a feature complete. Distinguish implemented code, passing checks, and a released version.

When this document was prepared, v0.3.0 is in development on `feature/multi-supplier-inventory`; the latest inspected feature commit is `c5c1fda`. Recheck Git rather than assuming these values remain current.

## Start each task

- Read applicable development instructions and the README. Inspect Git status, branch, recent commits, and the files relevant to the request.
- Preserve existing work. Do not discard changes, switch branches, reset history, or perform broad formatting without explicit authorization.
- Explain the proposed implementation and affected files before writing feature code. Discuss meaningful architecture choices and trade-offs.
- Break substantial features into reviewable tasks. Ask about decisions that change scope or inventory behavior; resolve routine implementation details within the agreed scope.

## Stack and architecture

- Backend: C# and .NET 10, ASP.NET Core Minimal APIs, EF Core, PostgreSQL/Npgsql, and CsvHelper.
- Frontend: React, TypeScript, Vite, React Router, Tailwind CSS, Lucide icons, and ESLint.
- Testing: xUnit and PostgreSQL integration tests using Testcontainers. Docker must be available for those tests. Playwright now provides one real Chromium reconciliation smoke test; see `docs/TESTING.md`. Broader browser coverage remains pending.
- `backend/DealerSync.Core`: domain models, matching, quantity aggregation, diagnostics, and approved update generation. Keep database, HTTP, and UI concerns out of Core.
- `backend/DealerSync.Infrastructure`: CSV import/export, reconciliation orchestration, EF Core persistence, repositories, and migrations.
- `backend/DealerSync.Api`: endpoint validation, DTOs, dependency registration, and calls into application services.
- `backend/DealerSync.Runner`: the CSV-based CLI. It currently does not load saved database mappings.
- `backend/DealerSync.Tests`: backend unit and API/integration coverage.
- `frontend/src`: `features/` for screens, `components/` for shared UI, `api/` for requests, `hooks/` for shared behavior, `types/` for contracts, and `lib/` for utilities.
- `samples/`: synthetic inventory examples. `data/`: ignored local inventory and generated exports.

Follow these boundaries. Keep changes small; do not introduce a new architectural layer or dependency without explaining why it is needed.

## Coding standards

- Follow the surrounding C# and TypeScript conventions. Preserve nullable contracts and use typed request/response models.
- Keep database entities and frontend contracts separate. Coordinate DTO, TypeScript, UI, and export changes when the contract changes.
- Preserve case-insensitive identifier handling and SKU/location identity. Do not treat product title similarity as a verified mapping.
- Provide loading, empty, success, and actionable error states. Use accessible labels and keyboard-operable controls.
- Prevent duplicate submissions. Invalidate stale reconciliation results after a successful mapping change.
- Do not silently coerce malformed quantities or replace missing values with zero. Use null-aware checks so an approved quantity of `0` remains valid.
- Avoid unrelated renaming, dependency upgrades, formatting, and refactoring.

## Inventory safety

- Unverified mappings must not generate inventory updates. A known unverified supplier blocks its Shopify product even when other contributors are verified.
- A mapped supplier missing from the inventory input must be flagged and must block the affected update.
- Ambiguous or conflicting assignments must not be silently accepted. Preserve duplicate and location validation.
- Reject malformed numeric input; exclude or reject invalid quantities, including negative values. Aggregate multiple trusted suppliers without integer overflow.
- Treat calculated combined inventory and backend-approved updates as different values. `combinedQuantity` is diagnostic inventory; `proposedQuantity` comes from an actual approved update and may be null.
- Normalized matches are suggestions and must not become approved updates without a trusted relationship.
- Keep diagnostics CSVs distinct from Shopify import CSVs. Preserve CSV quoting, encoding, and spreadsheet formula protection when changing exports.
- Never modify live Shopify inventory without explicit authorization and an agreed approval workflow. The current CSV workflow does not provide direct Shopify synchronization.
- Do not weaken safety checks to make a demonstration, test, or import succeed.

## Testing and verification

Run checks appropriate to the change and report exactly what ran. Do not describe historical results or static inspection as a fresh test run.

From the repository root, for backend changes:

```bash
dotnet build
dotnet test --verbosity quiet
```

For frontend changes, from `frontend/`:

```bash
npm run build
npm run lint
```

Add meaningful automated regression tests for new behavior and safety fixes. Use the existing xUnit/Testcontainers setup for backend work. Agree on a browser test approach before adding a new frontend test dependency. Build and lint are not end-to-end tests.

For reconciliation changes, cover trusted aggregation, unverified and missing suppliers, ambiguous/duplicate assignments, negative and overflowing quantities, approved zero, and JSON/CSV agreement as applicable. Check verification followed by a fresh reconciliation, plus loading and failure behavior for UI changes.

Use synthetic fixtures. Integration tests should use their disposable test databases, not the dealership or development database. Migration changes require review, appropriate tests, and explicit authorization before application to an existing database. Do not reset a database or remove Docker volumes to bypass a migration problem.

For documentation-only changes, check links, scope accuracy, whitespace, and Git status. Do not rebuild or mutate the database without a task-related reason.

## Git and data handling

- Inspect the diff and whitespace before presenting a checkpoint. Stage only approved, task-related files; avoid `git add .` when unrelated work exists.
- Let the author review changes. Do not commit, push, or merge without explicit approval. Do not create release tags without approval.
- Keep `AGENTS.md` and `docs/ROADMAP.md` tracked. Do not add ignore rules for them. Creating a file does not make it tracked; stage it only within the approved Git scope.
- Keep credentials, `.env`, local environment configuration, private dealership exports, generated reports, dependencies, and build artifacts out of commits. Preserve the existing ignore rules.
- Never print secrets or copy private inventory into documentation or fixtures. Use .NET user secrets or environment configuration for credentials.

## Documentation and learning

- Keep the roadmap current when approved scope or confirmed completion changes. Update README instructions when user-visible workflows change.
- Preserve the author's personal motivation. Write in plain, accurate language; avoid promotional claims, corporate phrasing, and repetitive explanations.
- Apply `/humanizer` to documentation if that skill is available. Otherwise state its absence and apply these writing standards manually.
- Explain significant algorithms, safety rules, and trade-offs. Describe what each modified class or component is responsible for and why the change belongs there.
- After work, summarize changes, verification, limitations, and the next suggested task. Separate recommendations from authorized implementation and wait for approval when the task requires it.
