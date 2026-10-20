# Local browser acceptance tests

The first Playwright test uploads two synthetic inventory files through the React reconciliation page. It checks a current Shopify quantity of `2`, a calculated supplier quantity of `7`, and a backend-approved quantity of `7`. The browser talks to the real API and PostgreSQL. No API responses are mocked.

The suite has 12 real-service Chromium tests: the exact-match smoke test, three supplier-safety tests, seven quantity-validation tests, and one CSV text/export test. Mapping UI workflows, broader search/pagination coverage, history, and CI integration remain later tasks in the [roadmap](ROADMAP.md).

## Supplier safety coverage

| Scenario | Input | Combined quantity | Approved quantity | Diagnostic status | Generated updates |
| --- | --- | --- | --- | --- | --- |
| Two verified suppliers | A: 3, B: 4; Shopify: 2 | 7 | 7 | `Ready` | 1 |
| Verified and unverified suppliers | A verified: 3, B unverified: 4; Shopify: 2 | 3 | `null` | `ReviewRecommended` / `UnverifiedSupplier` | 0 |
| Missing supplier inventory | A and B verified, only A: 3 in the CSV; Shopify: 2 | 3 | `null` | `ReviewRecommended` / `MissingSupplierInventory` | 0 |

Only exact or verified matches contribute to calculated inventory. The unverified supplier's quantity of 4 is therefore excluded from the second scenario's combined quantity. A calculated quantity of 3 is still available for diagnostics in each blocked case, while the backend-approved quantity is null and the UI displays a dash.

The response identifies missing inventory through `MissingSupplierInventory`; it does not provide the missing part number. The missing-supplier test establishes that B has a persisted verified mapping, omits B from its input, and checks the actual reason and UI warning. It does not assert an unsupported missing-part field.

## Quantity validation and diagnostics coverage

Step 2B adds these cases through real browser uploads:

| Scenario | Expected result |
| --- | --- |
| Verified suppliers: 7 and -2; Shopify: 2 | Combined 5, null approval, `InvalidQuantity`, review required, zero updates; excluded from Approved Updates |
| Verified supplier: 0; Shopify: 5 | Combined 0, approved 0, `Ready`, one update; included in Approved Updates; CSV contains `0` |
| Lightspeed quantity `abc` or blank | HTTP 400 with the invalid value, part number, and CSV row; no results or diagnostics download |
| Shopify quantity `1.5` or `2147483648` | HTTP 400 with the invalid value, SKU, and CSV row; no results or diagnostics download |
| Verified suppliers: 2147483647 and 1 | HTTP 400 with `Inventory reconciliation failed due to conflicting data.`; no result or approved quantity |
| CSV text containing commas, quotes, a newline, and `Café` | Parsed download preserves the text and UTF-8 BOM |
| Titles beginning with `=`, `+`, `-`, or `@` | Parsed download starts each formula-like title with an apostrophe |

The four malformed-input browser tests cover each requested error category and both upload paths. Existing xUnit importer theories already test all four formats in both importers, so the browser suite does not repeat that full matrix. Integer parsing rejects fractional, missing, nonnumeric, and out-of-range values instead of rounding or substituting zero.

The negative-supplier case deliberately has a positive total. Its approval must still be null because any negative contributor blocks that product. The current API returns `reviewReasons: []` here: `InvalidQuantity` and `requiresReview: true` carry the warning, while review reasons describe unverified or missing mappings.

Aggregation uses `long` before checking whether the result fits a signed 32-bit inventory quantity. The existing xUnit `Reconciliation_BlockedSupplierWithOverflow_DoesNotFail` covers an overflowing product already blocked by an unverified supplier; it returns null combined and approved quantities. The browser test covers the distinct fully verified path, which rejects the whole upload with HTTP 400. This prevents an unsafe update, though the generic conflicting-data message does not explain the overflow. The frontend also displays API errors as raw JSON text. These are existing error-reporting limitations; Step 2B does not change production behavior.

Zero and null have different meanings. An approved zero is an explicit instruction to change stock to zero. Null means the backend generated no update. The API derives `proposedQuantity` from its approved update collection after applying supplier trust, review, quantity, and difference checks. Tests check the response, result cells, Approved Updates filter, and downloaded CSV to ensure the calculated quantity never fills a missing approval.

`frontend/e2e/diagnostics.ts` waits for Playwright's actual download event before clicking the export button, reads the downloaded bytes, and parses them with the test-only `csv-parse` dependency. It checks all 14 headers independently of the application exporter. Expected quantities and text come from literal fixture values and test expectations, not a call to the exporter or a copy of the API response. The original supplier scenarios now also verify blank blocked approvals, statuses, reason arrays, and supplier contribution JSON in the download.

CSV quoting keeps commas, quotes, and line breaks inside their fields. It does not prevent a spreadsheet from interpreting a cell as a formula. The formula-prefix cases check the exporter's apostrophe protection using harmless synthetic expressions. Tests inspect the resulting file; they do not launch spreadsheet software or execute formulas. This is representative coverage, not a claim about every spreadsheet application's import behavior.

## Fixtures and test isolation

`frontend/e2e/support.ts` creates an identifier namespace from the run ID, test ID, repeat index, and retry index. Each supplier test receives its own Shopify SKU and two supplier part numbers. The CSV templates in `frontend/e2e/fixtures/` contain explicit quantities for supplier, negative, zero, overflow, and text cases. The helper replaces `{{CASE}}` with that test's namespace. Only malformed-input templates use quantity placeholders; the tests supply the four literal invalid values above and valid values for the other upload.

