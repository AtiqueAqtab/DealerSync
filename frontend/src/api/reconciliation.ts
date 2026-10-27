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

    let response: Response;
    try {
        response = await fetch(
            "/api/reconciliation?detailed=true",
            {
                method: "POST",
                body: formData,
                signal,
            }
        );
    } catch (error) {
        if (signal?.aborted) throw error;
        throw new ApiError(0, "Could not reach the API. Check that DealerSync is running and try again.");
    }

    if (!response.ok) {
        let message = response.status >= 500
            ? "Reconciliation is unavailable. Check that the API and local database are running, then try again."
            : `Reconciliation failed (${response.status}). Check the input files and try again.`;
        if (response.status < 500) {
            try {
                const body: unknown = await response.json();
                if (body && typeof body === "object" && "error" in body && typeof body.error === "string") {
                    message = body.error;
                }
            } catch { /* A proxy error may not contain the API's JSON validation contract. */ }
        }

        throw new ApiError(
            response.status,
            message
        );
    }

    return (await response.json()) as ReconciliationDetailedResponse;
}
