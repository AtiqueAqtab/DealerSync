import {apiGet} from "./client";
import type {ReconciliationRun} from "../types/reconciliation";
import {ApiError} from "./client";

import type {
    ReconciliationDetailedResponse,
} from "../types/reconciliation";

export function getReconciliationHistory(): Promise<ReconciliationRun[]> {
    return apiGet<ReconciliationRun[]>("/api/reconciliation/history");
}

export async function runReconciliation(
    lightspeedFile: File,
    shopifyFile: File,
    signal?: AbortSignal,
): Promise<ReconciliationDetailedResponse> {
    const formData = new FormData();

    formData.append("lightspeedFile", lightspeedFile);
    formData.append("shopifyFile", shopifyFile);

    const response = await fetch(
        "/api/reconciliation?detailed=true",
        {
            method: "POST",
            body: formData,
            signal,
        }
    );

    if (!response.ok) {
        const message = await response.text();

        throw new ApiError(
            response.status,
            message || `Reconciliation failed (${response.status})`
        );
    }

    return (await response.json()) as ReconciliationDetailedResponse;
}
