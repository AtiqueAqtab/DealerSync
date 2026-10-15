import {useState} from "react";
import {FileSpreadsheet, Upload} from "lucide-react";
import {runReconciliation} from "../../api/reconciliation";
import {Download} from "lucide-react";
import {createDiagnosticsCsv} from "../../lib/csvExport";
import ReconciliationResultsTable from "./ReconciliationResultsTable";

import type {
    ReconciliationDetailedResponse,
} from "../../types/reconciliation";

export default function ReconciliationPage() {
    const [lightspeedFile, setLightspeedFile] = useState<File | null>(null);
    const [shopifyFile, setShopifyFile] = useState<File | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [isSubmitting, setIsSubmitting] = useState(false);

    const [result, setResult] =
        useState<ReconciliationDetailedResponse | null>(null);

    const validateFile = (file: File): boolean => {
        const maxSize = 10 * 1024 * 1024;

        if (!file.name.toLowerCase().endsWith(".csv")) {
            setError("Please select a CSV file.");
            return false;
        }

        if (file.size === 0) {
            setError("The selected file is empty.");
            return false;
        }

        if (file.size > maxSize) {
            setError("CSV files must be smaller than 10 MB.");
            return false;
        }

        setError(null);
        return true;
    };

    const handleFileChange = (
        file: File | undefined,
        type: "lightspeed" | "shopify"
    ) => {
        if (!file) return;

        setResult(null);
        setError(null);

        if (!validateFile(file)) {
            if (type === "lightspeed") {
                setLightspeedFile(null);
            } else {
                setShopifyFile(null);
            }
            return;
        }

        if (type === "lightspeed") {
            setLightspeedFile(file);
        } else {
            setShopifyFile(file);
        }
    };

    const handleReconciliation = async () => {
        if (!lightspeedFile || !shopifyFile || isSubmitting) {
            return;
        }

        setIsSubmitting(true);
        setError(null);
        setResult(null);

        try {
            const response = await runReconciliation(
                lightspeedFile,
                shopifyFile
            );

            setResult(response);
        } catch (err) {
            setError(
                err instanceof Error
                    ? err.message
                    : "An unexpected error occurred."
            );
        } finally {
            setIsSubmitting(false);
        }
    };

    const handleDownloadDiagnostics = () => {
        if (!result || isSubmitting) {
            return;
        }

        const csv = createDiagnosticsCsv(result.matches);

        // UTF-8 BOM helps spreadsheet applications recognize the encoding.
        const blob = new Blob(["\uFEFF", csv], {
            type: "text/csv;charset=utf-8",
        });

        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");

        link.href = url;
        link.download = "dealersync_diagnostics.csv";

        document.body.appendChild(link);
        link.click();
        link.remove();

        window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    };

    return (
        <div className="mx-auto max-w-7xl space-y-8">
            <section>
                <h2 className="text-xl font-semibold text-slate-900 dark:text-white">
                    Inventory Reconciliation
                </h2>

                <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
                    Upload your Lightspeed and Shopify inventory exports
                    to compare quantities and identify discrepancies.
                </p>
            </section>

            <div className="grid gap-6 md:grid-cols-2">
                {[
                    {
                        title: "Lightspeed Inventory",
                        description: "Upload your Lightspeed EVO 505 report.",
                        type: "lightspeed" as const,
                        file: lightspeedFile,
                    },
                    {
                        title: "Shopify Inventory",
                        description: "Upload your Shopify inventory export.",
                        type: "shopify" as const,
                        file: shopifyFile,
                    },
                ].map((item) => (
                    <div
                        key={item.type}
                        className="rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900"
                    >
                        <div className="mb-4 flex items-center gap-3">
                            <FileSpreadsheet
                                size={24}
                                className="text-blue-600"
                            />

                            <div>
                                <h3 className="font-semibold text-slate-900 dark:text-white">
                                    {item.title}
                                </h3>

                                <p className="text-sm text-slate-500 dark:text-slate-400">
                                    {item.description}
                                </p>
                            </div>
                        </div>

                        <label
                            className="flex min-h-40 cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed border-slate-300 p-6 text-center transition-colors hover:border-blue-500 dark:border-slate-700 dark:hover:border-blue-500">
                            <Upload
                                size={28}
                                className="mb-3 text-slate-400"
                            />

                            <span className="text-sm font-medium text-slate-700 dark:text-slate-200">
                                {item.file
                                    ? item.file.name
                                    : "Click to select a CSV file"}
                            </span>

                            <span className="mt-2 text-xs text-slate-500 dark:text-slate-400">
                                CSV files up to 10 MB
                            </span>

                            <input
                                type="file"
                                accept=".csv,text/csv"
                                disabled={isSubmitting}
                                className="sr-only"
                                onChange={(event) => {
                                    handleFileChange(
                                        event.target.files?.[0],
                                        item.type
                                    );
                                    event.target.value = "";
                                }}
                            />
                        </label>
                    </div>
                ))}
            </div>

            {error && (
                <p
                    role="alert"
                    className="text-sm text-red-600 dark:text-red-400"
                >
                    {error}
                </p>
            )}

            <div className="flex justify-end">
                <button
                    type="button"
                    onClick={() => void handleReconciliation()}
                    disabled={
                        !lightspeedFile ||
                        !shopifyFile ||
                        isSubmitting
                    }
                    className="rounded-lg bg-blue-600 px-6 py-3 text-sm font-semibold text-white
                    transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50">
                    {isSubmitting
                        ? "Running Reconciliation..."
                        : "Run Reconciliation"}
                </button>
            </div>
            {result && (
                <section
                    className="rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
                    <div className="mb-6">
                        <h2 className="text-lg font-semibold text-slate-900 dark:text-white">
                            Reconciliation Results
                        </h2>

                        <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                            Inventory comparison completed successfully.
                        </p>
                    </div>

                    <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
                        <div>
                            <p className="text-sm text-slate-500 dark:text-slate-400">
                                Shopify Coverage
                            </p>

                            <p className="mt-2 text-3xl font-semibold text-slate-900 dark:text-white">
                                {result.summary.shopifyCoverage.toFixed(1)}%
                            </p>
                        </div>

                        <div>
                            <p className="text-sm text-slate-500 dark:text-slate-400">
                                Matched Products
                            </p>

                            <p className="mt-2 text-3xl font-semibold text-slate-900 dark:text-white">
                                {(
                                    result.summary.exactMatches +
                                    result.summary.verifiedMatches +
                                    result.summary.normalizedMatches
                                ).toLocaleString()}
                            </p>
                        </div>

                        <div>
                            <p className="text-sm text-slate-500 dark:text-slate-400">
                                Unmatched Products
                            </p>

                            <p className="mt-2 text-3xl font-semibold text-amber-600 dark:text-amber-400">
                                {result.summary.unmatched.toLocaleString()}
                            </p>
                        </div>

                        <div>
                            <p className="text-sm text-slate-500 dark:text-slate-400">
                                Eligible Updates
                            </p>

                            <p className="mt-2 text-3xl font-semibold text-blue-600 dark:text-blue-400">
                                {result.summary.inventoryUpdatesGenerated.toLocaleString()}
                            </p>
                        </div>
                    </div>

                    <p className="mt-6 text-sm text-slate-500 dark:text-slate-400">
                        {result.matches.length.toLocaleString()} inventory records
                        included in the reconciliation results.
                    </p>

                    <div
                        className="mt-6 flex flex-wrap items-center justify-between gap-4 border-t border-slate-200 pt-5 dark:border-slate-800">
                        <p className="text-xs text-slate-500 dark:text-slate-400">
                            Diagnostics report only. Not for Shopify inventory import.
                        </p>

                        <button
                            type="button"
                            onClick={handleDownloadDiagnostics}
                            disabled={isSubmitting}
                            className="inline-flex items-center gap-2 rounded-lg border border-slate-300 px-4 py-2.5 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                        >
                            <Download size={17}/>
                            Download Diagnostics CSV
                        </button>
                    </div>
                </section>
            )}
            {result && (
                <ReconciliationResultsTable matches={result.matches}/>
            )}
        </div>
    );
}