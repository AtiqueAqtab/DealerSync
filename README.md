# DealerSync

DealerSync is a C#/.NET application I'm building to make inventory reconciliation between **Lightspeed EVO** and
**Shopify** easier and more reliable.

The idea came from a problem I ran into at work. Our dealership uses Lightspeed EVO to manage inventory and Shopify for
the online store, but keeping the inventory numbers consistent between the two systems involves a lot of manual work.

One of the bigger problems is that product identifiers don't always match. Sometimes the difference is as simple as a
hyphen, while other times the SKUs are completely different.

I started DealerSync to automate the repetitive parts of that process without assuming every potential match is correct.
The application compares inventory exports, identifies SKU relationships, finds quantity differences, and generates
reports that can be reviewed before making changes to Shopify.

## Latest stable release

[v0.3.0](https://github.com/AtiqueAqtab/DealerSync/releases/tag/v0.3.0) is the latest stable release, published from
commit `7b1d800`. See the [v0.3.0 release notes](docs/RELEASE_NOTES_v0.3.0.md) for delivered features and known limitations.

## Previous release

### v0.2.0 — Reconciliation API and Saved SKU Mappings

The original version of DealerSync was a command-line application that compared two CSV files.

Version 0.2.0 built on that by adding an ASP.NET Core API, PostgreSQL database, and support for manually verified SKU
mappings.

Some of its main features included:

- Importing Lightspeed EVO 505 inventory reports and Shopify inventory exports
- Matching SKUs using exact, normalized, and manually verified relationships
- Saving SKU mappings in PostgreSQL
- Remembering verified mappings between reconciliation runs
- Detecting duplicate SKUs and conflicting inventory assignments
- Comparing inventory quantities between both systems
- Generating Shopify-compatible inventory update CSV files
- Excluding matches that aren't safe to use for inventory updates
- Providing detailed reconciliation results in JSON and CSV
- Saving reconciliation history
- Automated unit and integration testing

DealerSync still works with CSV imports and exports. Direct Shopify API integration is something I plan to add later.

## v0.3.0 dashboard

v0.3.0 adds a React/TypeScript dashboard and multi-supplier reconciliation. The release includes CSV uploads, searchable results, supplier contributions, review warnings, diagnostics downloads, and mapping creation and verification.

Release validation is complete for the trusted, single-user local CSV scope. The History and Settings routes are placeholders. The API saves run summaries; it does not save historical item-level reconciliation details.

The full plan contains 15 milestones, from v0.1.0 through the exploratory v4.0.0 Lightspeed integration. See [docs/ROADMAP.md](docs/ROADMAP.md) for scope, status, dependencies, and planned work. Read [AGENTS.md](AGENTS.md) before contributing or starting an agent-assisted task.

## Approved Shopify inventory CSVs

The web reconciliation page has separate **Download Diagnostics CSV** and **Download Approved Shopify CSV** controls.
Diagnostics are for review and must not be imported into Shopify. The approved file contains all updates approved by the
backend for that reconciliation, regardless of the table's search, page, or filter. It does not perform Shopify API calls.

Use Shopify's **All states** inventory export for one location. Keep the source `Handle`, `SKU`, `Location`,
`On hand (current)`, and every applicable option value. Unused second and third options may have blank or absent columns.
Optional titles and option names are retained when present. Both `Option1 Value` and `Option 1 Value` header styles are accepted, with the
same aliases for names and options 2 and 3. Do not include both aliases for the same field.

A default variant must already contain its source `Default Title` value. A variant with two or three options must retain
each applicable value. DealerSync does not create handles, infer option values, or treat SKU alone as Shopify's variant
identity. Every retained row needs a nonblank SKU; duplicate SKUs, duplicate headers, and conflicting assignments are
rejected. Integer quantities are required; fractional, blank, out-of-range, and `Not stocked` quantities are unsupported.

The download uses this supported All states update subset:

```text
Handle,Title,Option 1 Name,Option 1 Value,Option 2 Name,Option 2 Value,Option 3 Name,Option 3 Value,SKU,Location,On hand (current),On hand (new)
```

It preserves exact source identity, title, SKU, and location, writes the original current stock, and puts only the
backend-approved quantity in `On hand (new)`, including an approved `0`. Optional fields such as HS code, country of
origin, bin name, and read-only stock states are omitted rather than written blank. This file updates inventory, not
product details. Matching still normalizes surrounding whitespace; the download retains the original source strings.

Shopify uses the current/new quantity pair to check for stock changes since export. Do not clear `On hand (current)`
or copy calculated quantities into the file. Review the complete download before manually importing it. See
[Shopify's inventory CSV requirements](https://help.shopify.com/en/manual/inventory-and-locations/setup/inventory-csv).
These checks follow that documented contract; no live-store import has been performed or authorized.

Header review (2026-10-23): output uses the spaced `Option 1 Name` / `Option 1 Value` style for all three options,
matching the detailed column table in the linked Shopify documentation. Its required-fields summary instead uses
`Option1 Value`. The [official template](https://help.shopify.com/cdn/shopifycloud/product-knowledge/production/csv/inventory_template.csv)
returned HTTP 403 during verification, so its header spelling could not be checked. The detailed table is the most
defensible output reference available; the documentation inconsistency remains unresolved. Both input spellings remain
accepted. A real Shopify import test is still needed to establish live import compatibility.

The export policy is all-or-nothing. If any retained source identity is incomplete, contains an option gap, duplicates
another product/variant/location identity, or differs only by ambiguous identity casing, the whole download is blocked
with a visible reason. This includes identity checks on rows whose quantities were excluded. Diagnostics and approval
counts remain available, but no partial inventory file is advertised. Minimal older CSVs can still produce diagnostics
when their required reconciliation fields are present; without complete export identity they cannot produce an update
download. The file is also blocked if it exceeds Shopify's 15 MB limit. Keep the source export unmodified: without
contacting Shopify, DealerSync cannot detect option metadata removed from both its name and value columns.

Backend-generated diagnostics escape formula-like text for spreadsheet review. For the Shopify import file,
formula-like source text blocks export instead of adding an apostrophe that could change an identifier or option value.
This conservative policy can block legitimate text beginning with `=`, `+`, `-`, or `@`, including after whitespace.
Review such input; do not bypass the warning by treating diagnostics as an import file. Spreadsheet applications can
also reformat identifiers, so retain an untouched download and avoid saving a reformatted copy over it.

The detailed API response includes `approvedInventoryExport` with the server-generated `csv`, an optional `error`, and
the approved `updateCount`. If export validation fails, `csv` is null and the count still describes eligible updates,
not a partial exported file. With zero approved updates, CSV is null without an export error. React downloads the
backend's result snapshot; it never regenerates approval from combined inventory. Mapping changes and file changes
clear that snapshot. Leaving the reconciliation page cancels its pending browser request and discards a late response.
This does not provide cross-tab mapping synchronization or cancel already-running backend work.

The standalone CLI does **not** load saved PostgreSQL mappings. Its output is not equivalent to the mapping-aware web
workflow, even when it has enough metadata to write a CSV. Use the web workflow for approved multi-supplier exports.

## Why I Built It

At work, Lightspeed EVO is our main inventory management system, while Shopify handles the online store.

The two systems don't always use the same product identifiers.

For example:

```text
Lightspeed: ABC-123
Shopify:    ABC123
```

That's a fairly simple difference to handle, but there are other situations that make reconciliation more complicated:

- A product exists in Shopify but hasn't been received into Lightspeed
- Shopify has variants that don't appear in the Lightspeed inventory report
- The same SKU appears multiple times
- Lightspeed reports a negative inventory quantity
- Products have different SKUs in each system
- Supplier product data uses different formats

I wanted something that could identify these problems automatically instead of having to manually compare thousands of
spreadsheet rows.

At the same time, inventory accuracy matters. An incorrect update could make a product available online when it isn't
actually in stock.

Because of that, I've tried to make DealerSync cautious about which matches it trusts.

## How It Works

DealerSync takes two inventory exports: one from Lightspeed EVO and one from Shopify.

It imports the records, checks for data issues, matches products by SKU, and compares their quantities.

```text
Lightspeed EVO CSV ───┐
                      │
                      ▼
                 DealerSync
                      ▲
                      │
Shopify CSV ──────────┘
                      │
                      ▼
                Validate Data
                      │
                      ▼
                  Match SKUs
                      │
                      ▼
              Compare Quantities
                      │
                      ▼
               Apply Safety Rules
                      │
             ┌────────┴────────┐
             ▼                 ▼
       Inventory Updates    Diagnostics
             │                 │
             ▼                 ▼
         Shopify CSV       JSON / CSV
```

The API can also save a summary of each reconciliation in PostgreSQL, making it possible to review previous runs.

## SKU Matching

One of the main parts of DealerSync is its SKU matching logic.

There are currently three ways products can be matched.

### Exact Matching

If a Lightspeed part number and Shopify SKU are the same, ignoring case, they're considered an exact match.

```text
Lightspeed: KLIM-001
Shopify:    KLIM-001
```

Exact matches are checked first.

### Verified Matching

Sometimes a product has completely different identifiers in Lightspeed and Shopify.

For example:

```text
Lightspeed: BMW-12345
Shopify:    BMW-JACKET-12345
```

DealerSync allows these relationships to be saved manually.

Each mapping starts out unverified. Once it's been confirmed, it can be marked as verified and used in future
reconciliations.

The mappings are stored in PostgreSQL, so they don't need to be entered again every time new inventory files are
uploaded.

### Normalized Matching

Some SKUs are effectively the same but use different formatting.

For example:

```text
ABC-123
ABC 123
ABC_123
ABC.123
ABC/123
```

DealerSync removes common separators when comparing SKUs, so these all normalize to:

```text
ABC123
```

Normalized matches are useful for identifying possible relationships.

However, they're not automatically included in inventory update files. The relationship needs to be manually verified
first.

I made this decision because two similar-looking SKUs could represent different sizes, colours, or variants of the same
product.

### Matching Priority

When reconciling inventory, DealerSync checks for matches in this order:

1. Exact
2. Verified
3. Normalized
4. Unmatched

The matching service also checks whether multiple Shopify products are trying to use the same Lightspeed inventory
record.

If there's a conflict, reconciliation stops rather than choosing one of the matches.

## Inventory Safety

Since DealerSync is intended to work with real inventory data, I added several checks to reduce the risk of generating
incorrect updates.

### Unmatched Products

If DealerSync can't find a matching Lightspeed part number for a Shopify SKU, it leaves that product unmatched.

No inventory update is generated for it.

### Negative Quantities

Lightspeed sometimes reports negative inventory quantities.

DealerSync excludes these from inventory updates and flags them in the diagnostics.

### Normalized Matches

Even if two SKUs match after normalization, DealerSync doesn't automatically trust that relationship for inventory
updates.

Normalized matches appear in the reconciliation results, but they're excluded from update generation until verified.

### Duplicate and Conflicting Matches

DealerSync checks for duplicate inventory records and conflicting SKU assignments.

It also detects situations where multiple proposed updates target the same Shopify SKU and location.

If the quantities conflict, the application throws an error rather than guessing which one is correct.

### Shopify Locations

The current version only supports one Shopify inventory location per reconciliation.

If an uploaded file contains multiple locations, DealerSync rejects it.

### Which Matches Can Generate Updates?

| Match Type        | Eligible for Inventory Updates |
|-------------------|--------------------------------|
| Exact             | Yes                            |
| Verified          | Yes                            |
| Normalized        | No                             |
| Unmatched         | No                             |
| Negative quantity | No                             |

Even when a match is eligible, DealerSync only generates a proposed inventory change. It doesn't automatically push that
change to Shopify.

## Reconciliation Diagnostics

In addition to generating inventory updates, DealerSync produces diagnostic reports.

These reports help identify products that couldn't be matched or need to be reviewed.

Each result includes information such as:

- Shopify SKU and product title
- Lightspeed part number, if matched
- Match type
- Shopify location
- Current Shopify quantity
- Proposed quantity
- Whether there's an inventory difference
- Diagnostic status
- Whether the item requires review

### Diagnostic Statuses

| Status              | Meaning                                       |
|---------------------|-----------------------------------------------|
| `Ready`             | Exact or verified match with a valid quantity |
| `ReviewRecommended` | Normalized match that should be verified      |
| `MappingRequired`   | No matching Lightspeed product                |
| `InvalidQuantity`   | Negative Lightspeed quantity                  |

For example:

| Shopify SKU      | Lightspeed SKU | Match Type | Status            |
|------------------|----------------|------------|-------------------|
| BMW-123          | BMW-123        | Exact      | Ready             |
| KLIM123          | KLIM-123       | Normalized | ReviewRecommended |
| REVIT-JACKET-456 | REV-456        | Verified   | Ready             |
| TRIUMPH-789      | —              | Unmatched  | MappingRequired   |

The reports can be returned as detailed JSON through the API or downloaded as a CSV file.

These diagnostic CSV files are meant for reviewing inventory data. They aren't formatted for direct import into Shopify.

## Reconciliation Statistics

Each reconciliation also produces a summary showing how well the inventory records matched.

The summary includes:

- Number of Lightspeed items
- Number of Shopify items
- Exact matches
- Verified matches
- Normalized matches
- Unmatched Shopify items
- Inventory quantity differences
- Inventory updates generated
- Negative quantities excluded
- Normalized updates excluded
- Shopify matching coverage

For example:

```text
Reconciliation Summary
----------------------
Lightspeed items:             1000
Shopify items:                800

Exact matches:                500
Verified matches:             150
Normalized matches:            50
Unmatched:                    100

Shopify coverage:             87.5%

Inventory differences:        200
Normalized updates excluded:   20
Negative quantities excluded:   5
Inventory updates generated:  175
```

These are example numbers.

One important distinction is that finding an inventory difference doesn't necessarily mean DealerSync will generate an
update for it.

A normalized match, for example, can count toward the matching coverage while still being excluded from the inventory
update file.

## Project Structure

DealerSync is split into five .NET projects.

```text
DealerSync/
│
├── backend/
│   ├── DealerSync.Api/
│   ├── DealerSync.Core/
│   ├── DealerSync.Infrastructure/
│   ├── DealerSync.Runner/
│   └── DealerSync.Tests/
│
├── samples/
├── compose.yaml
└── README.md
```

### DealerSync.Core

Contains the main matching and reconciliation logic.

This includes:

- Inventory models
- SKU normalization
- Exact, normalized, and verified matching
- Conflict detection
- Inventory update generation
- Diagnostic classification

The core project doesn't depend on PostgreSQL or ASP.NET Core.

### DealerSync.Infrastructure

Handles CSV files, reconciliation services, and database persistence.

It contains:

- Lightspeed CSV importer
- Shopify CSV importer
- Shopify inventory CSV exporter
- Diagnostics CSV exporter
- Entity Framework Core database context
- PostgreSQL entities and migrations
- SKU mapping repository
- Reconciliation history repository

### DealerSync.Api

An ASP.NET Core Minimal API that exposes reconciliation and SKU mapping functionality through HTTP endpoints.

### DealerSync.Runner

The original command-line application.

It can still run reconciliation directly using local CSV files without starting the API.

### DealerSync.Tests

Contains unit tests and integration tests for the different parts of the application.

The integration tests use Testcontainers to start temporary PostgreSQL databases, so they don't modify the development
database.

## Getting started locally

The supported workflow is a trusted, single-user macOS or Linux workstation. PostgreSQL, the API, and Vite listen on
loopback. There is no authentication; do not expose them through a public hostname, port forwarding, or wildcard binds.
Vite serves the local UI; this is not a cloud deployment or packaged desktop release.

### Tools and installation

Install Git, Docker Desktop (or Docker Engine with Compose v2 on Linux), and these tools:

| Tool | Supported version |
| --- | --- |
| .NET SDK | `10.0.401` from `global.json`; later patches in the same feature band are allowed |
| Node.js | `24.22.1` LTS from `.node-version` |
| npm | `11.19.1`, bundled with the pinned Node release |
| EF CLI | `10.0.12`, restored from `.config/dotnet-tools.json` |
| PostgreSQL | `postgres:17`, started by Docker Compose |

Use a current Compose v2 that supports `up --wait`. With a Node version manager, install/select the version in
`.node-version` before running npm. The commands below are for a POSIX shell, from the repository root unless stated.

```bash
git clone https://github.com/AtiqueAqtab/DealerSync.git
cd DealerSync
git switch --detach v0.3.0 # Published stable release
dotnet --version
node --version
npm --version
dotnet tool restore
dotnet ef --version
dotnet restore
dotnet build
npm --prefix frontend ci
```

### Database configuration and startup

Create an ignored `.env` at the repository root. Set `POSTGRES_PASSWORD` to a long random local password; do not use
real dealership credentials or commit this file. Compose reads `.env`; the API uses .NET configuration independently.

```text
POSTGRES_PASSWORD=REPLACE_WITH_YOUR_LOCAL_RANDOM_PASSWORD
```

For an existing database, retain its existing password. Changing this variable does not change the password stored in
an initialized PostgreSQL volume. First-time setup is different from upgrading a populated installation.

```bash
docker compose up -d --wait postgres
```

This retains the existing named volume and publishes PostgreSQL only at `127.0.0.1:5433`. Never use `down -v` or delete
a volume to fix a startup problem. Store the matching API connection string in .NET user secrets, replacing the
placeholder locally. User secrets are local development storage, not an encrypted production secret manager; avoid
putting the actual command/password in shared terminal logs or shell history.

```bash
dotnet user-secrets set --project backend/DealerSync.Api \
  "ConnectionStrings:DefaultConnection" \
  "Host=127.0.0.1;Port=5433;Database=dealersync;Username=dealersync;Password=REPLACE_WITH_YOUR_LOCAL_RANDOM_PASSWORD"
```

The API requires `ConnectionStrings:DefaultConnection` (or the environment variable
`ConnectionStrings__DefaultConnection`). Development loads this project's user secrets. Tests use `Testing` and explicit
temporary connections. Neither the API nor Vite reads the root `.env`; only Compose does. Vite disables `.env` loading
and proxies relative `/api` requests directly to the local API. No frontend secret or `VITE_*` variable is needed.

### Apply migrations and start the API

For an existing database, make and verify a backup before applying migrations; see the upgrade section below.
The following command uses Development configuration to load user secrets and applies pending migrations forward.
It also initializes a fresh database schema. Startup itself does not apply migrations.

```bash
DOTNET_ENVIRONMENT=Development ASPNETCORE_ENVIRONMENT=Development \
dotnet ef database update --project backend/DealerSync.Infrastructure \
  --startup-project backend/DealerSync.Api
```

Start the API in one terminal, leaving it running:

```bash
DOTNET_ENVIRONMENT=Development ASPNETCORE_ENVIRONMENT=Development \
dotnet run --project backend/DealerSync.Api --no-launch-profile --urls http://127.0.0.1:5233
```

This explicit address limits the listener; `AllowedHosts` separately permits `localhost`, `127.0.0.1`, and `[::1]`.
The existing launch profiles also use loopback, but the command above avoids profile selection and development HTTPS
certificate requirements. Do not override the listener with `0.0.0.0`, `*`, or a public hostname. No permissive API CORS
policy is enabled; neither CORS nor Host filtering provides authentication.

Check a database-backed endpoint without printing saved mappings:

```bash
curl --fail --silent --output /dev/null http://127.0.0.1:5233/api/sku-mappings
```

`/api/health` is liveness only: it can return 200 while PostgreSQL is unavailable. Its legacy version field is not a
release-readiness signal. Development additionally exposes OpenAPI; Testing does not load development user secrets.

### Start the frontend and try synthetic inventory

In a second terminal at the repository root:

```bash
npm --prefix frontend run dev
```

Open `http://127.0.0.1:5173`, then Reconciliation. Vite binds to that loopback address, fails clearly if port 5173 is busy,
and proxies `/api` to `http://127.0.0.1:5233`.

Upload `samples/lightspeed_inventory_sample.csv` and `samples/shopify_inventory_sample.csv` in their respective controls.
These files are synthetic. On a fresh database with no additional mappings for those SKUs, expect four exact matches:
KLIM-001 goes from 3 to 4, BMW-003 from 4 to 2, TRIUMPH-004 from 1 to approved 0, and REVIT-002 stays at 7 without an update.
There are three eligible updates. Download **Approved Shopify CSV**, and check its original current quantities and
backend-approved new quantities, including zero. The separate diagnostics file must not be imported into Shopify.
This exercise generates a file locally; it does not contact Shopify or establish live import compatibility.

Stop Vite and the API with Ctrl+C in their respective terminals. Stop PostgreSQL without removing its data:

```bash
docker compose stop postgres
```

### Populated database upgrades and recovery

The `v0.2.0` tag (`3fdebe75`) includes migrations through
`20261013024015_AddNormalizedUpdatesExcluded`. Current v0.3 adds
`20261017023848_AllowMultipleSuppliersPerShopifySku`. It removes uniqueness from the Shopify SKU index, preserving
case-insensitive Lightspeed uniqueness, required identifiers, and existing mapping data. The automated upgrade test
uses these actual migrations, not a substitute schema; see [testing](docs/TESTING.md#populated-database-upgrade-and-ci).

Before upgrading a populated local database, stop the API/UI, confirm the intended database/container, and back up to
ignored local storage. With the reviewed Compose configuration, the database is `dealersync` in its `postgres` service:

```bash
mkdir -p data/backups
docker compose exec -T postgres pg_dump -U dealersync -d dealersync -Fc > data/backups/pre-v030.dump
docker compose exec -T postgres pg_restore --list < data/backups/pre-v030.dump > data/backups/pre-v030.contents.txt
```

Proceed only if both commands succeed. Listing a dump checks its archive structure; it is not a restore drill. Keep a
protected copy outside the working directory and test restoration into a separate empty database before relying on a
backup for important data. Backups can contain private mappings; never commit or upload them as CI artifacts.
Apply the forward migration command above, restart, and check mappings and a synthetic reconciliation.

Downgrading is unsupported once several suppliers share a Shopify SKU: the old unique index cannot represent those
rows. Do not run destructive rollback commands or edit migration-history entries. Recovery requires an operator-reviewed
restore of the pre-upgrade backup into a separate database with the matching application revision, followed by validation
before changing connections. The automated test does not constitute a backup/restore drill for a real installation.

### Common failures and independent checks

| Symptom | Check/recovery |
| --- | --- |
| SDK or `dotnet ef` missing | Install the SDK selected by `global.json`, then run `dotnet tool restore` from the root. |
| Frontend `package.json` missing | Run the documented `npm --prefix frontend ...` commands from the root. |
| Database password missing/mismatched | Set the ignored Compose `.env` and matching API secret. Preserve the password for an existing volume. |
| Missing API connection string | Use the Development commands above; set the local user secret. Testing requires an explicit test connection. |
| API returns 503 | Check PostgreSQL readiness, connection settings, and applied migrations; do not reset data. |
| API is alive but frontend fails | Check the database-backed endpoint and API port 5233, then confirm Vite's proxy. |
| Port already occupied | Identify the existing process; do not kill an unrelated server. For an alternate frontend port, use `npm --prefix frontend run dev -- --port 5174`. |
| Invalid Host response | Use the supported loopback URL; Host filtering is not fixed by exposing another interface. |
| HTTP-only API logs missing HTTPS redirect port | Expected for this local HTTP workflow; real requests and readiness checks must still succeed. |
| Migration fails | Preserve the backup, inspect the error and migration history, resolve its cause, then retry forward migrations. Do not delete volumes or history entries. |

Keep sensitive EF logging disabled, and do not share connection strings or real inventory logs. The legacy CLI does not
load saved PostgreSQL mappings and is not equivalent to the mapping-aware web workflow.

[docs/TESTING.md](docs/TESTING.md) has the full independent test commands and CI details. Integration/E2E tests use their
own disposable databases. Revision `9fb74ea` passed 151 backend tests and 24 Playwright tests locally and in
[GitHub CI on Ubuntu 24.04](https://github.com/AtiqueAqtab/DealerSync/actions/runs/40578983483).
The [release-validation report](docs/RELEASE_READINESS.md) records the pre-release fresh-clone acceptance,
synthetic backup/restore drill, known limits, and review checklist. See the
[published v0.3.0 release](https://github.com/AtiqueAqtab/DealerSync/releases/tag/v0.3.0) and
[release notes](docs/RELEASE_NOTES_v0.3.0.md).

## API Endpoints

The API provides endpoints for reconciliation, history, and SKU mapping management.

### Reconciliation

| Method | Endpoint                            | Description                         |
|--------|-------------------------------------|-------------------------------------|
| GET    | `/api/health`                       | Check API health                    |
| GET    | `/api/reconciliation/status`        | Check reconciliation service        |
| POST   | `/api/reconciliation`               | Upload CSV files and get a summary  |
| POST   | `/api/reconciliation?detailed=true` | Get detailed reconciliation results |
| POST   | `/api/reconciliation?format=csv`    | Download diagnostic CSV             |
| GET    | `/api/reconciliation/history`       | View recent reconciliations         |
| GET    | `/api/reconciliation/history/{id}`  | View a specific reconciliation      |

### SKU Mappings

| Method | Endpoint                        | Description            |
|--------|---------------------------------|------------------------|
| POST   | `/api/sku-mappings`             | Create a SKU mapping   |
| GET    | `/api/sku-mappings`             | List saved mappings    |
| GET    | `/api/sku-mappings/{id}`        | Get a specific mapping |
| PATCH  | `/api/sku-mappings/{id}/verify` | Verify a mapping       |

### Creating a Mapping

Example request:

```http
POST /api/sku-mappings
Content-Type: application/json
```

```json
{
  "lightspeedPartNumber": "BMW-12345",
  "shopifySku": "BMW-JACKET-12345"
}
```

New mappings are unverified by default.

### Running Reconciliation Through the API

The reconciliation endpoint accepts two CSV files using `multipart/form-data`.

The field names are:

- `lightspeedFile`
- `shopifyFile`

Example:

```bash
curl -X POST "http://localhost:<port>/api/reconciliation?detailed=true" \
  -F "lightspeedFile=@path/to/lightspeed.csv" \
  -F "shopifyFile=@path/to/shopify.csv"
```

Replace `<port>` with the port printed when you start the API.

To download a diagnostics report:

```bash
curl -X POST "http://localhost:<port>/api/reconciliation?format=csv" \
  -F "lightspeedFile=@path/to/lightspeed.csv" \
  -F "shopifyFile=@path/to/shopify.csv" \
  -o dealersync_diagnostics.csv
```

**Note:** The API doesn't currently have authentication or authorization. It's intended for local development and
shouldn't be exposed publicly without additional security measures.

## Running the CLI

The original command-line workflow still works:

```bash
dotnet run --project backend/DealerSync.Runner -- \
  "path/to/lightspeed.csv" \
  "path/to/shopify.csv"
```

The CLI processes both inventory files and generates local output files.

Saved PostgreSQL SKU mappings are currently used by the API workflow rather than the CLI.

## Database

DealerSync uses PostgreSQL with Entity Framework Core.

The database currently stores two main types of information.

### SKU Mappings

Each mapping includes:

- Lightspeed part number
- Shopify SKU
- Verification status
- Creation timestamp
- Last updated timestamp

Lightspeed part numbers are case-insensitive and unique across mappings. Multiple Lightspeed part numbers may share one Shopify SKU after the multi-supplier migration is applied.

### Reconciliation History

After a successful reconciliation, the API saves a summary containing information such as:

- When the reconciliation ran
- Number of imported inventory records
- Exact, normalized, and verified match counts
- Number of unmatched records
- Inventory differences
- Updates generated
- Excluded updates
- Shopify matching coverage

Individual matched inventory records are returned during reconciliation, but aren't currently saved as part of
historical runs.

## Data Privacy

I developed DealerSync around inventory problems I've encountered at work, but actual dealership inventory exports
aren't included in the repository.

Local inventory files are stored in:

```text
data/
```

That directory is ignored by Git.

Tests use sample data instead of private dealership inventory.

Database credentials are also kept out of source control using environment configuration or .NET user secrets.

## Tech Stack

- **Language:** C#
- **Framework:** .NET 10, ASP.NET Core Minimal APIs
- **Database:** PostgreSQL, Entity Framework Core, Npgsql
- **Data processing:** CsvHelper
- **Testing:** xUnit, Testcontainers
- **Development:** JetBrains Rider, Git, GitHub
- **Containerization:** Docker Compose

## Roadmap

The complete plan is maintained in [docs/ROADMAP.md](docs/ROADMAP.md). The summary below records the earlier plan. Use the detailed roadmap for current completion status and the expanded 15 milestones.

v0.3.0 is the latest stable release. The v0.1.0 and v0.2.0 summaries below are historical; the unchecked v0.2 final-release item is retained from the earlier plan.

The earlier dashboard plan included drag-and-drop uploads and a dedicated History page. The supplied v0.3.0 milestone requires CSV uploads but does not explicitly include those two features. Confirm their release scope before adding them. Current uploads use file selection, and the dashboard shows recent saved summaries.

### v0.1.0 — CSV Reconciliation MVP

- [x] Import Lightspeed EVO inventory
- [x] Import Shopify inventory
- [x] Exact and normalized SKU matching
- [x] Inventory quantity comparison
- [x] Duplicate protection
- [x] Negative quantity handling
- [x] Shopify inventory update CSV generation
- [x] Automated tests

### v0.2.0 — API and Persistence

- [x] ASP.NET Core API
- [x] PostgreSQL database
- [x] Entity Framework Core migrations
- [x] Persistent SKU mappings
- [x] Manual SKU verification
- [x] Verified SKU reconciliation
- [x] Reconciliation history
- [x] Detailed JSON results
- [x] Downloadable diagnostics CSV
- [x] Diagnostic classifications
- [x] Safer inventory update filtering
- [x] Automated PostgreSQL integration tests
- [ ] Final release

### v0.3.0 — Web dashboard

The implemented dashboard supports CSV file selection/upload, reconciliation summaries, searchable and paginated
inventory results, diagnostic/Approved Updates filters, supplier mapping creation/verification, and separate diagnostic
and approved inventory downloads. Multiple verified suppliers can contribute to one Shopify SKU.

The dashboard shows recent saved summaries. Dedicated History and Settings routes remain placeholders; drag-and-drop
is not implemented. v0.3.0 is published from release commit `7b1d800`. See the
[release notes](docs/RELEASE_NOTES_v0.3.0.md) for safety and compatibility limits.

### Future — Supplier Integration

Another part of the project I'd eventually like to work on is supplier catalog integration.

Different suppliers provide their product data through CSV, JSON, XML, and sometimes ZIP files containing spreadsheets
and images.

I'd like DealerSync to support importing these formats into a common product model.

That could make it possible to identify new products, find missing Shopify variants, compare supplier information, and
selectively add products to the online store.

### Future — Shopify API Integration

Longer-term features I'm considering include:

- Shopify Admin API integration
- Direct inventory synchronization
- Scheduled reconciliation
- Multi-location inventory
- Audit logging
- Product catalog health checks

Before implementing automatic inventory updates, I want to make sure the reconciliation and validation process is
reliable enough to handle real inventory safely.

## About the Project

DealerSync started as a way to solve a repetitive problem I encountered at work.

It's also become a project where I can apply what I'm learning in computer science to something that has a practical
use.

So far, I've been using it to get more experience with C#, .NET, REST APIs, database design, automated testing, and
working with real-world data.

My long-term goal is to turn it into a tool that can reduce manual inventory work while still giving users control over
which changes are actually applied.
