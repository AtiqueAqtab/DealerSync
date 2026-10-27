interface CoverageChartProps {
    coverage: number;
}

export default function CoverageChart({
                                          coverage,
                                      }: CoverageChartProps) {
    const percentage = Math.max(0, Math.min(100, coverage));

    const radius = 54;
    const circumference = 2 * Math.PI * radius;
    const offset = circumference * (1 - percentage / 100);

    return (
        <div className="flex flex-col items-center">
            <div className="relative h-40 w-40">
                <svg
                    viewBox="0 0 140 140"
                    className="h-full w-full -rotate-90"
                    role="img"
                    aria-label={`Shopify match coverage: ${percentage.toFixed(1)}%`}
                >
                    <circle
                        cx="70"
                        cy="70"
                        r={radius}
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="12"
                        className="text-slate-100 dark:text-slate-800"
                    />

                    <circle
                        cx="70"
                        cy="70"
                        r={radius}
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="12"
                        strokeLinecap="round"
                        strokeDasharray={circumference}
                        strokeDashoffset={offset}
                        className="text-blue-600 transition-all duration-500"
                    />
                </svg>

                <div className="absolute inset-0 flex flex-col items-center justify-center">
                    <span className="text-3xl font-bold text-slate-900 dark:text-white">
                        {percentage.toFixed(1)}%
                    </span>

                    <span className="text-xs text-slate-500 dark:text-slate-400">
                        Matched
                    </span>
                </div>
            </div>

            <p className="mt-3 text-sm font-medium text-slate-700 dark:text-slate-300">
                Shopify Match Coverage
            </p>
        </div>
    );
}