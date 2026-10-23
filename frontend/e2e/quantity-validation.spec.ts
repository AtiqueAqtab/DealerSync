import type {Page, Response} from '@playwright/test';
import {test, expect, createSupplierMapping, reconcileSuppliers, uploadInventory} from './support';
import {downloadDiagnostics} from './diagnostics';

async function expectRejectedUpload(page: Page, response: Response, error: string) {
    expect(response.status()).toBe(400);
    expect(await response.json()).toEqual({error});
    await expect(page.getByRole('alert')).toContainText(error);
    await expect(page.getByRole('heading', {name: 'Reconciliation Results', exact: true})).toHaveCount(0);
    await expect(page.getByRole('button', {name: 'Download Diagnostics CSV', exact: true})).toHaveCount(0);
    await expect(page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true})).toHaveCount(0);
}

test('a negative supplier blocks approval even when the calculated sum is positive', async ({page, request, inventory}) => {
    await createSupplierMapping(request, inventory.supplierA, inventory.shopifySku, true);
    await createSupplierMapping(request, inventory.supplierB, inventory.shopifySku, true);
    const result = await reconcileSuppliers(page, inventory, 'lightspeed-negative.csv');
    expect(result.summary.inventoryUpdatesGenerated).toBe(0);
    expect(result.matches).toHaveLength(1);
    const match = result.matches[0];
    expect(match).toMatchObject({
        shopifySku: inventory.shopifySku, currentQuantity: 2,
        combinedQuantity: 5, proposedQuantity: null, hasInventoryDifference: true,
        diagnosticStatus: 'InvalidQuantity', requiresReview: true,
    });
    // ReviewReason currently describes mapping problems; negative stock uses the diagnostic status.
    expect(match.reviewReasons).toEqual([]);
    const contributions = [
        {partNumber: inventory.supplierA, quantity: 7, matchType: 'Verified'},
        {partNumber: inventory.supplierB, quantity: -2, matchType: 'Verified'},
    ];
    expect(match.contributors).toHaveLength(2);
    expect(match.contributors).toEqual(expect.arrayContaining(contributions));
    const cells = page.getByRole('row').filter({hasText: inventory.shopifySku}).getByRole('cell');
    await expect(cells.nth(3)).toHaveText('2');
    await expect(cells.nth(4)).toHaveText('5');
    await expect(cells.nth(5)).toHaveText('—');
    await expect(cells.nth(6)).toContainText('InvalidQuantity');
    await expect(cells.nth(6)).toContainText('Requires review');
    await expect(cells.nth(1)).toContainText('Qty: -2 · Verified');
    const exported = await downloadDiagnostics(page);
    expect(exported).toHaveLength(1);
    expect(exported[0]).toMatchObject({
        'Shopify SKU': inventory.shopifySku, 'Current Quantity': '2',
        'Combined Quantity': '5', 'Proposed Quantity': '',
        'Diagnostic Status': 'InvalidQuantity', 'Requires Review': 'true',
        'Contributor Count': '2', 'Review Reasons': '[]',
    });
    expect(JSON.parse(exported[0]['Contributors'])).toEqual(expect.arrayContaining(contributions));
    await page.getByRole('combobox', {name: 'Filter inventory matches'}).selectOption('approved');
    await expect(page.getByRole('row').filter({hasText: inventory.shopifySku})).toHaveCount(0);
    await expect(page.getByText('No inventory records match your search or filter.')).toBeVisible();
});

test('an approved zero remains zero in the API, results, approved filter, and CSV', async ({page, request, inventory}) => {
    await createSupplierMapping(request, inventory.supplierA, inventory.shopifySku, true);
    const result = await reconcileSuppliers(page, inventory, 'lightspeed-zero.csv', 'shopify-zero.csv');
    expect(result.summary.inventoryUpdatesGenerated).toBe(1);
    expect(result.matches).toHaveLength(1);
    expect(result.matches[0]).toMatchObject({
        currentQuantity: 5, combinedQuantity: 0, proposedQuantity: 0,
        diagnosticStatus: 'Ready', requiresReview: false, hasInventoryDifference: true,
        reviewReasons: [], contributors: [{partNumber: inventory.supplierA, quantity: 0, matchType: 'Verified'}],
    });
    await page.getByRole('combobox', {name: 'Filter inventory matches'}).selectOption('approved');
    const row = page.getByRole('row').filter({hasText: inventory.shopifySku});
    await expect(row).toHaveCount(1);
    const cells = row.getByRole('cell');
    await expect(cells.nth(3)).toHaveText('5');
    await expect(cells.nth(4)).toHaveText('0');
    await expect(cells.nth(5)).toHaveText('0');
    await expect(cells.nth(6)).toHaveText('Ready');
    const exported = await downloadDiagnostics(page);
    expect(exported).toHaveLength(1);
    expect(exported[0]).toMatchObject({
        'Shopify SKU': inventory.shopifySku, 'Current Quantity': '5',
        'Combined Quantity': '0', 'Proposed Quantity': '0',
        'Diagnostic Status': 'Ready', 'Requires Review': 'false', 'Review Reasons': '[]',
        'Contributor Count': '1',
    });
    expect(JSON.parse(exported[0]['Contributors'])).toEqual([
        {partNumber: inventory.supplierA, quantity: 0, matchType: 'Verified'},
    ]);
});

// xUnit covers the full format matrix in both importers. These four cases exercise
// each error category through the browser, with both upload paths represented.
const invalidInputs = [
    {title: 'nonnumeric Lightspeed quantity', source: 'lightspeed', value: 'abc'},
    {title: 'fractional Shopify quantity', source: 'shopify', value: '1.5'},
    {title: 'blank Lightspeed quantity', source: 'lightspeed', value: ''},
    {title: 'out-of-range Shopify quantity', source: 'shopify', value: '2147483648'},
] as const;

for (const input of invalidInputs) {
    test(`rejects ${input.title} without offering results or a download`, async ({page, inventory}) => {
        const response = await uploadInventory(page, inventory, 'lightspeed-invalid.csv', 'shopify-invalid.csv', {
            '{{LIGHTSPEED_QUANTITY}}': input.source === 'lightspeed' ? input.value : '7',
            '{{SHOPIFY_QUANTITY}}': input.source === 'shopify' ? input.value : '2',
        });
        const identifier = input.source === 'lightspeed' ? 'part number' : 'SKU';
        await expectRejectedUpload(page, response,
            `Invalid quantity '${input.value}' for ${identifier} '${inventory.shopifySku}' at CSV row 2.`);
    });
}

test('individually valid supplier quantities cannot overflow into an approved update', async ({page, request, inventory}) => {
    await createSupplierMapping(request, inventory.supplierA, inventory.shopifySku, true);
    await createSupplierMapping(request, inventory.supplierB, inventory.shopifySku, true);
    const response = await uploadInventory(page, inventory, 'lightspeed-overflow.csv');
    // Existing blocked-overflow xUnit coverage returns diagnostics. Fully eligible
    // suppliers instead reject the upload; no successful result or quantity exists.
    await expectRejectedUpload(page, response, 'Inventory reconciliation failed due to conflicting data.');
});
