import {Moon, Sun} from "lucide-react";
import {useLocation} from "react-router";
import {useTheme} from "../../hooks/useTheme";

const pageTitles: Record<string, string> = {
    "/": "Dashboard",
    "/reconcile": "Reconciliation",
    "/inventory": "Inventory Review",
    "/mappings": "SKU Mappings",
    "/history": "Reconciliation History",
    "/settings": "Settings",
};

export default function Header() {
    const {theme, toggleTheme} = useTheme();
    const location = useLocation();

    const title = pageTitles[location.pathname] ?? "DealerSync";

    return (
        <header
            className="flex h-18 items-center justify-between border-b border-slate-200 bg-white px-8 dark:border-slate-800 dark:bg-slate-900">
            <div>
                <h1 className="text-xl font-semibold tracking-tight text-slate-900 dark:text-white">
                    {title}
                </h1>

                <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-400">
                    Inventory reconciliation workspace
                </p>
            </div>

            <button
                type="button"
                onClick={toggleTheme}
                aria-label={
                    theme === "light"
                        ? "Switch to dark mode"
                        : "Switch to light mode"
                }
                title={
                    theme === "light"
                        ? "Switch to dark mode"
                        : "Switch to light mode"
                }
                className="flex h-10 w-10 items-center justify-center rounded-lg border border-slate-200 text-slate-600 transition-colors hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
            >
                {theme === "light" ? (
                    <Moon size={19}/>
                ) : (
                    <Sun size={19}/>
                )}
            </button>
        </header>
    );
}