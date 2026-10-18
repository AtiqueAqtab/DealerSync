import type {ReconciliationMatch} from "../types/reconciliation";

function protectSpreadsheetText(value: string): string {
    if (/^[\s\uFEFF]*[=+\-@]/.test(value)) {
        return `'${value}`;
    }

    return value;
}

/**
 * Escapes a value so it can safely be written into a CSV field.
 *
 * Fields containing commas, quotes, or line breaks are enclosed
 * in double quotes. Existing double quotes are escaped.
 */
export function escapeCsvField(
    value: string | number | boolean | null | undefined
): string {
    if (value === null || value === undefined) {
        return "";
    }

    const text =
        typeof value === "string"
            ? protectSpreadsheetText(value)
            : String(value);

    if (/[",\r\n]/.test(text)) {
        return `"${text.replace(/"/g, '""')}"`;
    }

    return text;
}

/**
 * Converts reconciliation matches into a diagnostics CSV.
 *
 * This report is for review and troubleshooting only.
 * It must not be imported into Shopify.
 */
export function createDiagnosticsCsv(
    matches: ReconciliationMatch[]
): string {
    const headers = [
        "Shopify SKU",
        "Shopify Title",
        "Location",
        "Lightspeed Part Number",
        "Match Type",
        "Current Quantity",
        "Proposed Quantity",
        "Inventory Difference",
        "Diagnostic Status",
        "Requires Review",
    ];

    const rows = matches.map((match) => [
        match.shopifySku,
        match.shopifyTitle,
        match.location,
        match.lightspeedPartNumber,
        match.matchType,
        match.currentQuantity,
        match.proposedQuantity,
        match.hasInventoryDifference,
        match.diagnosticStatus,
        match.requiresReview,
    ]);

    return [
        headers.map(escapeCsvField).join(","),
        ...rows.map((row) =>
            row.map(escapeCsvField).join(",")
        ),
    ].join("\r\n");
}