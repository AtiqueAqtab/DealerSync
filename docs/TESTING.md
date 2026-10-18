# Local browser smoke test

The first Playwright test uploads two synthetic inventory files through the React reconciliation page. It checks a current Shopify quantity of `2`, a calculated supplier quantity of `7`, and a backend-approved quantity of `7`. The browser talks to the real API and PostgreSQL. No API responses are mocked.

This test covers one exact-match reconciliation. Multi-supplier safety cases, mapping workflows, history, and CI integration remain later tasks in the [roadmap](ROADMAP.md).

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

## Run the smoke test

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
5. Waits for a database-backed request through the frontend proxy, runs one Chromium test, and checks the API response and visible result cells.
6. Stops the process groups it started and removes only the container whose identity and ownership label match this run. It does not remove Docker volumes.

No normal development server needs to be stopped. The runner does not reuse it, read the development `.env`, load private inventory files, or call Shopify. The browser blocks requests outside its test frontend origin. The CSV fixtures are committed under `frontend/e2e/fixtures/`.

## Results and failures

The terminal reports the test result. Playwright saves its HTML report under `frontend/playwright-report/`. Failed tests also retain a trace and screenshot under `frontend/test-results/`. Both directories are ignored by Git.

From `frontend/`, open the report with:

```bash
npx playwright show-report
```

On a fresh database, EF Core can log an initial failed query for the migration-history table before creating it and completing the migrations. The HTTP-only test API can also log that no HTTPS redirect port is configured. Neither warning means the smoke test failed; migration commands, readiness checks, and browser assertions must still succeed.

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

Application tests, frontend build/lint, and this one smoke test each check different behavior. A passing smoke test does not complete v0.3's full end-to-end acceptance plan.

For a production-build check that also disables environment-file loading, run this from `frontend/`. These values are unused loopback addresses during a build; the command does not start servers.

```bash
DEALERSYNC_E2E_RUN_ID=build-validation \
DEALERSYNC_E2E_API_URL=http://127.0.0.1:1 \
DEALERSYNC_E2E_BASE_URL=http://127.0.0.1:2 \
npm run build -- --config e2e/vite.config.ts
```
