import {ArrowUpRight, History} from "lucide-react";
import {Link} from "react-router";

import type {ReconciliationRun} from "../../types/reconciliation";

interface RecentReconciliationsProps {
    runs: ReconciliationRun[];
}

function formatDate(date: string): string {
    return new Intl.DateTimeFormat("en-CA", {
        year: "numeric",
        month: "short",
        day: "numeric",
        hour: "numeric",
        minute: "2-digit",
    }).format(new Date(date));
}

export default function RecentReconciliations({
                                                  runs,
                                              }: RecentReconciliationsProps) {
    const recentRuns = runs.slice(0, 5);

    return (
        <section>
            <div className="mb-4 flex items-center justify-between">
                <div>
                    <h3 className="text-base font-semibold text-slate-900 dark:text-white">
                        Recent Reconciliations
                    </h3>

                    <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                        Your latest inventory comparisons
                    </p>
                </div>

                <Link
                    to="/history"
                    className="flex items-center gap-1 text-sm font-medium text-blue-600 hover:text-blue-700 dark:text-blue-400"
                >
                    View all
                    <ArrowUpRight size={16}/>
                </Link>
            </div>

            <div
                className="overflow-hidden rounded-xl border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
                {recentRuns.length === 0 ? (
                    <div className="flex flex-col items-center px-6 py-12 text-center">
                        <History
                            size={28}
                            className="text-slate-400"
                        />

                        <p className="mt-3 text-sm font-medium text-slate-900 dark:text-white">
                            No reconciliation history
                        </p>

                        <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                            Completed reconciliations will appear here.
                        </p>
                    </div>
                ) : (
                    <div className="overflow-x-auto">
                        <table className="w-full text-left text-sm">
                            <thead
                                className="border-b border-slate-200 bg-slate-50 text-xs uppercase tracking-wide text-slate-500 dark:border-slate-800 dark:bg-slate-800/50 dark:text-slate-400">
                            <tr>
                                <th scope="col" className="px-6 py-4 font-medium">
                                    Date
                                </th>

                                <th scope="col" className="px-6 py-4 font-medium">
                                    Shopify Items
                                </th>

                                <th scope="col" className="px-6 py-4 font-medium">
                                    Coverage
                                </th>

                                <th scope="col" className="px-6 py-4 font-medium">
                                    Unmatched
                                </th>

                                <th scope="col" className="px-6 py-4 font-medium">
                                    Updates
                                </th>
                            </tr>
                            </thead>

                            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                            {recentRuns.map((run) => (
                                <tr
                                    key={run.id}
                                    className="transition-colors hover:bg-slate-50 dark:hover:bg-slate-800/50"
                                >
                                    <td className="whitespace-nowrap px-6 py-4 font-medium text-slate-900 dark:text-white">
                                        {formatDate(run.createdAt)}
                                    </td>

                                    <td className="px-6 py-4 text-slate-600 dark:text-slate-300">
                                        {run.shopifyItemCount.toLocaleString()}
                                    </td>

                                    <td className="px-6 py-4">
                                            <span className="font-medium text-slate-900 dark:text-white">
                                                {run.shopifyCoverage.toFixed(1)}%
                                            </span>
                                    </td>

                                    <td className="px-6 py-4 text-slate-600 dark:text-slate-300">
                                        {run.unmatched.toLocaleString()}
                                    </td>

                                    <td className="px-6 py-4 text-slate-600 dark:text-slate-300">
                                        {run.inventoryUpdatesGenerated.toLocaleString()}
                                    </td>
                                </tr>
                            ))}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>
        </section>
    );
}