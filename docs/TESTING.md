# Local browser acceptance tests

The first Playwright test uploads two synthetic inventory files through the React reconciliation page. It checks a current Shopify quantity of `2`, a calculated supplier quantity of `7`, and a backend-approved quantity of `7`. The browser talks to the real API and PostgreSQL. Inventory approval assertions use real API responses. The reliability tests separately inject controlled HTTP and network failures at the local browser boundary.

The suite has 24 Chromium tests: the exact-match smoke test, three supplier-safety tests, seven quantity-validation tests, one diagnostics text/export test, two mapping-workflow tests, five approved-export tests, and five reliability tests. History UI remains outside browser coverage; CI wiring is described below and in the [roadmap](ROADMAP.md).

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

Aggregation uses `long` before checking whether the result fits a signed 32-bit inventory quantity. The existing xUnit `Reconciliation_BlockedSupplierWithOverflow_DoesNotFail` covers an overflowing product already blocked by an unverified supplier; it returns null combined and approved quantities. The browser test covers the distinct fully verified path, which rejects the whole upload with HTTP 400. This prevents an unsafe update, though the generic conflicting-data message does not explain the overflow. The frontend displays the API's JSON validation message without the JSON wrapper. Server failures and network errors use safe retry instructions. The generic overflow message remains an error-reporting limitation.

Zero and null have different meanings. An approved zero is an explicit instruction to change stock to zero. Null means the backend generated no update. The API derives `proposedQuantity` from its approved update collection after applying supplier trust, review, quantity, and difference checks. Tests check the response, result cells, Approved Updates filter, and downloaded CSV to ensure the calculated quantity never fills a missing approval.

`frontend/e2e/diagnostics.ts` waits for Playwright's actual download event before clicking the export button, reads the downloaded bytes, and parses them with the test-only `csv-parse` dependency. It checks all 14 headers independently of the application exporter. Expected quantities and text come from literal fixture values and test expectations, not a call to the exporter or a copy of the API response. The original supplier scenarios now also verify blank blocked approvals, statuses, reason arrays, and supplier contribution JSON in the download.

CSV quoting keeps commas, quotes, and line breaks inside their fields. It does not prevent a spreadsheet from interpreting a cell as a formula. The formula-prefix cases check the exporter's apostrophe protection using harmless synthetic expressions. Tests inspect the resulting file; they do not launch spreadsheet software or execute formulas. This is representative coverage, not a claim about every spreadsheet application's import behavior.

## Mapping workflow coverage

`frontend/e2e/mapping-workflow.spec.ts` adds two independent workflows using the existing supplier fixtures: A has 3 units, B has 4, and Shopify has 2. The API helper establishes A as a verified background supplier. B's creation and verification, plus the rejected duplicate creation of A, use React controls; direct GET requests independently check persisted state.

1. Create B through the mapping form and confirm its saved, unverified state. Reconcile through the browser: combined inventory is 3, approval is null, and `UnverifiedSupplier` blocks updates. Verify B through its row button, check the saved verified state and visible success message, then confirm the old Inventory and Reconciliation results are cleared. Re-upload the files and check both contributions, combined and approved quantities of 7, and one row under Approved Updates.
2. Start with A's approved quantity of 3. Attempt a duplicate mapping through the form and receive HTTP 409; the existing approval and diagnostics download must remain available. Create B successfully and confirm both result pages clear before any file selection. A fresh reconciliation must return combined 3, null approval, and no Approved Updates row because B is still unverified.

The stale-creation regression was run before changing production code. After correcting an ambiguous success-message test locator, 13 tests passed and this regression failed: the Inventory page still held the previous result after B was saved. Successful creation now calls `MappingsPage.handleCreated`, which clears the existing reconciliation context and then refreshes mappings. `CreateMappingForm` invokes this callback only after its API creation succeeds. Verification's invalidation path and backend approval rules are unchanged.

The workflow helpers navigate through sidebar links after the first page load. `uploadInventoryOnPage` selects files without reloading React, while existing tests retain `uploadInventory` and its initial navigation. Reloading would clear the provider and conceal the regression. Assertions inspect empty results before file selection, since selecting a file also clears old results. Network-response waits and retrying UI assertions synchronize the tests; there are no arbitrary delays.

