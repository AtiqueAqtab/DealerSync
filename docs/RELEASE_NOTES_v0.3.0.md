# DealerSync v0.3.0 release notes

Status: proposed release notes. Code revision `9fb74eae4f300b1a95a5e826af2cf818c7f3e320` passed release-candidate
acceptance on 2026-10-27. This document does not create a release or authorize a tag.

## What is included

The React dashboard provides CSV upload, reconciliation summaries, searchable inventory results, diagnostic filters,
pagination, and saved SKU mapping creation and verification. Supplier contributions, calculated combined quantities,
and backend-approved quantities are displayed separately. Zero is an approved quantity; a blank approval means there
is no authorized update.

Multiple verified Lightspeed suppliers can contribute inventory to one Shopify SKU. A known unverified supplier or a
missing mapped supplier blocks the affected product's update. Negative stock, overflowing totals, duplicate SKU/location
records, and conflicting assignments cannot authorize an unsafe update. Normalized matches remain suggestions until a
trusted relationship exists. Verification requires the operator to confirm that the identifiers describe the same item.

The web workflow offers separate diagnostic and approved Shopify inventory CSV downloads. The approved file comes from
the backend's actual update collection and retains source handle, option identity, exact location, current stock, and
approved new stock. Table filters do not change download scope. Output uses spaced `Option 1 Name` / `Option 1 Value`
headers for all three options; both input alias styles remain accepted. Incomplete or ambiguous identity blocks the whole
approved file. Diagnostic CSV text has spreadsheet formula protection; formula-like identity blocks machine-import
output instead of changing identifiers.

Recovery supports corrected CSV uploads and retries after API/network failures. Upload and submit controls are disabled
while processing. Navigation aborts old requests, successful mapping changes clear stored results, and a shared version
check prevents an earlier response from restoring obsolete approvals. Database outages return safe failures without
approval payloads. Local configuration restricts listeners/accepted hosts and does not enable permissive API CORS.

The repository includes pinned development tools, real PostgreSQL integration tests, populated migration-upgrade
coverage, 24 Chromium acceptance tests, and GitHub Actions on Ubuntu 24.04. At the validated revision, all 151 backend
tests and 24 browser tests passed locally and in [CI run 40578983483](https://github.com/AtiqueAqtab/DealerSync/actions/runs/40578983483).

## Installation and upgrade

Use .NET SDK 10.0.401 (later patches in its feature band are allowed), Node 24.22.1 with bundled npm 11.19.1, the local
EF tool 10.0.12, and Docker with Compose supporting `up --wait`. PostgreSQL uses the existing `postgres:17` image.
Follow the [README](../README.md#getting-started-locally) for setup, configuration, migrations, and safe shutdown.
The supported workstation workflow is macOS/Linux, with API `127.0.0.1:5233` and frontend `127.0.0.1:5173`.

Back up populated databases before applying forward migrations. v0.2 ends at
`20261013024015_AddNormalizedUpdatesExcluded`; v0.3 adds
`20261017023848_AllowMultipleSuppliersPerShopifySku`. Existing mapping data is preserved while Shopify SKU uniqueness is
removed to allow several Lightspeed suppliers. Lightspeed uniqueness remains case insensitive. Destructive downgrade is
unsupported after multi-supplier data exists. Restore a verified pre-upgrade backup into a separate database with the
matching application revision if recovery is required; do not delete volumes or migration-history entries.

An isolated release drill restored three mappings, seven reconciliation summaries, and all five migration records,
then generated a fresh approved file using the restored database. This does not verify a dealership backup, storage
retention, point-in-time recovery, or recovery after physical corruption.

## Known limits

- This is a trusted, single-user local CSV application. It does not provide authentication, multi-user security, cloud
  deployment, automatic Shopify synchronization, or direct Shopify inventory writes.
- Live Shopify CSV import compatibility has not been independently verified. The official documentation has inconsistent
  option-header spelling; output follows its detailed table. No live-store import was performed during acceptance.
- Reconciliation invalidation is local to one React provider. It does not synchronize across browser tabs, other clients,
  direct API/SQL changes, or mapping mutations whose responses were lost. Reconcile again after uncertain changes.
- The UI supports mapping creation and verification, not identifier editing, retargeting, or deletion.
- Reconciliation supports one Shopify location per upload and integer stock. Keep the exact source variant identity;
  do not substitute diagnostics or calculated quantities for the approved import file.
- Saved history contains summaries. Dedicated History and Settings routes remain placeholders, not completed workflows.
- `/api/health` checks liveness, not database readiness; its legacy `0.2.0` version field is not release metadata.
- The legacy CLI does not load saved mappings and is outside the mapping-aware web acceptance guarantee.
- Windows process cleanup, real dealership data, broad browser compatibility, production restore procedures, and a full
  historical secret audit have not been established by this release exercise.

See [RELEASE_READINESS.md](RELEASE_READINESS.md) for exact criteria, evidence, commands, remaining approval gates,
and the proposed commit/merge/tag sequence.
