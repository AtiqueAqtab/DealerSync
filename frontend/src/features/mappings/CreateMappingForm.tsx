import {useState} from "react";
import {Plus} from "lucide-react";

import {createSkuMapping} from "../../api/skuMappings";

interface CreateMappingFormProps {
    onCreated: () => Promise<void>;
}

export default function CreateMappingForm({
                                              onCreated,
                                          }: CreateMappingFormProps) {
    const [lightspeedPartNumber, setLightspeedPartNumber] = useState("");
    const [shopifySku, setShopifySku] = useState("");
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState<string | null>(null);

    const handleSubmit = async (
        event: React.SubmitEvent<HTMLFormElement>
    ) => {
        event.preventDefault();

        if (isSubmitting) return;

        const lightspeed = lightspeedPartNumber.trim();
        const shopify = shopifySku.trim();

        setError(null);
        setSuccess(null);

        if (!lightspeed || !shopify) {
            setError("Both SKU identifiers are required.");
            return;
        }

        if (lightspeed.length > 255 || shopify.length > 255) {
            setError("SKU identifiers cannot exceed 255 characters.");
            return;
        }

        setIsSubmitting(true);

        try {
            await createSkuMapping({
                lightspeedPartNumber: lightspeed,
                shopifySku: shopify,
            });

            setLightspeedPartNumber("");
            setShopifySku("");

            setSuccess(
                "Mapping created successfully. Verification is still required."
            );

            await onCreated();
        } catch (err) {
            setError(
                err instanceof Error
                    ? err.message
                    : "Unable to create SKU mapping."
            );
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <section className="rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
            <div className="mb-5">
                <h2 className="text-lg font-semibold text-slate-900 dark:text-white">
                    Create SKU Mapping
                </h2>

                <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                    Link a Lightspeed part number to a Shopify SKU.
                    New mappings require verification.
                </p>
            </div>

            <form onSubmit={handleSubmit} className="space-y-5">
                <div className="grid gap-4 md:grid-cols-2">
                    <div>
                        <label
                            htmlFor="lightspeed-part-number"
                            className="mb-2 block text-sm font-medium text-slate-700 dark:text-slate-200"
                        >
                            Lightspeed Part Number
                        </label>

                        <input
                            id="lightspeed-part-number"
                            type="text"
                            value={lightspeedPartNumber}
                            onChange={(event) =>
                                setLightspeedPartNumber(event.target.value)
                            }
                            maxLength={255}
                            disabled={isSubmitting}
                            placeholder="e.g. KLIM-456"
                            autoComplete="off"
                            className="w-full rounded-lg border border-slate-300 bg-white px-4 py-2.5 text-sm text-slate-900 outline-none focus:border-blue-500 disabled:opacity-50 dark:border-slate-700 dark:bg-slate-950 dark:text-white"
                        />
                    </div>

                    <div>
                        <label
                            htmlFor="shopify-sku"
                            className="mb-2 block text-sm font-medium text-slate-700 dark:text-slate-200"
                        >
                            Shopify SKU
                        </label>

                        <input
                            id="shopify-sku"
                            type="text"
                            value={shopifySku}
                            onChange={(event) =>
                                setShopifySku(event.target.value)
                            }
                            maxLength={255}
                            disabled={isSubmitting}
                            placeholder="e.g. KLIM456"
                            autoComplete="off"
                            className="w-full rounded-lg border border-slate-300 bg-white px-4 py-2.5 text-sm text-slate-900 outline-none focus:border-blue-500 disabled:opacity-50 dark:border-slate-700 dark:bg-slate-950 dark:text-white"
                        />
                    </div>
                </div>

                {error && (
                    <p
                        role="alert"
                        className="text-sm text-red-600 dark:text-red-400"
                    >
                        {error}
                    </p>
                )}

                {success && (
                    <p
                        role="status"
                        className="text-sm text-emerald-600 dark:text-emerald-400"
                    >
                        {success}
                    </p>
                )}

                <div className="flex justify-end">
                    <button
                        type="submit"
                        disabled={isSubmitting}
                        className="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50"
                    >
                        <Plus size={17}/>
                        {isSubmitting ? "Creating..." : "Create Mapping"}
                    </button>
                </div>
            </form>
        </section>
    );
}