Clearing results after a successful mapping change is conservative: the user must upload again even if another product was changed. This avoids presenting an old approval as current without adding result-version tracking. These initial tests cover completed requests in one browser context. The reliability tests below extend coverage to a mapping saved during reconciliation. Cross-tab invalidation and broad mapping error recovery remain outside coverage. They also cannot establish that real-world identifiers describe the same product; staff must confirm that before verification.

## Milestone 2: recovery, stale results, and local safety

`frontend/e2e/reliability.spec.ts` adds five browser tests using synthetic data:

- Invalid file extensions and empty files clear existing approvals and disable submission. A malformed quantity returns
  HTTP 400. Correcting the input on the same page clears the error and permits a successful reconciliation.
- Two failure cases hold an HTTP 503 or a network failure behind a promise. They check loading text, disabled file and
  submit controls, one submission, no old downloads, safe error messages, and a successful retry with the selected files.
  Only the failure is injected; the initial approval and retry use the real API and PostgreSQL.
- A delayed mapping creation overlaps a real reconciliation. The backend first calculates approval 3; the pending
  supplier then saves and invalidates that result before its response is delivered. The old approval must stay hidden.
  A fresh request sees the pending supplier and returns null approval. Sidebar navigation preserves React state.
- A generated dataset has 57 products: 30 approved at quantity 7, 26 unmatched, and one with negative stock. It checks
  SKU search, Ready/Mapping Required/Requires Review/Approved Updates filters, first and last pages, page reset after
  search or filter changes, and empty results. Requires Review includes all 26 unmatched products plus the negative
  product. Even with no visible rows, the approved download still contains exactly the 30 eligible products at quantity 7.

The mapping overlap test failed against Milestone 1: a late response restored approval 3 after successful creation had
cleared it. The shared provider now increments a version whenever results are invalidated. Reconciliation captures the
version before sending its request and accepts the response only if that version still matches. A discarded result shows
an instruction to run again. The existing abort controller still handles navigation; disabled inputs and the submission
check still prevent overlapping reconciliations. Mapping creation and verification use the same invalidation mechanism.
The version is held in the existing provider, without another state store or a backend approval change.

The controlled failures also reproduced raw server-body and browser-error display. The reconciliation client now extracts
JSON validation errors, gives actionable messages for server/network failures, and preserves cancellation handling.
A synthetic credential marker in the injected HTTP failure must never appear in the alert.

`backend/DealerSync.Tests/Api/DatabaseFailureApiTests.cs` adds seven cases. Three replace the database registration with
an unreachable loopback test connection and request mappings, history, or reconciliation. They require HTTP 503 without
approval payloads, driver details, or the synthetic password; captured application logs must also omit that password.
Two startup cases launch only their own API process in a temporary directory with an allowlisted environment and no
connection string or a whitespace-only value. Both must fail startup with the missing-configuration message. The test
terminates its own process on timeout and removes its temporary directory. Two further cases check an untrusted Host
header and an untrusted Origin. These tests never start or reset a development database.

