export interface ReconciliationRun extends ReconciliationSummary {
    id: string;
    createdAt: string;
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

export interface InventoryContributor {
    partNumber: string;
    quantity: number;
    matchType: string;
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
    combinedQuantity: number | null;
    contributors: InventoryContributor[];
    // Keep unknown future backend reasons visible rather than dropping them.
    reviewReasons: string[];
}

export interface ReconciliationDetailedResponse {
    summary: ReconciliationSummary;
    matches: ReconciliationMatch[];
    approvedInventoryExport: {
        csv: string | null;
        error: string | null;
        updateCount: number;
    };
}