The mapping helper calls the real `POST /api/sku-mappings` endpoint. New mappings must be unverified. For verified suppliers, it calls `PATCH /api/sku-mappings/{id}/verify`. A subsequent GET checks that the requested verification state was persisted, including when B must remain unverified.

Tests share the runner's disposable database, but do not share identifiers or assume an empty mappings table. Quantity and supplier cases each upload one Shopify product; the text/export case uploads five distinct exact-match products. They can run in any order without claiming another test's inventory or mappings. No per-test deletion or database reset is performed; all records disappear when the runner removes its own container. Keep the existing single-worker configuration for this stage. Step 2A was checked in both the declared and reversed supplier-scenario order.

The shared browser fixture blocks requests outside the isolated frontend origin for all 12 tests. The upload helper uses the real reconciliation page and reads the response to its POST; it does not call reconciliation in place of browser interaction.

Assertions check the backend summary and per-product contract, rendered quantities, contribution part numbers and quantities, and review messages. For blocked products, the detailed response has `proposedQuantity: null` and `inventoryUpdatesGenerated: 0`. Because the input contains one Shopify product, that zero applies to the tested SKU. The response does not expose a separate inventory-update collection. The Approved Updates filter must also hide the blocked row, preventing the calculated value from being presented as an approved update.

## Requirements

- macOS or Linux, with Node.js compatible with the frontend's installed Vite version.
- .NET 10 SDK and the EF Core CLI. Confirm `dotnet ef --version` works.
- A running Docker daemon. The first run may download `postgres:17`.
- Frontend dependencies and Playwright's Chromium browser.

From the repository root, install the frontend dependencies and browser:

```bash
npm --prefix frontend ci
cd frontend
npx playwright install chromium
```

On Linux, Playwright may also need system browser libraries. Use its documented `npx playwright install --with-deps chromium` setup on the test machine.

## Run the tests

From `frontend/`:

```bash
npm run test:e2e
```

To see the browser while it runs:

```bash
npm run test:e2e -- --headed
```

The command type-checks the test code and then:

1. Creates a uniquely named PostgreSQL 17 container with a generated test database and credentials. PostgreSQL data lives in a new memory-backed mount.
2. Verifies the container label, storage type, and live database name before applying the existing EF migrations. It does not reset a schema or delete records.
3. Starts a separate API process in the `Testing` environment with an explicit test connection string and a temporary content directory. Development launch profiles and user-secret configuration are not used.
4. Starts Vite with the existing application plugins and a test-only proxy target. The test configuration disables `.env` file loading. Both servers use automatically selected loopback ports.
5. Waits for a database-backed request through the frontend proxy, runs 12 Chromium tests, and checks API responses, visible results, and downloaded CSVs.
6. Stops the process groups it started and removes only the container whose identity and ownership label match this run. It does not remove Docker volumes.

No normal development server needs to be stopped. The runner does not reuse it, read the development `.env`, load private inventory files, or call Shopify. The browser blocks requests outside its test frontend origin. The CSV fixtures are committed under `frontend/e2e/fixtures/`.

## Results and failures

Step 2B validation passed all 12 browser tests and 104 existing backend tests, the frontend production build, lint, and test TypeScript checks. `git diff --check` passed. The production build used the environment-file-disabled command below. The runner removed its owned PostgreSQL container, and a separate label-based Docker check found no remaining E2E containers. Application code and existing backend tests were unchanged.

The terminal reports the test result. Playwright saves its HTML report under `frontend/playwright-report/`. Failed tests also retain a trace and screenshot under `frontend/test-results/`. Both directories are ignored by Git.

From `frontend/`, open the report with:

```bash
npx playwright show-report
```

On a fresh database, EF Core can log an initial failed query for the migration-history table before creating it and completing the migrations. The HTTP-only test API can also log that no HTTPS redirect port is configured. Neither warning means the browser tests failed; migration commands, readiness checks, and browser assertions must still succeed.

The runner returns a nonzero exit code for startup, migration, test, or cleanup failures. Logs from the API and Vite remain visible in the terminal. A cleanup failure prints the exact test container name for investigation.

Normal success, failures, Ctrl+C, and SIGTERM trigger cleanup. An uncatchable kill or machine shutdown can leave a stopped or running test container. Inspect the exact named container and its `com.dealersync.e2e.run` label before removing it; do not remove development containers or volumes. This first runner supports macOS/Linux process groups and does not provide Windows support or automatic orphan cleanup.

## Other checks

Keep the existing backend tests and frontend checks:

```bash
# From the repository root; integration tests require Docker.
dotnet build
dotnet test --verbosity normal
npm --prefix frontend run build
npm --prefix frontend run lint
npm --prefix frontend run test:e2e:types
```

The backend integration tests already create their own PostgreSQL Testcontainers. The browser runner creates a separate database because a browser needs an API listening on a real network port; the xUnit in-process server is not reachable from Chromium.

Backend tests, frontend build/lint, and the browser suite check different behavior. These 12 browser tests do not complete v0.3's full end-to-end acceptance plan.

For a production-build check that also disables environment-file loading, run this from `frontend/`. These values are unused loopback addresses during a build; the command does not start servers.

```bash
DEALERSYNC_E2E_RUN_ID=build-validation \
DEALERSYNC_E2E_API_URL=http://127.0.0.1:1 \
DEALERSYNC_E2E_BASE_URL=http://127.0.0.1:2 \
npm run build -- --config e2e/vite.config.ts
```
