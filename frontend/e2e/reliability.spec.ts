import {readFile} from 'node:fs/promises';
import {parse} from 'csv-parse/sync';
import type {ReconciliationDetailedResponse} from '../src/types/reconciliation';
import {test, expect, createSupplierMapping, uploadInventory} from './support';

function gate() {
    let release!: () => void;
    const promise = new Promise<void>((resolve) => { release = resolve; });
    return {promise, release};
}

test('invalid file selection and malformed CSV recover after corrected input', async ({page, request, inventory}) => {
    await createSupplierMapping(request, inventory.supplierA, inventory.shopifySku, true);
    await uploadInventory(page, inventory, 'lightspeed-suppliers.csv');
    await expect(page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true})).toBeEnabled();
    const input = page.locator('input[type="file"]').first();
    for (const [name, buffer, message] of [
        ['inventory.txt', Buffer.from('not CSV'), 'Please select a CSV file.'],
        ['empty.csv', Buffer.alloc(0), 'The selected file is empty.'],
    ] as const) {
        await input.setInputFiles({name, mimeType: 'text/csv', buffer});
        await expect(page.getByRole('alert')).toHaveText(message);
        await expect(page.getByRole('button', {name: 'Run Reconciliation', exact: true})).toBeDisabled();
        await expect(page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true})).toHaveCount(0);
    }
    await input.setInputFiles({name: 'malformed.csv', mimeType: 'text/csv',
        buffer: Buffer.from(`Part Number,Description,Qty\n${inventory.supplierA},Synthetic,abc\n`)});
    const failed = page.waitForResponse('**/api/reconciliation?detailed=true');
    await page.getByRole('button', {name: 'Run Reconciliation', exact: true}).click();
    expect((await failed).status()).toBe(400);
    await expect(page.getByRole('alert')).toContainText('abc');
    await expect(page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true})).toHaveCount(0);
    const corrected = (await readFile(new URL('./fixtures/lightspeed-suppliers.csv', import.meta.url), 'utf8'))
        .replaceAll('{{CASE}}', inventory.namespace);
    await input.setInputFiles({name: 'corrected.csv', mimeType: 'text/csv', buffer: Buffer.from(corrected)});
    await expect(page.getByRole('alert')).toHaveCount(0);
    const retried = page.waitForResponse('**/api/reconciliation?detailed=true');
    await page.getByRole('button', {name: 'Run Reconciliation', exact: true}).click();
    expect((await retried).status()).toBe(200);
    await expect(page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true})).toBeEnabled();
});

for (const failure of ['server', 'network'] as const) {
    test(`${failure} failure clears downloads, prevents duplicate submission, and permits retry`, async ({page, request, inventory}) => {
        await createSupplierMapping(request, inventory.supplierA, inventory.shopifySku, true);
        await uploadInventory(page, inventory, 'lightspeed-suppliers.csv');
        await expect(page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true})).toBeEnabled();
        const arrived = gate();
        const held = gate();
        let attempts = 0;
        await page.route('**/api/reconciliation?detailed=true', async (route) => {
            attempts++;
            if (attempts > 1) return route.fallback();
            arrived.release();
            await held.promise;
            if (failure === 'network') return route.abort('failed');
            await route.fulfill({status: 503, contentType: 'text/html',
                body: '<pre>Synthetic server detail: Password=NEVER-DISPLAY-THIS</pre>'});
        });
        try {
            await page.getByRole('button', {name: 'Run Reconciliation', exact: true}).click();
            await arrived.promise;
            const running = page.getByRole('button', {name: 'Running Reconciliation...', exact: true});
            await expect(running).toBeDisabled();
            await expect(page.locator('input[type="file"]').nth(0)).toBeDisabled();
            await expect(page.locator('input[type="file"]').nth(1)).toBeDisabled();
            await expect(page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true})).toHaveCount(0);
            // The focused submit control cannot submit again while disabled.
            await page.keyboard.press('Enter');
            expect(attempts).toBe(1);
            held.release();
            await expect(page.getByRole('alert')).toHaveText(failure === 'server'
                ? 'Reconciliation is unavailable. Check that the API and local database are running, then try again.'
                : 'Could not reach the API. Check that DealerSync is running and try again.');
            await expect(page.getByRole('alert')).not.toContainText('NEVER-DISPLAY-THIS');
            await expect(page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true})).toHaveCount(0);
            const retried = page.waitForResponse('**/api/reconciliation?detailed=true');
            await page.getByRole('button', {name: 'Run Reconciliation', exact: true}).click();
            expect((await retried).status()).toBe(200);
            await expect(page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true})).toBeEnabled();
            await expect(page.getByRole('alert')).toHaveCount(0);
            expect(attempts).toBe(2);
        } finally { held.release(); }
    });
}

