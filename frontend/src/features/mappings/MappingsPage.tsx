import {useMemo, useRef, useState} from "react";

import {Link2, RefreshCw, Search} from "lucide-react";

import {useSkuMappings} from "../../hooks/useSkuMappings";

import CreateMappingForm from "./CreateMappingForm";
import {verifySkuMapping} from "../../api/skuMappings";
import {useReconciliation} from "../../hooks/useReconciliation";
import type {SkuMapping} from "../../types/skuMapping";

type MappingFilter = "all" | "verified" | "unverified";

export default function MappingsPage() {
    const {
        mappings,
        loading,
        error,
        refreshMappings,
        updateMapping,
    } = useSkuMappings();

    const [search, setSearch] = useState("");
    const [filter, setFilter] = useState<MappingFilter>("all");
    const [verifyingId, setVerifyingId] = useState<string | null>(null);
    const verificationInProgress = useRef(false);
    const [verificationError, setVerificationError] = useState<string | null>(null);
    const [verificationSuccess, setVerificationSuccess] = useState<string | null>(null);
    const {setResult} = useReconciliation();

    const handleVerify = async (mapping: SkuMapping) => {
        if (mapping.isVerified || verificationInProgress.current || loading) return;

        verificationInProgress.current = true;
        setVerifyingId(mapping.id);
        setVerificationError(null);
        setVerificationSuccess(null);

        try {
            const updated = await verifySkuMapping(mapping.id);
            if (updated.id !== mapping.id || !updated.isVerified) {
                throw new Error("Verification was not confirmed. Refresh the mappings and try again.");
            }
            updateMapping(updated);
            // A mapping change makes the previous reconciliation results stale.
            setResult(null);
            setVerificationSuccess(
                `Verified ${updated.lightspeedPartNumber} for ${updated.shopifySku}. Run reconciliation again to use this mapping.`
            );
        } catch (err) {
            setVerificationError(
                `Could not verify ${mapping.lightspeedPartNumber}: ${err instanceof Error ? err.message : "An unexpected error occurred."}`
            );
        } finally {
            verificationInProgress.current = false;
            setVerifyingId(null);
        }
    };

    const verifiedCount = mappings.filter(
        (mapping) => mapping.isVerified
    ).length;

    const unverifiedCount = mappings.length - verifiedCount;

    const filteredMappings = useMemo(() => {
        const query = search.trim().toLowerCase();

        return mappings.filter((mapping) => {
            const matchesSearch =
                mapping.shopifySku.toLowerCase().includes(query) ||
                mapping.lightspeedPartNumber.toLowerCase().includes(query);

            const matchesFilter =
                filter === "all" ||
                (filter === "verified" && mapping.isVerified) ||
                (filter === "unverified" && !mapping.isVerified);

            return matchesSearch && matchesFilter;
        });
    }, [mappings, search, filter]);

    return (
        <div className="mx-auto max-w-7xl space-y-8">

            {/* Header */}
            <section className="flex flex-wrap items-start justify-between gap-4">
                <div>
                    <h2 className="text-xl font-semibold text-slate-900 dark:text-white">
                        SKU Mapping Management
                    </h2>

                    <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
                        Manage relationships between Lightspeed part numbers
                        and Shopify SKUs.
                    </p>
                </div>

                <button
                    type="button"
                    onClick={() => void refreshMappings()}
                    disabled={loading || verifyingId !== null}
                    className="inline-flex items-center gap-2 rounded-lg border border-slate-300 px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-100 disabled:opacity-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                >
                    <RefreshCw size={16}/>
                    Refresh
                </button>
            </section>

            <p className="text-sm text-slate-500 dark:text-slate-400">
                Verify a mapping after confirming that both identifiers refer to the same product.
                Verification makes it eligible for future reconciliation runs.
            </p>

            {verificationError && (
                <p role="alert" className="text-sm text-red-600 dark:text-red-400">
                    {verificationError}
                </p>
            )}
            {verificationSuccess && (
                <p role="status" className="text-sm text-emerald-600 dark:text-emerald-400">
                    {verificationSuccess}
                </p>
            )}

            {/* Statistics */}
            <section className="grid gap-4 sm:grid-cols-3">
                {[
                    {label: "Total Mappings", value: mappings.length},
                    {label: "Verified", value: verifiedCount},
                    {label: "Awaiting Verification", value: unverifiedCount},
                ].map((stat) => (
                    <div
                        key={stat.label}
                        className="rounded-xl border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900"
                    >
                        <p className="text-sm text-slate-500 dark:text-slate-400">
                            {stat.label}
                        </p>

                        <p className="mt-2 text-3xl font-semibold text-slate-900 dark:text-white">
                            {stat.value.toLocaleString()}
                        </p>
                    </div>
                ))}
            </section>

            {/* Create Mapping */}
            <CreateMappingForm onCreated={refreshMappings}/>

            {/* Search and filtering */}
            <section className="space-y-4">
                <div className="flex flex-wrap gap-3">

                    <div className="relative min-w-0 flex-1">
                        <Search
                            size={18}
                            className="pointer-events-none absolute top-3 left-3 text-slate-400"
                        />

                        <input
                            type="search"
                            value={search}
                            onChange={(event) =>
                                setSearch(event.target.value)
                            }
                            placeholder="Search Shopify SKU or Lightspeed part number"
                            aria-label="Search SKU mappings"
                            className="w-full rounded-lg border border-slate-300 bg-white py-2.5 pr-4 pl-10 text-sm text-slate-900 dark:border-slate-700 dark:bg-slate-900 dark:text-white"
                        />
                    </div>

                    <select
                        value={filter}
                        onChange={(event) =>
                            setFilter(event.target.value as MappingFilter)
                        }
                        aria-label="Filter mapping verification status"
                        className="rounded-lg border border-slate-300 bg-white px-4 py-2.5 text-sm text-slate-900 dark:border-slate-700 dark:bg-slate-900 dark:text-white"
                    >
                        <option value="all">All Mappings</option>
                        <option value="verified">Verified</option>
                        <option value="unverified">Unverified</option>
                    </select>
                </div>

                {/* Mapping table */}
                <div
                    className="overflow-hidden rounded-xl border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">

                    {loading ? (
                        <p
                            role="status"
                            className="px-6 py-12 text-center text-sm text-slate-500 dark:text-slate-400"
                        >
                            Loading SKU mappings...
                        </p>
                    ) : error ? (
                        <p
                            role="alert"
                            className="px-6 py-12 text-center text-sm text-red-600 dark:text-red-400"
                        >
                            {error}
                        </p>
                    ) : filteredMappings.length === 0 ? (
                        <div className="px-6 py-14 text-center">
                            <Link2
                                size={32}
                                className="mx-auto text-slate-400"
                            />

                            <p className="mt-3 font-medium text-slate-900 dark:text-white">
                                {mappings.length === 0
                                    ? "No SKU mappings yet"
                                    : "No matching SKU mappings"}
                            </p>

                            <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
                                {mappings.length === 0
                                    ? "Create a mapping to link a Lightspeed part number with a Shopify SKU."
                                    : "Try adjusting your search or verification filter."}
                            </p>
                        </div>
                    ) : (
                        <div className="overflow-x-auto">
                            <table className="w-full text-left text-sm">
                                <thead
                                    className="bg-slate-50 text-xs uppercase text-slate-500 dark:bg-slate-800/50 dark:text-slate-400">
                                <tr>
                                    <th scope="col" className="px-6 py-4">
                                        Shopify SKU
                                    </th>

                                    <th scope="col" className="px-6 py-4">
                                        Lightspeed Part Number
                                    </th>

                                    <th scope="col" className="px-6 py-4">
                                        Status
                                    </th>

                                    <th scope="col" className="px-6 py-4">
                                        Created
                                    </th>
                                    <th scope="col" className="px-6 py-4">
                                        Actions
                                    </th>
                                </tr>
                                </thead>

                                <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                                {filteredMappings.map((mapping) => (
                                    <tr
                                        key={mapping.id}
                                        className="hover:bg-slate-50 dark:hover:bg-slate-800/50"
                                    >
                                        <td className="px-6 py-4 font-medium text-slate-900 dark:text-white">
                                            {mapping.shopifySku}
                                        </td>

                                        <td className="px-6 py-4 text-slate-600 dark:text-slate-300">
                                            {mapping.lightspeedPartNumber}
                                        </td>

                                        <td className="px-6 py-4">
                                                <span
                                                    className={
                                                        mapping.isVerified
                                                            ? "font-medium text-emerald-600 dark:text-emerald-400"
                                                            : "font-medium text-amber-600 dark:text-amber-400"
                                                    }
                                                >
                                                    {mapping.isVerified
                                                        ? "Verified"
                                                        : "Unverified"}
                                                </span>
                                        </td>

                                        <td className="whitespace-nowrap px-6 py-4 text-slate-500 dark:text-slate-400">
                                            {new Date(mapping.createdAt).toLocaleDateString()}
                                        </td>
                                        <td className="px-6 py-4">
                                            {mapping.isVerified ? (
                                                <span className="text-sm text-slate-500 dark:text-slate-400">
                                                    Verified
                                                </span>
                                            ) : (
                                                <button
                                                    type="button"
                                                    onClick={() => void handleVerify(mapping)}
                                                    disabled={verifyingId !== null || loading}
                                                    aria-label={`Verify mapping from ${mapping.lightspeedPartNumber} to ${mapping.shopifySku}`}
                                                    aria-busy={verifyingId === mapping.id}
                                                    className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-500 disabled:cursor-not-allowed disabled:opacity-50"
                                                >
                                                    {verifyingId === mapping.id ? "Verifying..." : "Verify"}
                                                </button>
                                            )}
                                        </td>
                                    </tr>
                                ))}
                                </tbody>
                            </table>
                        </div>
                    )}
                </div>

                {!loading && !error && (
                    <p className="text-sm text-slate-500 dark:text-slate-400">
                        Showing {filteredMappings.length.toLocaleString()} of{" "}
                        {mappings.length.toLocaleString()} mappings
                    </p>
                )}
            </section>
        </div>
    );
}