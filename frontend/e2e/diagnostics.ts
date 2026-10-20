import {readFile} from 'node:fs/promises';
import {parse} from 'csv-parse/sync';
import type {Page} from '@playwright/test';
import {expect} from './support';

// An independent contract: never import the application exporter or its headers.
const headers = [
    'Shopify SKU', 'Shopify Title', 'Location', 'Lightspeed Part Number',
    'Match Type', 'Current Quantity', 'Proposed Quantity', 'Inventory Difference',
    'Diagnostic Status', 'Requires Review', 'Combined Quantity', 'Contributor Count',
    'Contributors', 'Review Reasons',
];

export async function downloadDiagnostics(page: Page): Promise<Record<string, string>[]> {
    const pendingDownload = page.waitForEvent('download');
    await page.getByRole('button', {name: 'Download Diagnostics CSV', exact: true}).click();
    const download = await pendingDownload;
    expect(download.suggestedFilename()).toBe('dealersync_diagnostics.csv');
    expect(await download.failure()).toBeNull();
    const path = await download.path();
    if (!path) throw new Error('The browser did not produce a diagnostics download.');
    const bytes = await readFile(path);
    expect([...bytes.subarray(0, 3)], 'UTF-8 BOM for spreadsheet readers').toEqual([239, 187, 191]);
    const records: string[][] = parse(bytes, {bom: true});
    expect(records[0]).toEqual(headers);
    return records.slice(1).map((values) => {
        expect(values).toHaveLength(headers.length);
        return Object.fromEntries(headers.map((header, index) => [header, values[index]]));
    });
}