test('a mapping saved during reconciliation prevents the older approval from returning', async ({page, request, inventory}) => {
    await createSupplierMapping(request, inventory.supplierA, inventory.shopifySku, true);
    const createHeld = gate();
    const createArrived = gate();
    const resultHeld = gate();
    const resultReady = gate();
    await page.route('**/api/sku-mappings', async (route) => {
        if (route.request().method() !== 'POST') return route.fallback();
        createArrived.release();
        await createHeld.promise;
        await route.fulfill({response: await route.fetch()});
    });
    await page.route('**/api/reconciliation?detailed=true', async (route) => {
        const response = await route.fetch();
        const old: ReconciliationDetailedResponse = await response.json();
        expect(old.matches[0].proposedQuantity).toBe(3);
        resultReady.release();
        await resultHeld.promise;
        await route.fulfill({response});
    }, {times: 1});
    try {
        await page.goto('/mappings');
        await page.getByLabel('Lightspeed Part Number', {exact: true}).fill(inventory.supplierB);
        await page.getByLabel('Shopify SKU', {exact: true}).fill(inventory.shopifySku);
        await page.getByRole('button', {name: 'Create Mapping', exact: true}).click();
        await createArrived.promise;
        await page.getByRole('navigation', {name: 'Main navigation'})
            .getByRole('link', {name: 'Reconciliation', exact: true}).click();
        // Select on this page: a reload would discard the pending mapping callback and conceal the race.
        for (const [index, name] of ['lightspeed-suppliers.csv', 'shopify-suppliers.csv'].entries()) {
            const csv = (await readFile(new URL(`./fixtures/${name}`, import.meta.url), 'utf8'))
                .replaceAll('{{CASE}}', inventory.namespace);
            await page.locator('input[type="file"]').nth(index).setInputFiles({name, mimeType: 'text/csv', buffer: Buffer.from(csv)});
        }
        await page.getByRole('button', {name: 'Run Reconciliation', exact: true}).click();
        await resultReady.promise;
        // handleCreated clears shared results before requesting this refresh, even after leaving Mappings.
        const invalidated = page.waitForResponse((response) =>
            new URL(response.url()).pathname === '/api/sku-mappings' && response.request().method() === 'GET');
        createHeld.release();
        expect((await invalidated).status()).toBe(200);
        resultHeld.release();
        await expect(page.getByRole('button', {name: 'Run Reconciliation', exact: true})).toBeEnabled();
        await expect(page.getByRole('heading', {name: 'Reconciliation Results', exact: true})).toHaveCount(0);
        await expect(page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true})).toHaveCount(0);
        await expect(page.getByRole('alert')).toHaveText('Reconciliation inputs or mappings changed. Run reconciliation again.');
        const fresh = page.waitForResponse('**/api/reconciliation?detailed=true');
        await page.getByRole('button', {name: 'Run Reconciliation', exact: true}).click();
        const current: ReconciliationDetailedResponse = await (await fresh).json();
        expect(current.matches[0].proposedQuantity).toBeNull();
        expect(current.matches[0].reviewReasons).toContain('UnverifiedSupplier');
        await expect(page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true})).toBeDisabled();
    } finally { createHeld.release(); resultHeld.release(); }
});

