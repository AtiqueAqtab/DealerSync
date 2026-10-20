import {test, expect, reconcileSuppliers} from './support';
import {downloadDiagnostics} from './diagnostics';

test('download preserves CSV text and protects formula-like spreadsheet cells', async ({page, inventory}) => {
    // Exact matches need no saved mappings. Inputs and expectations are specified
    // independently of the application exporter and its escaping implementation.
    const cases = [
        {suffix: 'TEXT', title: 'Café, "large"\nsecond line', exportedTitle: 'Café, "large"\nsecond line'},
        {suffix: 'EQUALS', title: '=1+1', exportedTitle: "'=1+1"},
        {suffix: 'PLUS', title: '+1+1', exportedTitle: "'+1+1"},
        {suffix: 'MINUS', title: '-1+1', exportedTitle: "'-1+1"},
        {suffix: 'AT', title: '@SUM(1,2)', exportedTitle: "'@SUM(1,2)"},
    ];
    const result = await reconcileSuppliers(page, inventory, 'lightspeed-text.csv', 'shopify-text.csv');
    expect(result.summary.inventoryUpdatesGenerated).toBe(5);
    expect(result.matches).toHaveLength(5);
    const exported = await downloadDiagnostics(page);
    expect(exported).toHaveLength(5);
    for (const item of cases) {
        const sku = `${inventory.namespace}-${item.suffix}`;
        expect(result.matches.find((match) => match.shopifySku === sku)).toMatchObject({
            shopifyTitle: item.title, currentQuantity: 2, combinedQuantity: 7,
            proposedQuantity: 7, diagnosticStatus: 'Ready', requiresReview: false,
        });
        const rows = exported.filter((row) => row['Shopify SKU'] === sku);
        expect(rows).toHaveLength(1);
        expect(rows[0]).toEqual({
            'Shopify SKU': sku, 'Shopify Title': item.exportedTitle,
            'Location': 'E2E Test Location', 'Lightspeed Part Number': sku,
            'Match Type': 'Exact', 'Current Quantity': '2', 'Combined Quantity': '7',
            'Proposed Quantity': '7', 'Inventory Difference': 'true',
            'Diagnostic Status': 'Ready', 'Requires Review': 'false',
            'Contributor Count': '1',
            'Contributors': JSON.stringify([{partNumber: sku, quantity: 7, matchType: 'Exact'}]),
            'Review Reasons': '[]',
        });
    }
});
