import {useState} from "react";

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
    const [result, setResult] =
        useState<ReconciliationDetailedResponse | null>(null);

    return (
        <ReconciliationContext.Provider value={{result, setResult}}>
            {children}
        </ReconciliationContext.Provider>
    );
}