test('57 records retain approvals across SKU search, status filters, and pagination', async ({page, inventory}) => {
    const lightspeed = ['Part Number,Description,Qty'];
    const shopify = ['Handle,Title,Option 1 Name,Option 1 Value,SKU,Location,On hand (current)'];
    for (let index = 1; index <= 57; index++) {
        const kind = index <= 30 ? 'READY' : index <= 56 ? 'UNMATCHED' : 'NEGATIVE';
        const sku = `${inventory.namespace}-${kind}-${String(index).padStart(2, '0')}`;
        shopify.push(`product-${index},Synthetic ${index},Title,Default Title,${sku},Ottawa,2`);
        if (index <= 30 || index === 57) lightspeed.push(`${sku},Synthetic,${index === 57 ? -1 : 7}`);
    }
    await page.goto('/reconcile');
    for (const [index, csv] of [lightspeed, shopify].entries()) {
        await page.locator('input[type="file"]').nth(index).setInputFiles({name: `synthetic-${index}.csv`,
            mimeType: 'text/csv', buffer: Buffer.from(csv.join('\n') + '\n')});
    }
    const pending = page.waitForResponse('**/api/reconciliation?detailed=true');
    await page.getByRole('button', {name: 'Run Reconciliation', exact: true}).click();
    const response = await pending;
    expect(response.status()).toBe(200);
    const result: ReconciliationDetailedResponse = await response.json();
    expect(result.matches).toHaveLength(57);
    expect(result.summary.inventoryUpdatesGenerated).toBe(30);
    const rows = page.locator('tbody tr');
    const next = page.getByRole('button', {name: 'Next', exact: true});
    const previous = page.getByRole('button', {name: 'Previous', exact: true});
    const search = page.getByRole('searchbox', {name: 'Search inventory matches'});
    const filter = page.getByRole('combobox', {name: 'Filter inventory matches'});
    await expect(rows).toHaveCount(25);
    await expect(previous).toBeDisabled();
    await next.click(); await next.click();
    await expect(rows).toHaveCount(7);
    await expect(next).toBeDisabled();
    await expect(page.getByText('3 / 3', {exact: true})).toBeVisible();
    await search.fill('ready');
    await expect(page.getByText('1 / 2', {exact: true})).toBeVisible();
    await expect(rows).toHaveCount(25);
    await next.click();
    await expect(rows).toHaveCount(5);
    await previous.click();
    await search.fill(`${inventory.namespace}-READY-01`);
    await expect(rows).toHaveCount(1);
    await expect(rows.first().getByRole('cell').nth(5)).toHaveText('7');
    await search.fill('');
    await next.click(); await next.click();
    await filter.selectOption('ready');
    await expect(page.getByText('1 / 2', {exact: true})).toBeVisible();
    for (const [value, count] of [['unmatched', 25], ['review', 25], ['approved', 25]] as const) {
        await filter.selectOption(value);
        await expect(rows).toHaveCount(count);
        await expect(previous).toBeDisabled();
        if (value === 'review') {
            await expect(page.getByText('Showing 25 of 27 records', {exact: true})).toBeVisible();
            await next.click();
            await expect(rows).toHaveCount(2);
            await expect(rows.filter({hasText: '-NEGATIVE-57'})).toContainText('InvalidQuantity');
        }
    }
    await next.click();
    await expect(rows).toHaveCount(5);
    for (const row of await rows.all()) await expect(row.getByRole('cell').nth(5)).toHaveText('7');
    await search.fill('no-synthetic-match');
    await expect(rows).toHaveCount(0);
    await expect(page.getByText('No inventory records match your search or filter.')).toBeVisible();
    await expect(previous).toBeDisabled(); await expect(next).toBeDisabled();
    const download = page.waitForEvent('download');
    await page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true}).click();
    const path = await (await download).path();
    expect(path).not.toBeNull();
    const text = await readFile(path!, 'utf8');
    expect(text.replace(/^\uFEFF/, '')).toBe(result.approvedInventoryExport.csv);
    const exported = parse(text, {columns: true, bom: true}) as Record<string, string>[];
    expect(exported).toHaveLength(30);
    expect(exported.every((row) => row['On hand (new)'] === '7' && row.SKU.includes('-READY-'))).toBe(true);
});
