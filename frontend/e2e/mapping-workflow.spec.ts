import type {Page} from '@playwright/test';
import type {SkuMapping} from '../src/types/skuMapping';
import type {ReconciliationDetailedResponse} from '../src/types/reconciliation';
import {test, expect, createSupplierMapping, uploadInventoryOnPage} from './support';

async function navigate(page: Page, destination: 'SKU Mappings' | 'Reconciliation' | 'Inventory') {
    await page.getByRole('navigation', {name: 'Main navigation'})
        .getByRole('link', {name: destination, exact: true}).click();
    const paths = {'SKU Mappings': '/mappings', Reconciliation: '/reconcile', Inventory: '/inventory'};
    await expect(page).toHaveURL(new RegExp(`${paths[destination]}$`));
}

async function submitMapping(page: Page, part: string, sku: string) {
    await page.getByLabel('Lightspeed Part Number', {exact: true}).fill(part);
    await page.getByLabel('Shopify SKU', {exact: true}).fill(sku);
    const pending = page.waitForResponse((response) =>
        new URL(response.url()).pathname === '/api/sku-mappings' && response.request().method() === 'POST');
    await page.getByRole('button', {name: 'Create Mapping', exact: true}).click();
    return pending;
}

function mappingRow(page: Page, part: string) {
    return page.getByRole('row').filter({has: page.getByRole('cell', {name: part, exact: true})});
}

async function expectCreated(page: Page, part: string, sku: string): Promise<SkuMapping> {
    const response = await submitMapping(page, part, sku);
    expect(response.status()).toBe(201);
    const mapping: SkuMapping = await response.json();
    expect(mapping).toMatchObject({lightspeedPartNumber: part, shopifySku: sku, isVerified: false});
    await expect(page.getByRole('status').filter({hasText: 'Mapping created successfully.'}))
        .toHaveText('Mapping created successfully. Verification is still required.');
    await expect(mappingRow(page, part).getByRole('cell').nth(2)).toHaveText('Unverified');
    await expect(page.getByRole('button', {name: `Verify mapping from ${part} to ${sku}`, exact: true})).toBeVisible();
    return mapping;
}

async function expectInventory(page: Page, result: ReconciliationDetailedResponse, sku: string, approved: boolean) {
    const combined = approved ? 7 : 3;
    const status = approved ? 'Ready' : 'ReviewRecommended';
    expect(result.summary).toMatchObject({shopifyItemCount: 1, verifiedMatches: 1, inventoryUpdatesGenerated: approved ? 1 : 0});
    expect(result.matches).toHaveLength(1);
    expect(result.matches[0]).toMatchObject({
        shopifySku: sku, currentQuantity: 2, matchType: 'Verified', hasInventoryDifference: true,
        combinedQuantity: combined, proposedQuantity: approved ? 7 : null,
        diagnosticStatus: status, requiresReview: !approved,
        reviewReasons: approved ? [] : ['UnverifiedSupplier'],
    });
    const cells = page.getByRole('row').filter({hasText: sku}).getByRole('cell');
    await expect(cells.nth(3)).toHaveText('2');
    await expect(cells.nth(4)).toHaveText(String(combined));
    await expect(cells.nth(5)).toHaveText(approved ? '7' : '—');
    await expect(cells.nth(6)).toContainText(status);
    if (!approved) await expect(cells.nth(6)).toContainText('Supplier mapping is unverified');
    await page.getByRole('combobox', {name: 'Filter inventory matches'}).selectOption('approved');
    await expect(page.getByRole('row').filter({hasText: sku})).toHaveCount(approved ? 1 : 0);
}

async function expectCleared(page: Page) {
    // Inspect before selecting files: selecting files would itself clear old results.
    await navigate(page, 'Inventory');
    await expect(page.getByRole('heading', {name: 'No inventory results available', exact: true})).toBeVisible();
    await navigate(page, 'Reconciliation');
    await expect(page.getByRole('heading', {name: 'Reconciliation Results', exact: true})).toHaveCount(0);
    await expect(page.getByRole('button', {name: 'Download Diagnostics CSV', exact: true})).toHaveCount(0);
    await expect(page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true})).toHaveCount(0);
    await expect(page.getByRole('button', {name: 'Run Reconciliation', exact: true})).toBeDisabled();
}

