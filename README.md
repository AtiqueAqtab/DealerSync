# DealerSync

DealerSync is a .NET application I'm building to help automate inventory updates between **Lightspeed EVO** and **Shopify**.

The idea came from a problem I ran into at work. Our dealership uses Lightspeed EVO to manage inventory and Shopify for the online store, but inventory between the two systems has to be updated manually. DealerSync is my attempt to automate that process while still making sure questionable matches or inventory values are reviewed instead of blindly pushed to Shopify.

## Current Version

### v0.1 - Inventory Reconciliation MVP

The current version can:

- Import a Lightspeed EVO 505 inventory report
- Import a Shopify inventory export
- Match Lightspeed part numbers to Shopify SKUs
- Handle some formatting differences between SKUs
- Find inventory quantity differences
- Detect duplicate inventory updates
- Exclude negative inventory quantities
- Generate a Shopify-compatible inventory update CSV
- Generate diagnostic reports for products that could not be matched

The program currently works with CSV files. Direct API integration will come later.

## Why I Built It

At the dealership, Lightspeed EVO is the main inventory system, while Shopify is used for the online store.

Keeping the two up to date manually gets difficult because the data isn't always identical.

For example:

```text
Lightspeed:  ABC-123
Shopify:     ABC123
```

There are also cases where:

- A product exists in Shopify but has never been received in Lightspeed
- Shopify contains variants that aren't in the Lightspeed 505 report
- The same SKU appears more than once
- Lightspeed inventory can be negative
- Suppliers use different formats for their product data

Because inventory affects what customers can actually order, DealerSync is designed to be conservative about what it considers a safe match.

## How It Works

The current workflow looks like this:

```text
Lightspeed 505 CSV ──┐
                     │
                     ▼
                DealerSync
                     ▲
                     │
Shopify CSV ─────────┘
                     │
                     ▼
               Match SKUs
                     │
                     ▼
            Compare Quantities
                     │
                     ▼
              Validate Updates
                     │
                     ▼
         Shopify Inventory CSV
```

DealerSync reads both files, matches the inventory records, compares their quantities, and generates a new CSV containing the changes that can safely be imported into Shopify.

## SKU Matching

The matching system currently has two levels.

### Exact Match

```text
Lightspeed: KLIM-001
Shopify:    KLIM-001
```

### Normalized Match

DealerSync removes some common formatting differences before comparing SKUs.

For example:

```text
ABC-123
ABC 123
ABC_123
ABC.123
ABC/123
```

all normalize to:

```text
ABC123
```

If neither method produces a match, the item stays unmatched.

I intentionally don't use fuzzy matching to automatically update inventory. Two SKUs can look very similar while representing different sizes, colours, or variants of the same product.

## Safety Checks

Since this project deals with real inventory, I added a few safeguards before generating an update file.

### Unmatched SKUs

If DealerSync can't confidently match a Shopify SKU to a Lightspeed part number, it doesn't generate an inventory update for it.

### Negative Inventory

Negative quantities from Lightspeed are excluded from the automatic update file and reported separately.

### Duplicate Updates

If duplicate records produce the same quantity update, DealerSync collapses them into one update.

If duplicate records produce conflicting quantities, DealerSync stops instead of guessing which quantity is correct.

## Project Structure

```text
backend/
├── DealerSync.Api/
├── DealerSync.Core/
├── DealerSync.Infrastructure/
├── DealerSync.Runner/
└── DealerSync.Tests/
```

### DealerSync.Core

Contains the main application logic, including:

- Models
- SKU matching
- SKU normalization
- Inventory reconciliation
- Inventory update generation

### DealerSync.Infrastructure

Handles external file formats.

Currently contains:

- Lightspeed CSV importer
- Shopify CSV importer
- Shopify inventory CSV exporter

### DealerSync.Runner

A command-line application used to run the complete reconciliation process.

### DealerSync.Api

ASP.NET Core project that will eventually expose the reconciliation functionality through an API.

### DealerSync.Tests

xUnit tests for the matching, reconciliation, importing, and exporting logic.

## Running the Project

### Requirements

- .NET 10 SDK

Clone the repository and build it:

```bash
dotnet build
```

Run the tests:

```bash
dotnet test
```

Run a reconciliation:

```bash
dotnet run --project backend/DealerSync.Runner -- \
"path/to/lightspeed.csv" \
"path/to/shopify.csv"
```

DealerSync will print a summary showing things like:

```text
Shopify Match Coverage
----------------------
Exact:                  ...
Normalized:             ...
Unmatched:              ...
Coverage:               ...%

Shopify Inventory Update
------------------------
Inventory updates generated: ...
Negative quantities excluded: ...
```

It also generates local diagnostic files and the Shopify inventory update CSV.

## Data Privacy

Real dealership inventory files are stored in:

```text
data/
```

The directory is ignored by Git and isn't included in this repository.

Tests use sample data instead of the dealership's actual inventory.

## Tech Stack

- C#
- .NET 10
- ASP.NET Core
- CsvHelper
- xUnit
- Git / GitHub
- JetBrains Rider

## Roadmap

### v0.1 - Inventory Reconciliation

- [x] Lightspeed EVO CSV importing
- [x] Shopify CSV importing
- [x] Exact SKU matching
- [x] SKU normalization
- [x] Inventory comparison
- [x] Duplicate protection
- [x] Negative inventory protection
- [x] Shopify inventory update CSV generation
- [x] Automated tests

### v0.2 - API and Saved Mappings

- [ ] ASP.NET Core reconciliation API
- [ ] Store manually confirmed SKU mappings
- [ ] Reconciliation history
- [ ] Better diagnostics and logging

### v0.3 - Supplier Integration

One of the longer-term goals is to import supplier product data directly.

Different suppliers provide their data in different ways, including:

- CSV
- JSON
- XML
- ZIP files containing spreadsheets and product images

The goal is to convert those different formats into one internal product model.

That could eventually allow DealerSync to:

- Detect new supplier products
- Match products using UPC/barcodes
- Compare supplier catalogs with Shopify
- Identify missing variants
- Select products to add to Shopify
- Import product descriptions and images

## Future Ideas

Some other features I'd like to add:

- Direct Shopify Admin API integration
- Automatic inventory syncing
- Web dashboard
- Scheduled syncs
- Multi-location inventory
- Audit logs
- Product catalog health checks

## About the Project

DealerSync started as a way to solve a repetitive inventory problem at work, but I'm also using it to get more experience building a larger C#/.NET project.

Some of the areas I'm focusing on are:

- Clean application architecture
- Working with real-world data
- Data normalization and matching
- Defensive programming
- Automated testing
- REST APIs
- External system integration