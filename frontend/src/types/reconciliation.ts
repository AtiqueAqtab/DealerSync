export interface ReconciliationRun {
    id: string;
    createdAt: string;

    lightspeedItemCount: number;
    shopifyItemCount: number;

    exactMatches: number;
    verifiedMatches: number;
    normalizedMatches: number;
    unmatched: number;

    inventoryDifferences: number;
    inventoryUpdatesGenerated: number;
    negativeQuantitiesExcluded: number;
    normalizedUpdatesExcluded: number;

    shopifyCoverage: number;
}

export interface ReconciliationSummary {
    lightspeedItemCount: number;
    shopifyItemCount: number;

    exactMatches: number;
    verifiedMatches: number;
    normalizedMatches: number;
    unmatched: number;

    inventoryDifferences: number;
    inventoryUpdatesGenerated: number;
    negativeQuantitiesExcluded: number;
    normalizedUpdatesExcluded: number;

    shopifyCoverage: number;
}

export interface ReconciliationMatch {
    shopifySku: string;
    shopifyTitle: string;
    location: string;
    lightspeedPartNumber: string | null;
    matchType: string;
    currentQuantity: number;
    proposedQuantity: number | null;
    hasInventoryDifference: boolean;
    diagnosticStatus: string;
    requiresReview: boolean;
}

export interface ReconciliationDetailedResponse {
    summary: ReconciliationSummary;
    matches: ReconciliationMatch[];
}