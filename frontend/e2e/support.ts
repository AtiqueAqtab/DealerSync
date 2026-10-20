import {createHash} from 'node:crypto';
import {readFile} from 'node:fs/promises';
import {expect, test as base} from '@playwright/test';
import type {APIRequestContext, Page} from '@playwright/test';
import type {ReconciliationDetailedResponse} from '../src/types/reconciliation';
import type {SkuMapping} from '../src/types/skuMapping';

interface InventoryCase {
    namespace: string;
    shopifySku: string;
    supplierA: string;
    supplierB: string;
}

export const test = base.extend<{inventory: InventoryCase; networkGuard: void}>({
    inventory: async ({baseURL}, provide, testInfo) => {
        // Stable within an attempt, distinct across tests, repeats, and runner invocations.
        const identity = [process.env.DEALERSYNC_E2E_RUN_ID, baseURL,
            testInfo.testId, testInfo.repeatEachIndex, testInfo.retry].join(':');
        const namespace = `E2E-${createHash('sha256').update(identity).digest('hex').slice(0, 20)}`;
        await provide({
            namespace, shopifySku: `${namespace}-SHOPIFY`,
            supplierA: `${namespace}-PART-A`, supplierB: `${namespace}-PART-B`,
        });
    },
    networkGuard: [async ({context, baseURL}, provide) => {
        const externalRequests: string[] = [];
        await context.route('**/*', async (route) => {
            const url = new URL(route.request().url());
            if (url.origin === baseURL) return route.continue();
            externalRequests.push(url.origin);
            return route.abort('blockedbyclient');
        });
        await provide();
        expect(externalRequests, 'Browser requests must stay inside the isolated frontend.').toEqual([]);
    }, {auto: true}],
});

export {expect} from '@playwright/test';

export async function createSupplierMapping(
    request: APIRequestContext, partNumber: string, shopifySku: string, verified: boolean,
) {
    const response = await request.post('/api/sku-mappings', {
        data: {lightspeedPartNumber: partNumber, shopifySku},
    });
    expect(response.status()).toBe(201);
    const mapping: SkuMapping = await response.json();
    expect(mapping).toMatchObject({lightspeedPartNumber: partNumber, shopifySku, isVerified: false});

    if (verified) {
        const verification = await request.patch(`/api/sku-mappings/${encodeURIComponent(mapping.id)}/verify`);
        expect(verification.status()).toBe(200);
        expect(await verification.json()).toMatchObject({id: mapping.id, isVerified: true});
    }

    // A new GET confirms the persisted state, including intentionally unverified mappings.
    const persisted = await request.get(`/api/sku-mappings/${encodeURIComponent(mapping.id)}`);
    expect(persisted.status()).toBe(200);
    expect(await persisted.json()).toMatchObject({
        id: mapping.id, lightspeedPartNumber: partNumber, shopifySku, isVerified: verified,
    });
}

export async function uploadInventory(
    page: Page, inventory: InventoryCase, lightspeedFile: string,
    shopifyFile = 'shopify-suppliers.csv', replacements: Record<string, string> = {},
) {
    const file = async (name: string) => {
        let csv = (await readFile(new URL(`./fixtures/${name}`, import.meta.url), 'utf8'))
            .replaceAll('{{CASE}}', inventory.namespace);
        for (const [token, value] of Object.entries(replacements)) csv = csv.replaceAll(token, value);
        return {
            name,
            mimeType: 'text/csv',
            buffer: Buffer.from(csv),
        };
    };
    await page.goto('/reconcile');
    const inputs = page.locator('input[type="file"]');
    await expect(inputs).toHaveCount(2);
    await inputs.nth(0).setInputFiles(await file(lightspeedFile));
    await inputs.nth(1).setInputFiles(await file(shopifyFile));

    const responsePromise = page.waitForResponse((response) =>
        new URL(response.url()).pathname === '/api/reconciliation' &&
        response.request().method() === 'POST');
    await page.getByRole('button', {name: 'Run Reconciliation', exact: true}).click();
    return responsePromise;
}

export async function reconcileSuppliers(
    page: Page, inventory: InventoryCase, lightspeedFile: string,
    shopifyFile = 'shopify-suppliers.csv',
) {
    const response = await uploadInventory(page, inventory, lightspeedFile, shopifyFile);
    expect(response.status()).toBe(200);
    const result: ReconciliationDetailedResponse = await response.json();
    await expect(page.getByRole('heading', {name: 'Reconciliation Results', exact: true})).toBeVisible();
    return result;
}
