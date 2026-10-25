import {useCallback, useRef, useState} from "react";

import {
    ReconciliationContext,
} from "./ReconciliationContext";

import type {
    ReactNode,
} from "react";

import type {
    ReconciliationDetailedResponse,
} from "../../types/reconciliation";

interface ReconciliationProviderProps {
    children: ReactNode;
}

export default function ReconciliationProvider({
                                                   children,
                                               }: ReconciliationProviderProps) {
    const [result, storeResult] =
        useState<ReconciliationDetailedResponse | null>(null);
    const version = useRef(0);
    const setResult = useCallback((next: ReconciliationDetailedResponse | null) => {
        // Invalidation must also reject responses from work started before the change.
        if (next === null) version.current++;
        storeResult(next);
    }, []);
    const getResultVersion = useCallback(() => version.current, []);

    return (
        <ReconciliationContext.Provider value={{result, setResult, getResultVersion}}>
            {children}
        </ReconciliationContext.Provider>
    );
}
