import {expect, test} from '@playwright/test';
import {fileURLToPath} from 'node:url';

test('uploads synthetic CSVs and displays approved reconciliation quantities', async ({page, context, baseURL}) => {
    const externalRequests: string[] = [];
    await context.route('**/*', async (route) => {
        const url = new URL(route.request().url());
        if (url.origin === baseURL) return route.continue();
        externalRequests.push(url.origin);
        return route.abort('blockedbyclient');
    });

    await page.goto('/reconcile');
    const run = page.getByRole('button', {name: 'Run Reconciliation', exact: true});
    await expect(run).toBeDisabled();

    // The screen has two inputs, in Lightspeed then Shopify order.
    const uploads = page.locator('input[type="file"]');
    await expect(uploads).toHaveCount(2);
    await uploads.nth(0).setInputFiles(fileURLToPath(new URL('./fixtures/lightspeed-smoke.csv', import.meta.url)));
    await expect(run).toBeDisabled();
    await uploads.nth(1).setInputFiles(fileURLToPath(new URL('./fixtures/shopify-smoke.csv', import.meta.url)));
    await expect(run).toBeEnabled();

    const responsePromise = page.waitForResponse((response) =>
        new URL(response.url()).pathname === '/api/reconciliation' &&
        response.request().method() === 'POST');
    await run.click();
    const response = await responsePromise;
    expect(response.status()).toBe(200);
    const result = await response.json();
    expect(result.summary.inventoryUpdatesGenerated).toBe(1);
    expect(result.matches).toHaveLength(1);
    expect(result.matches[0]).toMatchObject({
        shopifySku: 'E2E-SMOKE-001', currentQuantity: 2,
        combinedQuantity: 7, proposedQuantity: 7,
        diagnosticStatus: 'Ready', requiresReview: false,
    });

    await expect(page.getByRole('heading', {name: 'Reconciliation Results', exact: true})).toBeVisible();
    const row = page.getByRole('row').filter({hasText: 'E2E-SMOKE-001'});
    await expect(row).toHaveCount(1);
    const cells = row.getByRole('cell');
    await expect(cells.nth(3)).toHaveText('2');
    await expect(cells.nth(4)).toHaveText('7');
    await expect(cells.nth(5)).toHaveText('7');
    await expect(cells.nth(6)).toHaveText('Ready');
    expect(externalRequests).toEqual([]);
});
