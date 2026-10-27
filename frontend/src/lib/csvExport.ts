import type {ReconciliationMatch} from "../types/reconciliation";

function protectSpreadsheetText(value: string): string {
    if (/^[\s\uFEFF]*[=+\-@]/.test(value)) {
        return `'${value}`;
    }
    return value;
}

export function escapeCsvField(
    value: string | number | boolean | null | undefined
): string {
    if (value === null || value === undefined) return "";
    const text = typeof value === "string"
        ? protectSpreadsheetText(value)
        : String(value);
    return /[",\r\n]/.test(text)
        ? `"${text.replace(/"/g, '""')}"`
        : text;
}

/** Diagnostics only. This report must not be imported into Shopify. */
export function createDiagnosticsCsv(matches: ReconciliationMatch[]): string {
    const headers = [
        "Shopify SKU", "Shopify Title", "Location", "Lightspeed Part Number",
        "Match Type", "Current Quantity", "Proposed Quantity",
        "Inventory Difference", "Diagnostic Status", "Requires Review",
        "Combined Quantity", "Contributor Count", "Contributors", "Review Reasons",
    ];
    const rows = matches.map((match) => [
        match.shopifySku,
        match.shopifyTitle,
        match.location,
        match.lightspeedPartNumber,
        match.matchType,
        match.currentQuantity,
        // Never substitute combined inventory for a backend-approved update.
        match.proposedQuantity,
        match.hasInventoryDifference,
        match.diagnosticStatus,
        match.requiresReview,
        match.combinedQuantity,
        match.contributors.length,
        // JSON preserves the association between each part, quantity, and match type.
        JSON.stringify(match.contributors),
        JSON.stringify(match.reviewReasons),
    ]);
    return [
        headers.map(escapeCsvField).join(","),
        ...rows.map((row) => row.map(escapeCsvField).join(",")),
    ].join("\r\n");
}
