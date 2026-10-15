import {Link} from "react-router";
import {useReconciliationHistory} from "../../hooks/useReconciliationHistory";
import CoverageChart from "./CoverageChart";
import RecentReconciliations from "./RecentReconciliations";

import {
    ArrowRight,
    FileUp,
    History,
    Link2,
    PackageSearch,
} from "lucide-react";

const quickActions = [
    {
        title: "Run Reconciliation",
        description: "Compare Lightspeed and Shopify inventory.",
        path: "/reconcile",
        icon: FileUp,
    },
    {
        title: "Review Inventory",
        description: "Examine matches and inventory differences.",
        path: "/inventory",
        icon: PackageSearch,
    },
    {
        title: "Manage SKU Mappings",
        description: "Review and verify SKU relationships.",
        path: "/mappings",
        icon: Link2,
    },
];

export default function DashboardPage() {
    const {runs, loading, error} = useReconciliationHistory();

    const latestRun = runs[0];

    return (
        <div className="mx-auto max-w-7xl space-y-8">

            {/* Latest Reconciliation */}
            <section>
                <h2 className="mb-4 text-lg font-semibold text-slate-900 dark:text-white">
                    Latest Reconciliation
                </h2>

                <div
                    className="rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
                    {loading ? (
                        <p className="text-sm text-slate-500 dark:text-slate-400">
                            Loading reconciliation history...
                        </p>
                    ) : error ? (
                        <p
                            role="alert"
                            className="text-sm text-red-600 dark:text-red-400"
                        >
                            {error}
                        </p>
                    ) : latestRun ? (
                        <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_260px]">

                            {/* Statistics */}
                            <div className="grid gap-6 sm:grid-cols-2">

                                <div>
                                    <p className="text-sm text-slate-500 dark:text-slate-400">
                                        Exact Matches
                                    </p>

                                    <p className="mt-2 text-3xl font-semibold text-slate-900 dark:text-white">
                                        {latestRun.exactMatches.toLocaleString()}
                                    </p>
                                </div>

                                <div>
                                    <p className="text-sm text-slate-500 dark:text-slate-400">
                                        Verified Matches
                                    </p>

                                    <p className="mt-2 text-3xl font-semibold text-slate-900 dark:text-white">
                                        {latestRun.verifiedMatches.toLocaleString()}
                                    </p>
                                </div>

                                <div>
                                    <p className="text-sm text-slate-500 dark:text-slate-400">
                                        Requires Mapping
                                    </p>

                                    <p className="mt-2 text-3xl font-semibold text-slate-900 dark:text-white">
                                        {latestRun.unmatched.toLocaleString()}
                                    </p>
                                </div>

                                <div>
                                    <p className="text-sm text-slate-500 dark:text-slate-400">
                                        Eligible Updates
                                    </p>

                                    <p className="mt-2 text-3xl font-semibold text-slate-900 dark:text-white">
                                        {latestRun.inventoryUpdatesGenerated.toLocaleString()}
                                    </p>
                                </div>

                            </div>

                            {/* Coverage Chart */}
                            <div
                                className="border-t border-slate-200 pt-6 lg:border-t-0 lg:border-l lg:pt-0 lg:pl-8 dark:border-slate-800">

                                <CoverageChart
                                    coverage={latestRun.shopifyCoverage}
                                />

                                {/* Match Breakdown */}
                                <div className="mt-6 space-y-3 border-t border-slate-200 pt-5 dark:border-slate-800">

                                    <div className="flex items-center justify-between text-sm">
                                        <span className="text-slate-500 dark:text-slate-400">
                                            Exact
                                        </span>

                                        <span className="font-medium text-slate-900 dark:text-white">
                                            {latestRun.exactMatches.toLocaleString()}
                                        </span>
                                    </div>

                                    <div className="flex items-center justify-between text-sm">
                                        <span className="text-slate-500 dark:text-slate-400">
                                            Verified
                                        </span>

                                        <span className="font-medium text-slate-900 dark:text-white">
                                            {latestRun.verifiedMatches.toLocaleString()}
                                        </span>
                                    </div>

                                    <div className="flex items-center justify-between text-sm">
                                        <span className="text-slate-500 dark:text-slate-400">
                                            Normalized
                                        </span>

                                        <span className="font-medium text-amber-600 dark:text-amber-400">
                                            {latestRun.normalizedMatches.toLocaleString()}
                                        </span>
                                    </div>

                                    <div className="flex items-center justify-between text-sm">
                                        <span className="text-slate-500 dark:text-slate-400">
                                            Unmatched
                                        </span>

                                        <span className="font-medium text-red-600 dark:text-red-400">
                                            {latestRun.unmatched.toLocaleString()}
                                        </span>
                                    </div>

                                </div>

                                <p className="mt-4 text-xs leading-relaxed text-slate-500 dark:text-slate-400">
                                    Coverage includes normalized matches, which
                                    require verification before inventory updates.
                                </p>

                            </div>
                        </div>
                    ) : (
                        <div className="py-8 text-center">

                            <History
                                size={32}
                                className="mx-auto text-slate-400"
                            />

                            <h3 className="mt-4 font-semibold text-slate-900 dark:text-white">
                                No reconciliations yet
                            </h3>

                            <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
                                Run a reconciliation to see your inventory statistics.
                            </p>

                            <Link
                                to="/reconcile"
                                className="mt-5 inline-flex rounded-lg bg-blue-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-blue-700"
                            >
                                Run Reconciliation
                            </Link>

                        </div>
                    )}
                </div>
            </section>

            {/* Quick Actions */}
            <section>
                <h2 className="mb-4 text-lg font-semibold text-slate-900 dark:text-white">
                    Quick Actions
                </h2>

                <div className="grid gap-4 md:grid-cols-3">
                    {quickActions.map((action) => {
                        const Icon = action.icon;

                        return (
                            <Link
                                key={action.path}
                                to={action.path}
                                className="group rounded-xl border border-slate-200 bg-white p-5 transition-colors hover:border-blue-300 hover:bg-slate-50 dark:border-slate-800 dark:bg-slate-900 dark:hover:border-blue-700 dark:hover:bg-slate-800"
                            >

                                <div
                                    className="mb-4 flex h-11 w-11 items-center justify-center rounded-lg bg-blue-50 text-blue-600 dark:bg-blue-500/10 dark:text-blue-400">
                                    <Icon size={21}/>
                                </div>

                                <h3 className="font-semibold text-slate-900 dark:text-white">
                                    {action.title}
                                </h3>

                                <p className="mt-2 text-sm leading-relaxed text-slate-500 dark:text-slate-400">
                                    {action.description}
                                </p>

                                <div
                                    className="mt-5 flex items-center gap-2 text-sm font-medium text-blue-600 dark:text-blue-400">
                                    Open

                                    <ArrowRight
                                        size={16}
                                        className="transition-transform group-hover:translate-x-1"
                                    />
                                </div>

                            </Link>
                        );
                    })}
                </div>
            </section>

            {/* Recent Reconciliations */}
            {!loading && !error && (
                <RecentReconciliations runs={runs}/>
            )}

        </div>
    );
}