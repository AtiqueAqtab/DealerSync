import {useMemo, useState} from "react";
import type {ReconciliationMatch} from "../../types/reconciliation";

interface ReconciliationResultsTableProps {
    matches: ReconciliationMatch[];
}

type MatchFilter = "all" | "review" | "unmatched" | "ready" | "approved" | "multiple";

function formatReviewReason(reason: string): string {
    switch (reason) {
        case "UnverifiedSupplier":
            return "Supplier mapping is unverified";
        case "MissingSupplierInventory":
            return "Mapped supplier inventory is missing";
        default:
            return reason;
    }
}

export default function ReconciliationResultsTable({
                                                       matches,
                                                   }: ReconciliationResultsTableProps) {
    const [search, setSearch] = useState("");
    const [filter, setFilter] = useState<MatchFilter>("all");
    const [page, setPage] = useState(1);

    const pageSize = 25;

    const filteredMatches = useMemo(() => {
        const query = search.trim().toLowerCase();

        return matches.filter((match) => {
            const matchesSearch =
                match.shopifySku.toLowerCase().includes(query) ||
                match.shopifyTitle.toLowerCase().includes(query) ||
                (match.lightspeedPartNumber ?? "")
                    .toLowerCase()
                    .includes(query) ||
                match.location.toLowerCase().includes(query) ||
                match.contributors.some((contributor) =>
                    contributor.partNumber.toLowerCase().includes(query)
                );

            const matchesFilter =
                filter === "all" ||
                (filter === "review" && match.requiresReview) ||
                (filter === "unmatched" &&
                    match.diagnosticStatus === "MappingRequired") ||
                (filter === "ready" &&
                    match.diagnosticStatus === "Ready") ||
                (filter === "approved" &&
                    match.proposedQuantity !== null && !match.requiresReview) ||
                (filter === "multiple" && match.contributors.length > 1);

            return matchesSearch && matchesFilter;
        });
    }, [matches, search, filter]);

    const pageCount = Math.max(
        1,
        Math.ceil(filteredMatches.length / pageSize)
    );

    const currentPage = Math.min(page, pageCount);

    const visibleMatches = filteredMatches.slice(
        (currentPage - 1) * pageSize,
        currentPage * pageSize
    );

    const handleSearchChange = (value: string) => {
        setSearch(value);
        setPage(1);
    };

    const handleFilterChange = (value: MatchFilter) => {
        setFilter(value);
        setPage(1);
    };

    return (
        <section className="space-y-4">
            <div>
                <h2 className="text-lg font-semibold text-slate-900 dark:text-white">
                    Inventory Matches
                </h2>

                <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                    Review matched products, quantity differences,
                    and records requiring attention.
                </p>
                <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                    Combined quantity is supplier inventory. Approved quantity appears
                    only when the backend generated an update; a dash means no approved update.
                </p>
            </div>

            {/* Search and filters */}
            <div className="flex flex-wrap gap-3">
                <input
                    type="search"
                    value={search}
                    onChange={(event) =>
                        handleSearchChange(event.target.value)
                    }
                    placeholder="Search SKU, product, supplier part, or location..."
                    aria-label="Search inventory matches"
                    className="min-w-0 flex-1 rounded-lg border border-slate-300 bg-white px-4 py-2.5 text-sm text-slate-900 outline-none focus:border-blue-500 dark:border-slate-700 dark:bg-slate-900 dark:text-white"
                />

                <select
                    value={filter}
                    onChange={(event) =>
                        handleFilterChange(
                            event.target.value as MatchFilter
                        )
                    }
                    aria-label="Filter inventory matches"
                    className="rounded-lg border border-slate-300 bg-white px-4 py-2.5 text-sm text-slate-900 dark:border-slate-700 dark:bg-slate-900 dark:text-white"
                >
                    <option value="all">All Products</option>
                    <option value="review">Requires Review</option>
                    <option value="unmatched">Mapping Required</option>
                    <option value="ready">Ready</option>
                    <option value="approved">Approved Updates</option>
                    <option value="multiple">Multiple Suppliers</option>
                </select>
            </div>

            {/* Results table */}
            <div
                className="overflow-hidden rounded-xl border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
                <div className="overflow-x-auto">
                    <table className="w-full text-left text-sm">
                        <thead
                            className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500 dark:bg-slate-800/50 dark:text-slate-400">
                        <tr>
                            <th scope="col" className="px-5 py-4">
                                Shopify SKU
                            </th>
                            <th scope="col" className="px-5 py-4">
                                Supplier Contributions
                            </th>
                            <th scope="col" className="px-5 py-4">
                                Match Type
                            </th>
                            <th scope="col" className="px-5 py-4">
                                Current Qty
                            </th>
                            <th scope="col" className="px-5 py-4">
                                Combined Qty
                            </th>
                            <th scope="col" className="px-5 py-4">
                                Approved Qty
                            </th>
                            <th scope="col" className="px-5 py-4">
                                Status
                            </th>
                        </tr>
                        </thead>

                        <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                        {visibleMatches.map((match, index) => (
                            <tr
                                key={`${match.shopifySku}-${match.location}-${index}`}
                                className="hover:bg-slate-50 dark:hover:bg-slate-800/50"
                            >
                                <td className="px-5 py-4">
                                    <div className="font-medium text-slate-900 dark:text-white">
                                        {match.shopifySku}
                                    </div>

                                    <div className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                                        {match.shopifyTitle}
                                    </div>
                                    <div className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                                        Location: {match.location}
                                    </div>
                                </td>

                                <td className="px-5 py-4 text-slate-600 dark:text-slate-300">
                                    {match.contributors.length > 0 ? (
                                        <ul className="min-w-48 space-y-2">
                                            {match.contributors.map((contributor, contributorIndex) => (
                                                <li key={`${contributor.partNumber}-${contributorIndex}`}>
                                                    <div className="font-medium">{contributor.partNumber}</div>
                                                    <div className="text-xs text-slate-500 dark:text-slate-400">
                                                        Qty: {contributor.quantity.toLocaleString()} · {contributor.matchType}
                                                    </div>
                                                </li>
                                            ))}
                                        </ul>
                                    ) : (match.lightspeedPartNumber ?? "—")}
                                </td>

                                <td className="px-5 py-4 text-slate-600 dark:text-slate-300">
                                    {match.matchType}
                                </td>

                                <td className="px-5 py-4 text-slate-600 dark:text-slate-300">
                                    {match.currentQuantity}
                                </td>

                                <td className="px-5 py-4 text-slate-600 dark:text-slate-300">
                                    {match.combinedQuantity?.toLocaleString() ?? "—"}
                                </td>

                                <td className="px-5 py-4 text-slate-600 dark:text-slate-300">
                                    {match.proposedQuantity?.toLocaleString() ?? "—"}
                                </td>

                                <td className="px-5 py-4">
                                        <span
                                            className={
                                                match.requiresReview
                                                    ? "font-medium text-amber-600 dark:text-amber-400"
                                                    : match.diagnosticStatus === "Ready"
                                                        ? "font-medium text-green-600 dark:text-green-400"
                                                        : match.diagnosticStatus === "ReviewRecommended"
                                                            ? "font-medium text-amber-600 dark:text-amber-400"
                                                            : "font-medium text-red-600 dark:text-red-400"
                                            }
                                        >
                                            {match.diagnosticStatus}
                                        </span>
                                    {match.requiresReview && (
                                        <p className="mt-1 text-xs font-medium text-amber-700 dark:text-amber-400">
                                            Requires review
                                        </p>
                                    )}
                                    {match.reviewReasons.length > 0 && (
                                        <ul className="mt-2 min-w-48 list-disc space-y-1 pl-4 text-xs text-amber-700 dark:text-amber-400">
                                            {match.reviewReasons.map((reason, reasonIndex) => (
                                                <li key={`${reason}-${reasonIndex}`}>
                                                    {formatReviewReason(reason)}
                                                </li>
                                            ))}
                                        </ul>
                                    )}
                                </td>
                            </tr>
                        ))}
                        </tbody>
                    </table>
                </div>

                {filteredMatches.length === 0 && (
                    <p className="px-6 py-10 text-center text-sm text-slate-500 dark:text-slate-400">
                        No inventory records match your search or filter.
                    </p>
                )}

                {/* Pagination */}
                <div
                    className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 px-5 py-4 text-sm dark:border-slate-800">
                    <span className="text-slate-500 dark:text-slate-400">
                        Showing {visibleMatches.length} of{" "}
                        {filteredMatches.length.toLocaleString()} records
                    </span>

                    <div className="flex items-center gap-3">
                        <button
                            type="button"
                            disabled={currentPage === 1}
                            onClick={() =>
                                setPage(Math.max(1, currentPage - 1))
                            }
                            className="rounded-md border border-slate-300 px-3 py-1.5 text-slate-700 disabled:opacity-40 dark:border-slate-700 dark:text-slate-200"
                        >
                            Previous
                        </button>

                        <span className="text-slate-600 dark:text-slate-300">
                            {currentPage} / {pageCount}
                        </span>

                        <button
                            type="button"
                            disabled={currentPage === pageCount}
                            onClick={() =>
                                setPage(Math.min(pageCount, currentPage + 1))
                            }
                            className="rounded-md border border-slate-300 px-3 py-1.5 text-slate-700 disabled:opacity-40 dark:border-slate-700 dark:text-slate-200"
                        >
                            Next
                        </button>
                    </div>
                </div>
            </div>
        </section>
    );
}