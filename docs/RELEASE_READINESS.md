# DealerSync v0.3.0 release-candidate readiness

Validation date: 2026-10-27. Branch: `feature/multi-supplier-inventory`.
Validated code: `9fb74eae4f300b1a95a5e826af2cf818c7f3e320`.
Decision: PASS for review as a trusted, single-user, local CSV release candidate. No blocking production defect was found
within that scope. This is not approval to publish, merge, or tag. The documentation changes accompanying this report
remain uncommitted and are not covered by the existing CI SHA until approved, committed, and checked again.

## Acceptance criteria

| Criterion | Result | Evidence / scope |
| --- | --- | --- |
| Fresh source checkout | PASS | Fresh HTTPS clone in `/private/tmp/dealersync-rc-9fb74ea`; exact SHA verified, working tree clean after checks. |
| Pinned tools and dependency restore | PASS | SDK 10.0.401, Node 24.22.1/npm 11.19.1, EF 10.0.12; `dotnet tool restore`, restore/build, and new `npm ci`. Existing machine tool/package caches were permitted. |
| PostgreSQL isolation and migrations | PASS | Unique Compose project/container, tmpfs only, random loopback database port, verified database/container ownership, five real EF migrations. No development volume used. |
| Fresh-machine installation / Development user-secret entry | NOT VERIFIED | Existing tools/caches were used; normal user-secret storage was deliberately untouched. The isolated Testing environment override was verified. |
| API/frontend listeners and proxy | PASS | Actual listeners `127.0.0.1:5233` and `127.0.0.1:5173`; normal Vite config; database-backed proxy readiness and real browser reconciliation. |
| Upload, unmatched identification, mapping creation | PASS | Browser controls identified an unmatched jacket SKU, created two supplier mappings, and persisted their verification states. |
| Approval safeguards | PASS | Supplier 3+4 approved as 7 after verification; pending supplier left combined 3 with null approval. Approved zero preserved; negative stock excluded. Full suite also covers missing suppliers and overflow. |
| Actual approved download | PASS | UTF-8 BOM, all 12 spaced output headers, exact handle/options/location/current/new fields. Jacket: `rc-jacket`, Red/L, Ottawa East, 2 to 7; zero update remains 0. |
| Mapping change and stale approval handling | PASS | Creation clears results; verification of an existing pending mapping clears prior approvals; fresh reconciliation adds the newly eligible quantity 2. Existing tests also cover navigation and delayed-response races. |
| Mapping identifier editing/deletion | NOT VERIFIED | No such UI exists. The requested mapping modification was exercised through supported verification, not invented editing controls. |
| Invalid / ambiguous input | PASS | Case/whitespace duplicate SKU with distinct variants, malformed quantity, and formula-like title prevented unsafe export; corrected inputs recovered. Existing tests cover other identity ambiguities and diagnostic formula protection. |
| Real API/database failures | PASS | Stopping only the owned API cleared downloads; restart/retry succeeded. Stopping the disposable database returned 503 without credentials or approval payload. |
| Host/CORS/local model | PASS | Raw HTTP rejected an untrusted Host; untrusted Origin received no CORS permission; Compose port and actual listeners were loopback. No authentication or public deployment claimed. |
| Populated v0.2 upgrade | PASS | `MigrationUpgradeTests.PopulatedV02Database_UpgradesWithoutLosingMappingsOrIntegrity` passed inside the 151-test suite, using actual historical EF migrations and preserved mapping fields. |
| Backup and restore | PASS | README `pg_dump -Fc` and archive listing executed through isolated Compose. `pg_restore --exit-on-error` restored into a new empty database. Three mappings, seven summaries, five migrations matched exactly; restored API produced a fresh three-row approved download. |
| Real dealership recovery / downgrade | NOT VERIFIED | No development/production database touched. No destructive rollback, real backup verification, retention, corruption recovery, or point-in-time recovery attempted. |
| Backend build/tests | PASS | Fresh-clone build: zero warnings/errors. TRX: 151 passed, zero failed/skipped, including upgrade regression. |
| Frontend build/lint/types | PASS | Clean locked install and production build, ESLint, and test TypeScript check passed with pinned Node. |
| Complete browser suite | PASS | All 24 existing Playwright tests passed from the fresh clone. One-off RC acceptance was additional and did not alter the suite/test count. |
| Whitespace and workflow | PASS | Current commit first-parent check and working-tree diff checks passed. Existing workflow has depth-two checkout and guarded explicit first-parent diff. |
| Isolated cleanup | PASS | This invocation's Compose container/network, E2E container, Testcontainers, and application listeners were removed. An older E2E container was identified and left untouched; no existing persistent volume was removed. |
| Secrets / private-data review | PASS | Targeted review: 158 tracked paths, 157 text files; no strong token/key/credential-URL indicators or tracked private export/config paths. Password contexts were placeholders, generated values, or explicit test credentials. Private filename history check found none. This is not an exhaustive historical-content/secret audit. |
| Direct Shopify operations | PASS | Source review found no direct Shopify inventory-write implementation; RC browser blocked nonlocal requests. No live import or store API operation performed. |
| Live Shopify import compatibility | NOT VERIFIED | Synthetic files and documented contract only; current spelling ambiguity and unavailable template evidence remain disclosed. |
| Cross-tab / external-change invalidation | NOT VERIFIED | Not implemented globally. Same-provider invalidation is tested; snapshots in other contexts require manual fresh reconciliation. |
| CI for the committed code revision | PASS | [Run 40578983483](https://github.com/AtiqueAqtab/DealerSync/actions/runs/40578983483), push event, exact SHA above, acceptance job success on Ubuntu 24.04.5. Logs confirm 151 backend tests, 24 browser tests, whitespace, cleanup, and artifact upload. |
| CI for final documentation / merge / tag revision | NOT VERIFIED | These future revisions do not exist yet; require passing checks after approval and before tagging. |

## Exercise details and limits

The isolated Compose invocation used the fresh clone's `compose.yaml` with a temporary ownership-specific override:
unique container/project names, `127.0.0.1::5432`, no persistent volume mounts, and tmpfs at PostgreSQL's data directory.
A generated password lived only in a temporary environment file. The API used explicit Testing configuration and that
temporary connection, while using the documented listener command and normal frontend configuration. The development
`.env`, saved user secrets, database, and inventory were not used. Installing Docker or entering user secrets on a brand
new machine was not exercised. The isolation override uses Compose `!override` syntax (2.24.4+); normal startup needs
only the README's `up --wait` support. Local Compose was 5.6.1.

The browser used two Lightspeed supplier parts with quantities 3 and 4 for one jacket, another part with quantity 2,
and exact-match zero/negative products. All creation/verification actions used actual React controls; API reads only
independently checked persisted state. Initial download contained jacket 7 and zero 0. Creating and subsequently
verifying the other mapping invalidated that snapshot; fresh export added its quantity 2. No retarget/delete workflow
was claimed. On recovery, a real dump preserved all saved fields and history before a new reconciliation ran against
the restored database.

The first Host test used Node fetch, which did not transmit the custom Host header as intended. A raw curl probe
confirmed correct rejection; this was a validation-harness correction, not an application defect. The first drill's
container/process cleanup still ran. No production code correction or new regression test was necessary.

Local evidence is retained outside Git: `/private/tmp/dealersync-rc-acceptance-lP8XPp/summary.json`, the associated
synthetic archive/logs, `/private/tmp/dealersync-rc-backend-tests.log`, `/private/tmp/dealersync-rc-e2e.log`, and
`/private/tmp/dealersync-rc-secret-review.json`. These temporary paths are not permanent release artifacts. Commit this
report and retain CI evidence according to the release checklist; do not upload connection files, dumps, or private data.

A final desktop audit found pre-existing `dealersync-e2e-1453bb1c-0e1f-4242-8d69-56918d74aa86`, created on
2026-10-25 at 21:25:19 UTC and bound to `127.0.0.1:52274`. Its run label differs from this acceptance invocation's
`ddb762f0-63b8-40f2-9e58-0411a8e28263`, whose container is confirmed absent. The older container and existing development
network were left untouched. This is a nonblocking local hygiene item for its owner to review, not evidence of a cleanup
failure in the current run. Do not remove resources merely because their names contain `e2e`.

## Files changed for review

| File | Change |
| --- | --- |
| `README.md` | Replace stale CI/planned-dashboard statements with the validated candidate status and links to these reports. |
| `docs/TESTING.md` | Record fresh-clone, browser, restore, and CI acceptance, preserving historical milestone results. |
| `docs/ROADMAP.md` | Record completed milestones and remaining review/merge/tag gates without marking a release published. |
| `docs/RELEASE_NOTES_v0.3.0.md` | Add proposed release notes with installation, upgrade, and known-limit disclosures. |
| `docs/RELEASE_READINESS.md` | Add this criterion/evidence report and proposed release checklist. |

All changes are documentation only. Validated application, dependency, migration, and CI code remains at `9fb74ea`.

## Independent verification

With the pinned tools and Docker installed, create a separate clone rather than resetting an existing checkout:

```bash
git clone --single-branch --branch feature/multi-supplier-inventory https://github.com/AtiqueAqtab/DealerSync.git /tmp/dealersync-rc-review
cd /tmp/dealersync-rc-review
git rev-parse HEAD
dotnet tool restore
dotnet restore
dotnet build --no-restore
npm --prefix frontend ci
DOTNET_ENVIRONMENT=Testing ASPNETCORE_ENVIRONMENT=Testing \
ConnectionStrings__DefaultConnection='Host=127.0.0.1;Port=1;Database=dealersync_tests_unused;Username=test;Password=test' \
dotnet test --no-build --no-restore --logger 'trx;LogFileName=backend.trx' --results-directory TestResults
npm --prefix frontend run build
npm --prefix frontend run lint
npm --prefix frontend run test:e2e:types
(cd frontend && npx --no-install playwright install chromium)
# Linux: use install --with-deps chromium above.
npm --prefix frontend run test:e2e
git diff --check HEAD^ HEAD
git diff --check
git status --porcelain
docker ps -a --filter label=com.dealersync.e2e.run
docker ps -a --filter label=com.dealersync.upgrade-test
docker ps -a --filter label=org.testcontainers=true
```

Confirm Node matches `.node-version` before running npm. Check the reported SHA against the intended revision; the
branch can advance after this report. The full .NET run includes the populated migration regression. To run it alone,
use the same test environment prefix and add `--filter 'FullyQualifiedName~MigrationUpgradeTests'` to `dotnet test`.

For the one-off RC exercise retained on this workstation, the exact invocation was:

```bash
/private/tmp/dealersync-node24/node-v24.22.1-darwin-arm64/bin/node /private/tmp/dealersync-rc-browser-recovery.cjs
```

That temporary script requires the fresh clone at its recorded path, uses a unique memory-backed Compose deployment,
verifies ownership before cleanup, and leaves the primary checkout untouched. It is evidence tooling, not an additional
supported application/test framework. For manual acceptance, reproduce the browser steps above with synthetic CSVs;
use README backup commands only against an explicitly verified disposable deployment. Restore into a new empty
`rc_restore_*` database with `pg_restore --exit-on-error --no-owner --no-privileges`, then check mappings, history,
migration records, and a fresh reconciliation before treating the restore as successful.

## Proposed release checklist and sequence

- [ ] Review this report and [release notes](RELEASE_NOTES_v0.3.0.md), including every NOT VERIFIED criterion.
- [ ] Confirm acceptance of the local-only, single-user CSV scope and the unverified live Shopify import limitation.
- [ ] Review the documentation-only diff and approve an explicit file list for staging/commit.
- [ ] After approval, commit the reviewed documentation, suggested message: `docs: record v0.3.0 release acceptance`.
- [ ] After separate push approval, push the feature branch and require successful CI for that exact new SHA.
- [ ] Open/review the release PR against the agreed target branch (normally `main`); do not infer merge approval.
- [ ] After merge approval, merge and require passing CI on the resulting release commit. Revalidate if application code changes.
- [ ] Confirm `v0.3.0` is absent locally and remotely, then obtain explicit tag/release approval for the reviewed commit.
- [ ] Only after approval, create an annotated `v0.3.0` tag on that release commit, push that tag, and publish reviewed notes.

No staging, commit, push, merge, release creation, or tagging was performed during this validation. No blocking defect
was found within the approved scope. Known limits remain the release notes' one-location/integer CSV workflow,
summary-only history/placeholder routes, legacy CLI/health metadata, same-context invalidation, and unverified live
Shopify/real dealership recovery behavior. This report is not a cloud or multi-user security certification.
