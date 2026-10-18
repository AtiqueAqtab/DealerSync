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

## Latest Tagged Version

### v0.2.0 — Reconciliation API and Saved SKU Mappings

The original version of DealerSync was a command-line application that compared two CSV files.

Version 0.2.0 builds on that by adding an ASP.NET Core API, PostgreSQL database, and support for manually verified SKU
mappings.

Some of the main features include:

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

## Current Development

v0.3.0 adds a React/TypeScript dashboard and multi-supplier reconciliation. The current feature branch includes CSV uploads, searchable results, supplier contributions, review warnings, diagnostics downloads, and mapping creation and verification.

The core features are implemented. Final end-to-end acceptance and release preparation remain open. The History and Settings routes are placeholders. The API saves run summaries; it does not save historical item-level reconciliation details.

The full plan contains 15 milestones, from v0.1.0 through the exploratory v4.0.0 Lightspeed integration. See [docs/ROADMAP.md](docs/ROADMAP.md) for scope, status, dependencies, and remaining v0.3.0 work. Read [AGENTS.md](AGENTS.md) before contributing or starting an agent-assisted task.

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

## Getting Started

### Requirements

- .NET 10 SDK
- Docker Desktop or another compatible Docker environment
- EF Core command-line tools
- Git

### Clone and Build

```bash
git clone https://github.com/AtiqueAqtab/DealerSync.git
cd DealerSync

dotnet restore
dotnet build
```

### Set Up PostgreSQL

DealerSync uses PostgreSQL to store SKU mappings and reconciliation history.

A `compose.yaml` file is included for local development.

Create a local `.env` file containing the PostgreSQL password required by the Compose configuration.

The `.env` file should not be committed to Git.

Start the database:

```bash
docker compose up -d
```

The current development configuration exposes PostgreSQL on localhost port `5433`.

Next, configure the API connection string using .NET user secrets.

From the repository root:

```bash
dotnet user-secrets set \
  --project backend/DealerSync.Api \
  "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5433;Database=dealersync;Username=dealersync;Password=<your-local-password>"
```

Replace the password placeholder with your local database password.

### Apply Database Migrations

Run:

```bash
dotnet ef database update \
  --project backend/DealerSync.Infrastructure \
  --startup-project backend/DealerSync.Api
```

This creates the database tables needed for SKU mappings and reconciliation history.

### Start the API

```bash
dotnet run --project backend/DealerSync.Api
```

ASP.NET Core will print the local address where the API is running.

### Run the Tests

```bash
dotnet test
```

Docker needs to be running for the integration tests.

At the end of the v0.2.0 development milestones, DealerSync has **93 passing tests**.

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

The local repository has v0.1.0 and v0.2.0 tags. The unchecked v0.2 final-release item below remains open until its intended meaning is confirmed. v0.3.0 is in development.

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

### v0.3.0 — Web Dashboard

For the next version, I want to build a frontend so the reconciliation process can be managed without using the terminal
or making API requests manually.

Some features I'm planning include:

- Drag-and-drop CSV uploads
- Reconciliation summaries
- Searchable inventory results
- Filters for unmatched and flagged products
- SKU mapping management
- Manual verification
- Reconciliation history
- Downloadable reports

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
