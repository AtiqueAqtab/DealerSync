import {readFile} from 'node:fs/promises';
import {parse} from 'csv-parse/sync';
import type {Page} from '@playwright/test';
import type {ReconciliationDetailedResponse} from '../src/types/reconciliation';
import {test, expect, createSupplierMapping, uploadInventory} from './support';

const headers = ['Handle', 'Title', 'Option 1 Name', 'Option 1 Value', 'Option 2 Name', 'Option 2 Value',
    'Option 3 Name', 'Option 3 Value', 'SKU', 'Location', 'On hand (current)', 'On hand (new)'];

async function downloadApproved(page: Page) {
    const pending = page.waitForEvent('download');
    await page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true}).click();
    const download = await pending;
    expect(download.suggestedFilename()).toBe('dealersync_approved_shopify_inventory.csv');
    expect(await download.failure()).toBeNull();
    const path = await download.path();
    if (!path) throw new Error('No approved CSV file was downloaded.');
    const bytes = await readFile(path);
    expect([...bytes.subarray(0, 3)]).toEqual([239, 187, 191]);
    const rows: string[][] = parse(bytes, {bom: true});
    expect(rows[0]).toEqual(headers);
    return {text: bytes.toString('utf8').slice(1), records: rows.slice(1).map((values) => {
        expect(values).toHaveLength(12);
        return Object.fromEntries(headers.map((header, index) => [header, values[index]]));
    })};
}

test('download contains only approved source variants with exact identity, current stock, and approved zero', async ({page, request, inventory}) => {
    for (const [part, sku, verified] of [
        ['PART-A', 'SHOPIFY', true], ['PART-B', 'SHOPIFY', true],
        ['PENDING-A', 'PENDING', true], ['PENDING-B', 'PENDING', false],
        ['MISSING-A', 'MISSING', true], ['MISSING-B', 'MISSING', true],
        ['NEGATIVE-A', 'NEGATIVE', true], ['NEGATIVE-B', 'NEGATIVE', true],
    ] as const) await createSupplierMapping(request, `${inventory.namespace}-${part}`, `${inventory.namespace}-${sku}`, verified);
    const response = await uploadInventory(page, inventory, 'lightspeed-approved-export.csv', 'shopify-approved-export.csv');
    expect(response.status()).toBe(200);
    const result: ReconciliationDetailedResponse = await response.json();
    expect(result.summary.inventoryUpdatesGenerated).toBe(6);
    expect(result.approvedInventoryExport).toMatchObject({error: null, updateCount: 6});
    for (const suffix of ['PENDING', 'MISSING', 'NEGATIVE', 'NORM-1', 'SAME']) {
        expect(result.matches.find((match) => match.shopifySku === `${inventory.namespace}-${suffix}`)?.proposedQuantity).toBeNull();
    }
    // Download scope is the backend-approved collection, independent of the visible review filter.
    await page.getByRole('combobox', {name: 'Filter inventory matches'}).selectOption('review');
    const exported = await downloadApproved(page);
    expect(exported.text).toBe(result.approvedInventoryExport.csv);
    expect(exported.records).toHaveLength(6);
    const expected = [
        {suffix: 'DEFAULT', handle: 'synthetic-default', current: '2', approved: '7', options: ['Title', 'Default Title', '', '', '', ''], title: 'Default product'},
        {suffix: 'ONE', handle: 'synthetic-one', current: '2', approved: '8', options: ['Size', 'L', '', '', '', ''], title: 'One option'},
        {suffix: 'TWO', handle: 'synthetic-two', current: '2', approved: '9', options: ['Color', 'Blue', 'Size', 'L', '', ''], title: 'Two options'},
        {suffix: 'THREE', handle: 'synthetic-three', current: '2', approved: '10', options: ['Color', 'Blue', 'Size', 'L', 'Finish', 'Matte'], title: '  Café, "summer"\nOriginal  '},
        {suffix: 'ZERO', handle: 'synthetic-zero', current: '5', approved: '0', options: ['Title', 'Default Title', '', '', '', ''], title: 'Zero product'},
        {suffix: 'SHOPIFY', handle: 'synthetic-multi', current: '2', approved: '7', options: ['Title', 'Default Title', '', '', '', ''], title: 'Verified suppliers'},
    ];
    for (const item of expected) {
        const sku = `${inventory.namespace}-${item.suffix}`;
        const rows = exported.records.filter((record) => record.SKU === sku);
        expect(rows).toHaveLength(1);
        expect(rows[0]).toEqual({
            Handle: item.handle, Title: item.title, SKU: sku, Location: 'Ottawa East',
            'Option 1 Name': item.options[0], 'Option 1 Value': item.options[1],
            'Option 2 Name': item.options[2], 'Option 2 Value': item.options[3],
            'Option 3 Name': item.options[4], 'Option 3 Value': item.options[5],
            'On hand (current)': item.current, 'On hand (new)': item.approved,
        });
        expect(result.matches.find((match) => match.shopifySku === sku)?.proposedQuantity).toBe(Number(item.approved));
    }
    // Changing an input invalidates both downloads, not just the diagnostic table.
    await page.locator('input[type="file"]').nth(0).setInputFiles({
        name: 'replacement.csv', mimeType: 'text/csv',
        buffer: Buffer.from('Part Number,Description,Qty\nSYNTHETIC,Replacement,1\n'),
    });
    await expect(page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true})).toHaveCount(0);
});

