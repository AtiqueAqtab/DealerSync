# Local browser acceptance tests

The first Playwright test uploads two synthetic inventory files through the React reconciliation page. It checks a current Shopify quantity of `2`, a calculated supplier quantity of `7`, and a backend-approved quantity of `7`. The browser talks to the real API and PostgreSQL. No API responses are mocked.

The suite includes the original exact-match smoke test and three supplier-safety tests. Mapping UI workflows, negative/invalid quantities, broader search/pagination coverage, history, diagnostic downloads, and CI integration remain later tasks in the [roadmap](ROADMAP.md).

## Supplier safety coverage

| Scenario | Input | Combined quantity | Approved quantity | Diagnostic status | Generated updates |
| --- | --- | --- | --- | --- | --- |
| Two verified suppliers | A: 3, B: 4; Shopify: 2 | 7 | 7 | `Ready` | 1 |
| Verified and unverified suppliers | A verified: 3, B unverified: 4; Shopify: 2 | 3 | `null` | `ReviewRecommended` / `UnverifiedSupplier` | 0 |
| Missing supplier inventory | A and B verified, only A: 3 in the CSV; Shopify: 2 | 3 | `null` | `ReviewRecommended` / `MissingSupplierInventory` | 0 |

Only exact or verified matches contribute to calculated inventory. The unverified supplier's quantity of 4 is therefore excluded from the second scenario's combined quantity. A calculated quantity of 3 is still available for diagnostics in each blocked case, while the backend-approved quantity is null and the UI displays a dash.

The response identifies missing inventory through `MissingSupplierInventory`; it does not provide the missing part number. The missing-supplier test establishes that B has a persisted verified mapping, omits B from its input, and checks the actual reason and UI warning. It does not assert an unsupported missing-part field.

## Fixtures and test isolation

`frontend/e2e/support.ts` creates an identifier namespace from the run ID, test ID, repeat index, and retry index. Each supplier test receives its own Shopify SKU and two supplier part numbers. The CSV templates in `frontend/e2e/fixtures/` contain literal quantities of 3 and 4 and a current Shopify quantity of 2. The helper replaces only `{{CASE}}` with that test's namespace.

The mapping helper calls the real `POST /api/sku-mappings` endpoint. New mappings must be unverified. For verified suppliers, it calls `PATCH /api/sku-mappings/{id}/verify`. A subsequent GET checks that the requested verification state was persisted, including when B must remain unverified.

Tests share the runner's disposable database, but do not share identifiers or assume an empty mappings table. Each uploads exactly one Shopify product. They can run in any order without claiming another test's inventory or mappings. No per-test deletion or database reset is performed; all records disappear when the runner removes its own container. Keep the existing single-worker configuration for this stage. Step 2A was checked in both the declared and reversed supplier-scenario order.

The shared browser fixture blocks requests outside the isolated frontend origin for all four tests. The upload helper uses the real reconciliation page and reads the response to its POST; it does not call reconciliation in place of browser interaction.

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
5. Waits for a database-backed request through the frontend proxy, runs four Chromium tests, and checks the API responses and visible result cells.
6. Stops the process groups it started and removes only the container whose identity and ownership label match this run. It does not remove Docker volumes.

No normal development server needs to be stopped. The runner does not reuse it, read the development `.env`, load private inventory files, or call Shopify. The browser blocks requests outside its test frontend origin. The CSV fixtures are committed under `frontend/e2e/fixtures/`.

## Results and failures

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
dotnet test --verbosity quiet
npm --prefix frontend run build
npm --prefix frontend run lint
npm --prefix frontend run test:e2e:types
```

The backend integration tests already create their own PostgreSQL Testcontainers. The browser runner creates a separate database because a browser needs an API listening on a real network port; the xUnit in-process server is not reachable from Chromium.

Backend tests, frontend build/lint, and the browser suite check different behavior. These four browser tests do not complete v0.3's full end-to-end acceptance plan.

For a production-build check that also disables environment-file loading, run this from `frontend/`. These values are unused loopback addresses during a build; the command does not start servers.

```bash
DEALERSYNC_E2E_RUN_ID=build-validation \
DEALERSYNC_E2E_API_URL=http://127.0.0.1:1 \
DEALERSYNC_E2E_BASE_URL=http://127.0.0.1:2 \
npm run build -- --config e2e/vite.config.ts
```
