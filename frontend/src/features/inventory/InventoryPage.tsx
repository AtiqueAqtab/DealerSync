import {Link} from "react-router";

import {
    AlertTriangle,
    ArrowRight,
    CheckCircle2,
    FileSearch,
    Link2,
    RefreshCw,
} from "lucide-react";

import {useReconciliation} from "../../hooks/useReconciliation";

import ReconciliationResultsTable from "../reconciliation/ReconciliationResultsTable";

export default function InventoryPage() {
    const {result} = useReconciliation();

    if (!result) {
        return (
            <div className="mx-auto max-w-7xl space-y-6">
                <div>
                    <h2 className="text-xl font-semibold text-slate-900 dark:text-white">
                        Inventory Review
                    </h2>

                    <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
                        Review inventory matches, quantity differences,
                        and records requiring attention.
                    </p>
                </div>

                <div
                    className="rounded-xl border border-slate-200 bg-white px-6 py-16 text-center dark:border-slate-800 dark:bg-slate-900">
                    <FileSearch
                        size={40}
                        className="mx-auto text-slate-400"
                    />

                    <h3 className="mt-4 text-lg font-semibold text-slate-900 dark:text-white">
                        No inventory results available
                    </h3>

                    <p className="mx-auto mt-2 max-w-md text-sm leading-relaxed text-slate-500 dark:text-slate-400">
                        Run an inventory reconciliation to review
                        matches between Lightspeed and Shopify.
                    </p>

                    <Link
                        to="/reconcile"
                        className="mt-6 inline-flex items-center gap-2 rounded-lg bg-blue-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-blue-700"
                    >
                        Run Reconciliation
                        <ArrowRight size={16}/>
                    </Link>
                </div>
            </div>
        );
    }

    const {summary, matches} = result;

    const attentionRequired = matches.filter(
        (match) =>
            match.diagnosticStatus === "ReviewRecommended" ||
            match.diagnosticStatus === "MappingRequired" ||
            match.diagnosticStatus === "InvalidQuantity"
    ).length;

    const readyRecords = matches.filter(
        (match) => match.diagnosticStatus === "Ready"
    ).length;

    return (
        <div className="mx-auto max-w-7xl space-y-8">

            {/* Page introduction */}
            <section className="flex flex-wrap items-start justify-between gap-4">
                <div>
                    <h2 className="text-xl font-semibold text-slate-900 dark:text-white">
                        Inventory Review
                    </h2>

                    <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
                        Examine inventory matches and identify records
                        requiring review before generating Shopify updates.
                    </p>
                </div>

                <Link
                    to="/reconcile"
                    className="inline-flex items-center gap-2 rounded-lg border border-slate-300 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200 dark:hover:bg-slate-800"
                >
                    <RefreshCw size={16}/>
                    New Reconciliation
                </Link>
            </section>

            {/* Inventory overview */}
            <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">

                <div
                    className="rounded-xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
                    <p className="text-sm text-slate-500 dark:text-slate-400">
                        Shopify Records
                    </p>

                    <p className="mt-2 text-3xl font-semibold text-slate-900 dark:text-white">
                        {summary.shopifyItemCount.toLocaleString()}
                    </p>
                </div>

                <div
                    className="rounded-xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
                    <div className="flex items-center gap-2">
                        <CheckCircle2 size={17} className="text-emerald-500"/>

                        <p className="text-sm text-slate-500 dark:text-slate-400">
                            Ready Records
                        </p>
                    </div>

                    <p className="mt-2 text-3xl font-semibold text-slate-900 dark:text-white">
                        {readyRecords.toLocaleString()}
                    </p>
                </div>

                <div
                    className="rounded-xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
                    <div className="flex items-center gap-2">
                        <AlertTriangle size={17} className="text-amber-500"/>

                        <p className="text-sm text-slate-500 dark:text-slate-400">
                            Requires Attention
                        </p>
                    </div>

                    <p className="mt-2 text-3xl font-semibold text-amber-600 dark:text-amber-400">
                        {attentionRequired.toLocaleString()}
                    </p>
                </div>

                <div
                    className="rounded-xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
                    <p className="text-sm text-slate-500 dark:text-slate-400">
                        Eligible Updates
                    </p>

                    <p className="mt-2 text-3xl font-semibold text-blue-600 dark:text-blue-400">
                        {summary.inventoryUpdatesGenerated.toLocaleString()}
                    </p>
                </div>

            </section>

            {/* Diagnostic information */}
            <section
                className="rounded-xl border border-amber-200 bg-amber-50 p-5 dark:border-amber-900/50 dark:bg-amber-950/20">
                <div className="flex items-start gap-3">
                    <AlertTriangle
                        size={20}
                        className="mt-0.5 shrink-0 text-amber-600 dark:text-amber-400"
                    />

                    <div>
                        <h3 className="font-semibold text-amber-900 dark:text-amber-200">
                            Review inventory matches before updating Shopify
                        </h3>

                        <p className="mt-2 text-sm leading-relaxed text-amber-800 dark:text-amber-300">
                            Normalized matches are suggestions and require
                            verification. Unmatched products require SKU
                            mapping, while invalid quantities must be resolved.
                            Reviewing results does not modify Shopify inventory.
                        </p>
                    </div>
                </div>
            </section>

            {/* Inventory results */}
            <ReconciliationResultsTable matches={matches}/>

            {/* Mapping navigation */}
            <section
                className="rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
                <div className="flex flex-wrap items-center justify-between gap-4">
                    <div className="flex items-start gap-3">
                        <Link2
                            size={22}
                            className="mt-0.5 text-blue-600 dark:text-blue-400"
                        />

                        <div>
                            <h3 className="font-semibold text-slate-900 dark:text-white">
                                Resolve unmatched products
                            </h3>

                            <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                                Manage verified relationships between
                                Shopify SKUs and Lightspeed part numbers.
                            </p>
                        </div>
                    </div>

                    <Link
                        to="/mappings"
                        className="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-blue-700"
                    >
                        Manage Mappings
                        <ArrowRight size={16}/>
                    </Link>
                </div>
            </section>

        </div>
    );
}