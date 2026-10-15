import {ApiError, apiGet} from "./client";

import type {
    SkuMapping,
    CreateSkuMappingRequest,
} from "../types/skuMapping";

export async function getSkuMappings(): Promise<SkuMapping[]> {
    return apiGet<SkuMapping[]>("/api/sku-mappings");
}

export async function getSkuMapping(
    id: string
): Promise<SkuMapping> {
    return apiGet<SkuMapping>(`/api/sku-mappings/${encodeURIComponent(id)}`);
}

async function readApiError(response: Response): Promise<string> {
    try {
        const body: unknown = await response.json();

        if (
            typeof body === "object" &&
            body !== null &&
            "error" in body &&
            typeof body.error === "string"
        ) {
            return body.error;
        }
    } catch {
        // The API may return an empty or non-JSON error response.
    }

    return `Request failed (${response.status})`;
}

export async function createSkuMapping(
    request: CreateSkuMappingRequest
): Promise<SkuMapping> {
    const response = await fetch("/api/sku-mappings", {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            Accept: "application/json",
        },
        body: JSON.stringify(request),
    });

    if (!response.ok) {
        throw new ApiError(
            response.status,
            await readApiError(response)
        );
    }

    return (await response.json()) as SkuMapping;
}

export async function verifySkuMapping(
    id: string
): Promise<SkuMapping> {
    const response = await fetch(
        `/api/sku-mappings/${encodeURIComponent(id)}/verify`,
        {
            method: "PATCH",
            headers: {
                Accept: "application/json",
            },
        }
    );

    if (!response.ok) {
        throw new ApiError(
            response.status,
            await readApiError(response)
        );
    }

    return (await response.json()) as SkuMapping;
}