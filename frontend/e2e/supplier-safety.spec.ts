import {test, expect, createSupplierMapping, reconcileSuppliers} from './support';
import {downloadDiagnostics} from './diagnostics';

const scenarios = [
    {
        title: 'two verified suppliers produce one approved combined update',
        verifySecond: true, lightspeed: 'lightspeed-suppliers.csv', inputCount: 2,
        combined: 7, approved: 7, updateCount: 1, status: 'Ready',
        reason: null, warning: null,
        contributions: [{supplier: 'A', quantity: 3}, {supplier: 'B', quantity: 4}],
    },
    {
        title: 'an unverified supplier blocks approval despite available inventory',
        verifySecond: false, lightspeed: 'lightspeed-suppliers.csv', inputCount: 2,
        combined: 3, approved: null, updateCount: 0, status: 'ReviewRecommended',
        reason: 'UnverifiedSupplier', warning: 'Supplier mapping is unverified',
        contributions: [{supplier: 'A', quantity: 3}],
    },
    {
        title: 'missing inventory for a verified supplier blocks approval',
        verifySecond: true, lightspeed: 'lightspeed-missing-supplier.csv', inputCount: 1,
        combined: 3, approved: null, updateCount: 0, status: 'ReviewRecommended',
        reason: 'MissingSupplierInventory', warning: 'Mapped supplier inventory is missing',
        contributions: [{supplier: 'A', quantity: 3}],
    },
] as const;

for (const scenario of scenarios) {
    test(scenario.title, async ({page, request, inventory}) => {
        await createSupplierMapping(request, inventory.supplierA, inventory.shopifySku, true);
        await createSupplierMapping(request, inventory.supplierB, inventory.shopifySku, scenario.verifySecond);

        const result = await reconcileSuppliers(page, inventory, scenario.lightspeed);
        expect(result.summary).toMatchObject({
            lightspeedItemCount: scenario.inputCount, shopifyItemCount: 1,
            verifiedMatches: 1, inventoryDifferences: 1,
            inventoryUpdatesGenerated: scenario.updateCount,
        });
        // There is exactly one target SKU. A zero summary means no update for this SKU.
        expect(result.matches).toHaveLength(1);
        const match = result.matches[0];
        expect(match).toMatchObject({
            shopifySku: inventory.shopifySku, location: 'E2E Test Location',
            matchType: 'Verified', currentQuantity: 2, hasInventoryDifference: true,
            combinedQuantity: scenario.combined,
            proposedQuantity: scenario.approved,
            diagnosticStatus: scenario.status,
            requiresReview: scenario.reason !== null,
        });
        expect(match.reviewReasons).toEqual(scenario.reason ? [scenario.reason] : []);
        const expectedContributions = scenario.contributions.map((contributor) => ({
            partNumber: contributor.supplier === 'A' ? inventory.supplierA : inventory.supplierB,
            quantity: contributor.quantity, matchType: 'Verified',
        }));
        expect(match.contributors).toHaveLength(expectedContributions.length);
        expect(match.contributors).toEqual(expect.arrayContaining(expectedContributions));

        const row = page.getByRole('row').filter({hasText: inventory.shopifySku});
        await expect(row).toHaveCount(1);
        const cells = row.getByRole('cell');
        await expect(cells.nth(3)).toHaveText('2');
        await expect(cells.nth(4)).toHaveText(String(scenario.combined));
        await expect(cells.nth(5)).toHaveText(scenario.approved === null ? '—' : String(scenario.approved));
        await expect(cells.nth(6)).toContainText(scenario.status);
        const contributors = cells.nth(1).getByRole('listitem');
        await expect(contributors).toHaveCount(expectedContributions.length);
        for (const contributor of expectedContributions) {
            const item = contributors.filter({hasText: contributor.partNumber});
            await expect(item.getByText(contributor.partNumber, {exact: true})).toBeVisible();
            await expect(item).toContainText(`Qty: ${contributor.quantity} · Verified`);
        }

        if (scenario.warning) {
            await expect(cells.nth(6).getByText('Requires review', {exact: true})).toBeVisible();
            await expect(cells.nth(6).getByText(scenario.warning, {exact: true})).toBeVisible();
            await expect(cells.nth(1).getByText(inventory.supplierB, {exact: true})).toHaveCount(0);
            // Calculated inventory is present, but the backend has approved nothing.
            expect(match.proposedQuantity).toBeNull();
            expect(result.matches.filter((item) => item.proposedQuantity !== null)).toHaveLength(0);
        } else {
            await expect(cells.nth(6).getByText('Requires review', {exact: true})).toHaveCount(0);
        }

        const exported = await downloadDiagnostics(page);
        expect(exported).toHaveLength(1);
        expect(exported[0]).toMatchObject({
            'Shopify SKU': inventory.shopifySku,
            'Current Quantity': '2', 'Combined Quantity': String(scenario.combined),
            'Proposed Quantity': scenario.approved === null ? '' : String(scenario.approved),
            'Inventory Difference': 'true', 'Diagnostic Status': scenario.status,
            'Requires Review': String(scenario.reason !== null),
            'Contributor Count': String(expectedContributions.length),
        });
        expect(JSON.parse(exported[0]['Contributors'])).toEqual(expect.arrayContaining(expectedContributions));
        expect(JSON.parse(exported[0]['Contributors'])).toHaveLength(expectedContributions.length);
        expect(JSON.parse(exported[0]['Review Reasons'])).toEqual(scenario.reason ? [scenario.reason] : []);

        // Guard the actual approval-facing UI, not just its diagnostic status label.
        await page.getByRole('combobox', {name: 'Filter inventory matches'}).selectOption('approved');
        await expect(page.getByRole('row').filter({hasText: inventory.shopifySku})).toHaveCount(scenario.updateCount);
        if (scenario.approved === null) {
            await expect(page.getByText('No inventory records match your search or filter.')).toBeVisible();
        }
    });
}
