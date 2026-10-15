import {useCallback, useEffect, useState} from "react";

import {getSkuMappings} from "../api/skuMappings";

import type {SkuMapping} from "../types/skuMapping";

export function useSkuMappings() {
    const [mappings, setMappings] = useState<SkuMapping[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const refreshMappings = useCallback(async () => {
        setLoading(true);
        setError(null);

        try {
            const data = await getSkuMappings();
            setMappings(data);
        } catch (err) {
            setError(
                err instanceof Error
                    ? err.message
                    : "Unable to load SKU mappings."
            );
        } finally {
            setLoading(false);
        }
    }, []);

    useEffect(() => {
        let cancelled = false;

        async function loadMappings() {
            try {
                const data = await getSkuMappings();

                if (!cancelled) {
                    setMappings(data);
                    setError(null);
                }
            } catch (err) {
                if (!cancelled) {
                    setError(
                        err instanceof Error
                            ? err.message
                            : "Unable to load SKU mappings."
                    );
                }
            } finally {
                if (!cancelled) {
                    setLoading(false);
                }
            }
        }

        void loadMappings();

        return () => {
            cancelled = true;
        };
    }, []);

    return {
        mappings,
        loading,
        error,
        refreshMappings,
    };
}