import {useEffect, useState} from "react";
import {getReconciliationHistory} from "../api/reconciliation";
import type {ReconciliationRun} from "../types/reconciliation";

interface ReconciliationHistoryState {
    runs: ReconciliationRun[];
    loading: boolean;
    error: string | null;
}

export function useReconciliationHistory(): ReconciliationHistoryState {
    const [runs, setRuns] = useState<ReconciliationRun[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const controller = new AbortController();

        async function loadHistory() {
            try {
                setLoading(true);

                const data = await getReconciliationHistory();

                if (!controller.signal.aborted) {
                    setRuns(data);
                    setError(null);
                }
            } catch {
                if (!controller.signal.aborted) {
                    setError("Unable to load reconciliation history.");
                }
            } finally {
                if (!controller.signal.aborted) {
                    setLoading(false);
                }
            }
        }

        void loadHistory();

        return () => controller.abort();
    }, []);

    return {runs, loading, error};
}