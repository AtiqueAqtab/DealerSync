export interface SkuMapping {
    id: string;
    lightspeedPartNumber: string;
    shopifySku: string;
    createdAt: string;
    updatedAt: string;
    isVerified: boolean;
}

export interface CreateSkuMappingRequest {
    lightspeedPartNumber: string;
    shopifySku: string;
}