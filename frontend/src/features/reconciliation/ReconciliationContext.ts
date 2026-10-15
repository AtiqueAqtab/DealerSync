import {createContext} from "react";

import type {
    ReconciliationDetailedResponse,
} from "../../types/reconciliation";

export interface ReconciliationContextValue {
    result: ReconciliationDetailedResponse | null;
    setResult: (result: ReconciliationDetailedResponse | null) => void;
}

export const ReconciliationContext =
    createContext<ReconciliationContextValue | undefined>(undefined);