for (const scenario of [
    {title: 'missing option value', handle: 'bad-b', value: '', product: 'Second product', error: 'Source identity is incomplete'},
    {title: 'duplicate variant under different SKUs', handle: 'bad-a', value: 'Default Title', product: 'Second product', error: 'Duplicate product/variant/location'},
    {title: 'formula-like source text', handle: 'bad-b', value: 'Default Title', product: '=1+1', error: 'Source text is formula-like'},
]) {
    test(`blocks the whole approved download for ${scenario.title}`, async ({page, inventory}) => {
        const response = await uploadInventory(page, inventory, 'lightspeed-invalid-export.csv', 'shopify-invalid-export.csv', {
            '{{SECOND_HANDLE}}': scenario.handle, '{{BAD_VALUE}}': scenario.value, '{{BAD_TITLE}}': scenario.product,
        });
        expect(response.status()).toBe(200);
        const result: ReconciliationDetailedResponse = await response.json();
        expect(result.summary.inventoryUpdatesGenerated).toBe(2);
        expect(result.approvedInventoryExport).toMatchObject({csv: null, updateCount: 2});
        expect(result.approvedInventoryExport.error).toContain(scenario.error);
        await expect(page.getByRole('alert')).toContainText(scenario.error);
        await expect(page.getByRole('alert')).toContainText('No partial update file was generated.');
        await expect(page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true})).toBeDisabled();
        await expect(page.getByRole('button', {name: 'Download Diagnostics CSV', exact: true})).toBeEnabled();
    });
}

test('a completed old API response cannot restore an export after navigation and mapping creation', async ({page, request, inventory}) => {
    await createSupplierMapping(request, inventory.supplierA, inventory.shopifySku, true);
    let release!: () => void;
    const held = new Promise<void>((resolve) => { release = resolve; });
    let ready!: (value: ReconciliationDetailedResponse) => void;
    const serverResult = new Promise<ReconciliationDetailedResponse>((resolve) => { ready = resolve; });
    let finish!: () => void;
    const delivered = new Promise<void>((resolve) => { finish = resolve; });
    await page.route('**/api/reconciliation?detailed=true', async (route) => {
        try {
            const response = await route.fetch(); // Real API/database; hold delivery, not approval generation.
            ready(await response.json());
            await held;
            try { await route.fulfill({response}); }
            catch (error) { if (!route.request().failure()) throw error; }
        } finally { finish(); }
    });
    try {
        await page.goto('/reconcile');
        for (const [index, name] of ['lightspeed-suppliers.csv', 'shopify-suppliers.csv'].entries()) {
            const content = (await readFile(new URL(`./fixtures/${name}`, import.meta.url), 'utf8')).replaceAll('{{CASE}}', inventory.namespace);
            await page.locator('input[type="file"]').nth(index).setInputFiles({name, mimeType: 'text/csv', buffer: Buffer.from(content)});
        }
        await page.getByRole('button', {name: 'Run Reconciliation', exact: true}).click();
        const original = await serverResult;
        // A real response is held: file replacement must remain unavailable until submission ends.
        await expect(page.locator('input[type="file"]')).toHaveCount(2);
        for (const input of await page.locator('input[type="file"]').all()) {
            await expect(input).toBeDisabled();
        }
        await expect(page.getByRole('button', {name: 'Running Reconciliation...', exact: true})).toBeDisabled();
        expect(original.approvedInventoryExport.csv).not.toBeNull();
        expect(original.matches[0].proposedQuantity).toBe(3);
        const cancelled = page.waitForEvent('requestfailed', (request) =>
            new URL(request.url()).pathname === '/api/reconciliation' && request.method() === 'POST');
        await page.getByRole('navigation', {name: 'Main navigation'}).getByRole('link', {name: 'SKU Mappings', exact: true}).click();
        expect((await cancelled).failure()?.errorText).toContain('ERR_ABORTED');
        await page.getByLabel('Lightspeed Part Number', {exact: true}).fill(inventory.supplierB);
        await page.getByLabel('Shopify SKU', {exact: true}).fill(inventory.shopifySku);
        await page.getByRole('button', {name: 'Create Mapping', exact: true}).click();
        await expect(page.getByRole('status').filter({hasText: 'Mapping created successfully.'})).toBeVisible();
        release();
        await delivered;
        await page.getByRole('navigation', {name: 'Main navigation'}).getByRole('link', {name: 'Inventory', exact: true}).click();
        await expect(page.getByRole('heading', {name: 'No inventory results available', exact: true})).toBeVisible();
        await page.getByRole('navigation', {name: 'Main navigation'}).getByRole('link', {name: 'Reconciliation', exact: true}).click();
        await expect(page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true})).toHaveCount(0);
    } finally { release(); }
});
