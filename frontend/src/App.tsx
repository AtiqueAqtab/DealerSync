import {BrowserRouter, Route, Routes} from "react-router";
import AppLayout from "./components/layout/AppLayout";
import {ThemeProvider} from "./components/layout/ThemeProvider";

function PlaceholderPage({title}: { title: string }) {
    return (
        <div className="p-8">
            <h1 className="text-3xl font-semibold tracking-tight text-slate-900 dark:text-white">
                {title}
            </h1>

            <p className="mt-2 text-slate-500 dark:text-slate-400">
                DealerSync v0.3.0
            </p>
        </div>
    );
}

export default function App() {
    return (
        <ThemeProvider>
            <BrowserRouter>
                <Routes>
                    <Route element={<AppLayout/>}>
                        <Route
                            index
                            element={<PlaceholderPage title="Dashboard"/>}
                        />

                        <Route
                            path="reconcile"
                            element={<PlaceholderPage title="Reconciliation"/>}
                        />

                        <Route
                            path="inventory"
                            element={<PlaceholderPage title="Inventory Review"/>}
                        />

                        <Route
                            path="mappings"
                            element={<PlaceholderPage title="SKU Mappings"/>}
                        />

                        <Route
                            path="history"
                            element={<PlaceholderPage title="Reconciliation History"/>}
                        />

                        <Route
                            path="settings"
                            element={<PlaceholderPage title="Settings"/>}
                        />
                    </Route>
                </Routes>
            </BrowserRouter>
        </ThemeProvider>
    );
}