test('create an unverified mapping, verify it through React, and reconcile fresh inventory', async ({page, request, inventory}) => {
    // A is background setup; creation and verification of B must use browser controls.
    await createSupplierMapping(request, inventory.supplierA, inventory.shopifySku, true);
    await page.goto('/mappings');
    const mapping = await expectCreated(page, inventory.supplierB, inventory.shopifySku);
    const persisted = await request.get(`/api/sku-mappings/${mapping.id}`);
    expect(persisted.status()).toBe(200);
    expect(await persisted.json()).toMatchObject({id: mapping.id, isVerified: false});

    await navigate(page, 'Reconciliation');
    const before = await uploadInventoryOnPage(page, inventory, 'lightspeed-suppliers.csv');
    expect(before.status()).toBe(200);
    const blocked: ReconciliationDetailedResponse = await before.json();
    expect(blocked.matches[0].contributors).toEqual([
        {partNumber: inventory.supplierA, quantity: 3, matchType: 'Verified'},
    ]);
    await expectInventory(page, blocked, inventory.shopifySku, false);

    await navigate(page, 'SKU Mappings');
    const pendingVerification = page.waitForResponse((response) =>
        new URL(response.url()).pathname === `/api/sku-mappings/${mapping.id}/verify` &&
        response.request().method() === 'PATCH');
    await page.getByRole('button', {
        name: `Verify mapping from ${inventory.supplierB} to ${inventory.shopifySku}`, exact: true,
    }).click();
    const verified = await pendingVerification;
    expect(verified.status()).toBe(200);
    expect(await verified.json()).toMatchObject({id: mapping.id, isVerified: true});
    await expect(page.getByRole('status')).toHaveText(
        `Verified ${inventory.supplierB} for ${inventory.shopifySku}. Run reconciliation again to use this mapping.`);
    await expect(mappingRow(page, inventory.supplierB).getByRole('cell').nth(2)).toHaveText('Verified');
    const saved = await request.get(`/api/sku-mappings/${mapping.id}`);
    expect(saved.status()).toBe(200);
    expect(await saved.json()).toMatchObject({id: mapping.id, isVerified: true});

    await expectCleared(page);
    const after = await uploadInventoryOnPage(page, inventory, 'lightspeed-suppliers.csv');
    expect(after.status()).toBe(200);
    const eligible: ReconciliationDetailedResponse = await after.json();
    expect(eligible.matches[0].contributors).toHaveLength(2);
    expect(eligible.matches[0].contributors).toEqual(expect.arrayContaining([
        {partNumber: inventory.supplierA, quantity: 3, matchType: 'Verified'},
        {partNumber: inventory.supplierB, quantity: 4, matchType: 'Verified'},
    ]));
    await expectInventory(page, eligible, inventory.shopifySku, true);
    const contributions = page.getByRole('row').filter({hasText: inventory.shopifySku}).getByRole('listitem');
    await expect(contributions).toHaveCount(2);
    await expect(contributions.filter({hasText: inventory.supplierA})).toContainText('Qty: 3 · Verified');
    await expect(contributions.filter({hasText: inventory.supplierB})).toContainText('Qty: 4 · Verified');
});

test('successful mapping creation clears old approvals while a failed creation preserves them', async ({page, request, inventory}) => {
    await createSupplierMapping(request, inventory.supplierA, inventory.shopifySku, true);
    await page.goto('/reconcile');
    const response = await uploadInventoryOnPage(page, inventory, 'lightspeed-suppliers.csv');
    expect(response.status()).toBe(200);
    const original: ReconciliationDetailedResponse = await response.json();
    expect(original.summary.inventoryUpdatesGenerated).toBe(1);
    expect(original.matches).toHaveLength(1);
    expect(original.matches[0]).toMatchObject({combinedQuantity: 3, proposedQuantity: 3, requiresReview: false});

    await navigate(page, 'SKU Mappings');
    // A real duplicate POST returns 409. Failure must not invalidate the valid result.
    const duplicate = await submitMapping(page, inventory.supplierA, inventory.shopifySku);
    expect(duplicate.status()).toBe(409);
    expect(await duplicate.json()).toEqual({error: 'This Lightspeed part number is already mapped.'});
    await expect(page.getByRole('alert')).toHaveText('This Lightspeed part number is already mapped.');
    await navigate(page, 'Reconciliation');
    await expect(page.getByRole('heading', {name: 'Reconciliation Results', exact: true})).toBeVisible();
    await expect(page.getByRole('button', {name: 'Download Diagnostics CSV', exact: true})).toBeVisible();
    await expect(page.getByRole('button', {name: 'Download Approved Shopify CSV', exact: true})).toBeEnabled();
    await page.getByRole('combobox', {name: 'Filter inventory matches'}).selectOption('approved');
    await expect(page.getByRole('row').filter({hasText: inventory.shopifySku}).getByRole('cell').nth(5)).toHaveText('3');

    await navigate(page, 'SKU Mappings');
    const mapping = await expectCreated(page, inventory.supplierB, inventory.shopifySku);
    const saved = await request.get(`/api/sku-mappings/${mapping.id}`);
    expect(saved.status()).toBe(200);
    expect(await saved.json()).toMatchObject({id: mapping.id, isVerified: false});
    await expectCleared(page);

    const fresh = await uploadInventoryOnPage(page, inventory, 'lightspeed-suppliers.csv');
    expect(fresh.status()).toBe(200);
    await expectInventory(page, await fresh.json(), inventory.shopifySku, false);
});