Database outages previously escaped as exceptions on read endpoints or became a generic reconciliation conflict.
The API now returns a safe 503 for Npgsql failures, including EF Core wrappers, without logging an extra raw exception.
Its startup check rejects blank connection strings. The existing inventory-conflict HTTP 400 behavior is preserved.
The Host-header test reproduced acceptance of an arbitrary hostname; `AllowedHosts` now permits only `localhost`,
`127.0.0.1`, and `[::1]`. This follows [ASP.NET Core's host-filtering configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/host-filtering?view=aspnetcore-10.0).

### Local deployment review and limits

PostgreSQL's Compose port remains `127.0.0.1:5433:5432`. API launch profiles bind to `localhost`; the isolated runner
binds its API, frontend, and temporary PostgreSQL port to `127.0.0.1`. The development frontend retains
[Vite's default localhost binding and restricted host handling](https://vite.dev/config/server-options).
There is no permissive API CORS policy. Host filtering does not restrict listening interfaces or provide authentication:
keep local bindings, avoid `--host 0.0.0.0` and wildcard API URL overrides, and do not publish these services on a network.
No additional bind-address change is needed for the reviewed defaults. Environment overrides and the running process's
actual listener must still be checked when setting up a release machine; clean-install instructions remain Milestone 3.

The reviewed code does not enable EF sensitive-data logging or log connection strings. Credential assertions cover the
synthetic outage paths, not every possible future driver error or operator logging override. Keep database secrets in
local configuration and do not enable sensitive logging for dealership data. Fixtures and generated records are synthetic;
no private inventory, development `.env`, external Shopify service, or Docker volume is used by these checks.

Invalidation covers changes acknowledged through this React provider in one browser context. Other tabs, direct API or
SQL changes, and a server mutation whose response is lost are not tracked globally. Reconcile again after uncertain
mapping operations. Cancellation does not undo backend processing or historical summaries. `/api/health` remains a
liveness endpoint and does not establish database readiness. Authentication, network hosting, historical item-level
snapshots, and live Shopify import validation remain outside this milestone. Supplier trust and the Milestone 1 CSV
header/quantity contract are unchanged.

## Fixtures and test isolation

The approved-export fixtures use the documented Shopify All states identity columns. The mixed fixture includes default,
one-, two-, and three-option variants, exact location `Ottawa East`, a quoted multiline Unicode title, verified supplier
aggregation, approved zero, pending and missing suppliers, negative stock, a normalized suggestion, and unchanged stock.
The browser parses the actual approved download with `csv-parse`, checks all 12 supported update headers and six literal
expected rows, and compares it with the backend snapshot. Output expectations use spaced `Option 1 Name` /
`Option 1 Value` headers for all three options; importer tests and unspaced fixtures retain coverage of both input aliases.
See the [header review in the README](../README.md#approved-shopify-inventory-csvs) for the official documentation
inconsistency, unavailable template, and limits of compatibility verification. Selecting the Review filter still downloads
all six approved updates. Changing a source file removes the download.

Three additional browser cases block the whole file for a missing option value, duplicate variant identity under
different SKUs, or formula-like text. They confirm that approval counts remain visible, the approved download is disabled,
and diagnostics remain available. Existing mapping workflow tests now check the approved download after invalidation and
after rejected creation; overflow and malformed-upload tests check that no approved download is offered.

The fifth new browser test fetches a real API response and holds its delivery with a promise. It then navigates away,
creates a pending mapping through React, releases the response, and checks that no old result or approved download is
restored. While the response is held, it also checks that both upload inputs and the submit button are disabled.
File replacement during submission is therefore unavailable through the current UI; the test does not bypass disabled
controls to simulate a user action. There is no arbitrary delay or mocked approval. Aborting the browser request does
not claim to cancel backend processing or prevent the historical summary from being saved.

Backend coverage parses generated CSVs with CsvHelper independently of the application exporter. It checks identity
preservation through import and matching, one/two/three-option values, current/new quantities, malformed identity,
duplicate headers, exact location casing, ambiguous variants, changed current-stock data, size limits, and diagnostics
formula escaping. API tests use persisted mappings to check approved/export agreement, pending-supplier exclusion,
whole-file blocking, and overflow rejection. No external Shopify import is executed; compatibility claims are limited to
the [published inventory CSV contract](https://help.shopify.com/en/manual/inventory-and-locations/setup/inventory-csv)
and the representative synthetic cases.

The duplicate-SKU API regression uses two different handles and option values at `Ottawa` with the same SKU.
Four cases cover identical spelling, different case, surrounding whitespace, and both case and whitespace differences.
Each case first creates and verifies a saved supplier mapping, then confirms that a valid upload generates an approved
quantity of 7 and an export. Adding the second variant returns HTTP 400 with no result or approved-export payload,
including no partial export for an unrelated eligible product in the same upload. The existing matcher rejects duplicate
SKU/location pairs after trimming and ignoring case, before approval generation. This differs from punctuation-stripping
SKU normalization used to suggest matches; that suggestion rule does not define duplicate SKU identity.

`frontend/e2e/support.ts` creates an identifier namespace from the run ID, test ID, repeat index, and retry index. Each supplier test receives its own Shopify SKU and two supplier part numbers. The CSV templates in `frontend/e2e/fixtures/` contain explicit quantities for supplier, negative, zero, overflow, and text cases. The helper replaces `{{CASE}}` with that test's namespace. Only malformed-input templates use quantity placeholders; the tests supply the four literal invalid values above and valid values for the other upload.

The mapping helper calls the real `POST /api/sku-mappings` endpoint. New mappings must be unverified. For verified suppliers, it calls `PATCH /api/sku-mappings/{id}/verify`. A subsequent GET checks that the requested verification state was persisted, including when B must remain unverified.

Tests share the runner's disposable database, but do not share identifiers or assume an empty mappings table. Quantity and supplier cases each upload one Shopify product; the text/export case uploads five distinct exact-match products. They can run in any order without claiming another test's inventory or mappings. No per-test deletion or database reset is performed; all records disappear when the runner removes its own container. Keep the existing single-worker configuration for this stage. Step 2A was checked in both the declared and reversed supplier-scenario order.

The shared browser fixture blocks requests outside the isolated frontend origin for all 24 tests. The upload helpers use the real reconciliation page and read the response to its POST; they do not call reconciliation in place of browser interaction. The late-response test routes only the local reconciliation request and forwards it to the real backend before holding delivery.

Assertions check the backend summary and per-product contract, rendered quantities, contribution part numbers and quantities, and review messages. For blocked products, the detailed response has `proposedQuantity: null` and `inventoryUpdatesGenerated: 0`. Because the input contains one Shopify product, that zero applies to the tested SKU. The response does not expose a separate inventory-update collection. The Approved Updates filter must also hide the blocked row, preventing the calculated value from being presented as an approved update.

## Requirements

- macOS or Linux, with Node `24.22.1` from `.node-version` (bundled npm `11.19.1`).
- .NET SDK `10.0.401` (same-feature-band patches allowed by `global.json`). Run `dotnet tool restore` at the root for EF CLI `10.0.12`, then confirm `dotnet ef --version`.
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
5. Waits for a database-backed request through the frontend proxy, runs 24 Chromium tests, and checks API responses, visible results, and downloaded CSVs.
6. Stops the process groups it started and removes only the container whose identity and ownership label match this run. It does not remove Docker volumes.

No normal development server needs to be stopped. The runner does not reuse it, read the development `.env`, load private inventory files, or call Shopify. The browser blocks requests outside its test frontend origin. The CSV fixtures are committed under `frontend/e2e/fixtures/`.

## Populated database upgrade and CI

`backend/DealerSync.Tests/Persistence/MigrationUpgradeTests.cs` creates a test-owned PostgreSQL 17 container with tmpfs
storage and a loopback-only random port. It verifies the live database identity, then uses `IMigrator` to apply migrations
through `20261013024015_AddNormalizedUpdatesExcluded`. This is the actual final migration in tag `v0.2.0`
(`3fdebe75c3a25c07f4dd9eb2f65c45e8a5b3ce92`). The four historical migration bodies match that tag; no approximate schema
or `EnsureCreated` shortcut is used.

The test inserts verified and unverified mappings with distinct Shopify SKUs and checks that the old case-insensitive
Shopify uniqueness constraint rejects a second supplier. `Database.MigrateAsync()` applies the remaining real migration.
New contexts read back every original ID, identifier, verification state, and timestamp. A second Lightspeed supplier
can then share a Shopify SKU, including different case. Case-insensitive Lightspeed uniqueness, primary-key uniqueness,
and non-null identifiers must still reject invalid inserts without changing the three valid rows. Disposal removes
only this container and its temporary data. The existing API test fixture also binds its database port to loopback.

Run just the populated upgrade test from the root, with Docker running:

```bash
DOTNET_ENVIRONMENT=Testing ASPNETCORE_ENVIRONMENT=Testing \
ConnectionStrings__DefaultConnection='Host=127.0.0.1;Port=1;Database=dealersync_tests_unused;Username=test;Password=test' \
dotnet test --filter 'FullyQualifiedName~MigrationUpgradeTests'
```

For all independent checks, select the pinned Node version, then run:

```bash
dotnet tool restore
dotnet restore
dotnet build --no-restore
DOTNET_ENVIRONMENT=Testing ASPNETCORE_ENVIRONMENT=Testing \
ConnectionStrings__DefaultConnection='Host=127.0.0.1;Port=1;Database=dealersync_tests_unused;Username=test;Password=test' \
dotnet test --no-build --no-restore --logger 'trx;LogFileName=backend.trx' --results-directory TestResults
npm --prefix frontend ci
npm --prefix frontend run build
npm --prefix frontend run lint
npm --prefix frontend run test:e2e:types
# On macOS; on Linux use install --with-deps chromium instead.
(cd frontend && npx --no-install playwright install chromium)
npm --prefix frontend run test:e2e
git diff --check
docker ps -a --filter label=com.dealersync.e2e.run
docker ps -a --filter label=com.dealersync.upgrade-test
docker ps -a --filter label=org.testcontainers=true
```

The connection above is deliberately unreachable. Existing API fixtures replace it with their own PostgreSQL connection;
the browser runner creates another independent connection. Do not substitute a dealership or development connection.
`TestResults/`, browser reports, build output, and local data/backups are ignored by Git.

`.github/workflows/ci.yml` runs this stack on `ubuntu-24.04` for pull requests and pushes to `main`, `develop`, `feature/**`, or
`codex/**`, with optional manual dispatch. One job restores/builds/tests .NET, uses `npm ci` for the locked frontend,
builds/lints/type-checks it, installs Chromium with Linux libraries, and runs the existing E2E runner. It does not create
a second service-container database or test harness. Docker is checked before tests. The runner reserves random loopback
ports, polls readiness for up to 60 seconds, uses one Chromium worker with zero test retries, and supports Linux/macOS
process-group cleanup. Its `finally` block handles normal failures and signals. Hard termination can bypass cleanup;
GitHub-hosted jobs use disposable machines. An always-run read-only check detects remaining labelled test containers or
test-server processes. No broad Docker cleanup or volume deletion is performed.

Official Actions are pinned to immutable commits with release comments: checkout `v7.0.1`, setup-dotnet `v6.0.0`,
setup-node `v7.1.0`, and upload-artifact `v7.0.2`. Pins were resolved from their official release tags. The workflow has
read-only repository permissions, disables persisted checkout credentials and automatic npm caching, and uses no
repository secrets. It avoids `pull_request_target`. Always-run artifact upload retains backend TRX, browser HTML
reports, failure traces/screenshots, and the E2E log for seven days. These artifacts contain only synthetic test data;
never add real exports or connection files to the artifact paths. Dependency and browser downloads still require network
access. Scheduled dependency/action updates are not part of this milestone.

The [README startup workflow](../README.md#getting-started-locally) was exercised with a disposable database, explicit
Testing configuration, and the documented fixed API/frontend addresses. Actual listeners were verified at
`127.0.0.1:5233` and `127.0.0.1:5173`. The normal Vite configuration proxied a browser upload of the root synthetic
samples; it produced three approved rows and the downloaded CSV matched the backend snapshot. No development `.env`,
user-secret entry, persistent development volume, or real backup/restore was used for that verification. Development
user-secret setup is documented separately; operators must verify their own credentials and backup restoration.

Local `actionlint` 1.7.12 and `bash -n` checks passed during Milestone 3. Subsequent
[CI run 40578983483](https://github.com/AtiqueAqtab/DealerSync/actions/runs/40578983483) passed on Ubuntu 24.04.5 for
`9fb74eae4f300b1a95a5e826af2cf818c7f3e320`, including Linux browser setup, 151 backend tests, 24 browser tests, the
first-parent whitespace check, cleanup, and artifact upload. Checkout fetches two history levels so `HEAD^` exists;
the explicit first-parent comparison avoids treating a shallow boundary as a new root. This green result applies to
that committed SHA, not future documentation or merge revisions.

## Final release-candidate acceptance

Milestone 4 validated a fresh HTTPS clone of `9fb74ea` with SDK 10.0.401, EF tool 10.0.12, Node 24.22.1, and npm
11.19.1. All 151 backend tests (including the real populated v0.2 upgrade) and 24 Playwright tests passed. Backend and
frontend builds, clean dependency installation, lint, and TypeScript passed; the clone remained clean after ignored
artifacts were generated. The Linux CI SHA above is independently verified; its SDK 10.0.403 is allowed by the pinned
same-feature-band patch roll-forward policy.

An additional one-off browser exercise used the normal Vite configuration and fixed API/frontend ports against a
uniquely owned Compose container with tmpfs storage and a random loopback PostgreSQL port. Real React controls created
and verified suppliers, blocked an unverified relationship, approved combined 7 and zero, excluded negative stock,
invalidated earlier approvals, and generated a fresh export after verification changed. The download's exact identity,
location, current/new values, BOM, and headers were checked. Case/whitespace duplicate SKU, invalid quantity, and
formula-like identity inputs could not offer unsafe exports. No external browser requests or Shopify operations occurred.

The README's archive commands were exercised against that disposable deployment. A new empty restore database received
the archive with `pg_restore --exit-on-error`; all three mappings, seven summaries, and five migration records matched
exactly. The API restarted against the restored database and produced a fresh safe export. Real API stop/restart and
owned-database stop also confirmed safe failure behavior. All resources owned by this invocation were cleaned up without removing existing persistent volumes.
A separate E2E container created on October 25 was left untouched; the report records its identity and cleanup scope. No existing development database or user secrets were used.

No blocking production defect was found. A Node fetch Host-header probe was corrected to raw HTTP after a local control
confirmed the intended header was not sent by fetch; no application fix was justified. Documentation corrections cover
stale planned-feature/CI statements and the distinction between tested local acceptance and an approved public release.
See [RELEASE_READINESS.md](RELEASE_READINESS.md) for the criterion matrix, evidence scope, exact commands, and checklist.
Live Shopify import, cross-context invalidation, real dealership recovery, and fresh-workstation user-secret entry remain
unverified or unsupported as explicitly recorded there. The acceptance exercise did not add another committed test
framework or change the 151/24 suite baseline.

## Results and failures

Milestone 3 passed 151 backend tests (including the populated upgrade regression) and all 24 Playwright tests. Backend
restore/build, clean frontend install/build, lint, TypeScript, whitespace, workflow syntax/shell parsing, and setup/proxy
verification passed locally. The backend build has zero warnings. Test-container and process cleanup checks passed.
The first upgrade run passed with two nullable warnings in the port-binding code; explicit guards removed those warnings
before the full build. Local workflow validation does not establish a successful GitHub CI run.


Milestone 2 passed all 150 backend tests and 24 Playwright tests, including the 143/19 Milestone 1 baseline. Backend
build, frontend production build with environment-file loading disabled, ESLint, test TypeScript checks, and Git
whitespace checks passed. The initial browser run reproduced three production failures (server error display, network
error display, and stale approval restoration); its fourth failure was an incorrect test expectation that omitted
unmatched products from Requires Review. Correcting that assertion required no table change. Initial database test
configuration was corrected before relying on its results; valid regressions then reproduced blank-setting startup,
unhandled/misclassified database failures, and arbitrary Host acceptance. The focused seven-case suite and full suites
passed after the fixes. Cleanup checks found no owned test containers or API/frontend test processes remaining.

The safe-export milestone, including the four duplicate-SKU API regression cases, passed 19 browser tests and
143 backend tests, including the recorded 14/104 baseline. The duplicate-SKU review required no production changes.
The backend build, frontend production build with environment-file loading disabled, lint, test TypeScript checks,
and `git diff --check` passed. Docker was initially stopped, so the first backend run could not initialize integration
fixtures; the full suite passed after Docker Desktop started. The runner's labelled container and test server processes
were absent after completion, and no volumes were removed. No dependencies or migrations were added.

Step 3A validation passed all 14 browser tests and 104 existing backend tests, the production build with environment-file loading disabled, lint, test TypeScript checks, and `git diff --check`. The runner cleaned up after both the reproduced regression failure and the fixed run. A separate Docker/process check found no remaining E2E containers or test servers. No volumes were removed. Only the mapping page's successful-creation callback changed in production; no backend code or approval rules changed.

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

Use the root-level command block under [Populated database upgrade and CI](#populated-database-upgrade-and-ci) for the
complete checks. Backend integration tests create their own PostgreSQL Testcontainers. The browser runner uses a separate
database because Chromium needs an API listening on a real network port; the xUnit in-process server is not reachable
from the browser. Build/lint, backend tests, and browser tests establish different behavior. These 24 browser tests do
not by themselves establish release readiness or live Shopify compatibility.

The normal `npm --prefix frontend run build` now disables `.env` loading through the shared Vite configuration. E2E-only
build variables and the test Vite configuration are unnecessary for a production build.
