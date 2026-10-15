import {BrowserRouter, Route, Routes} from "react-router";
import AppLayout from "./components/layout/AppLayout";
import {ThemeProvider} from "./components/layout/ThemeProvider";
import DashboardPage from "./features/dashboard/DashboardPage";
import ReconciliationPage from "./features/reconciliation/ReconciliationPage";
import ReconciliationProvider from "./features/reconciliation/ReconciliationProvider";
import InventoryPage from "./features/inventory/InventoryPage";
import MappingsPage from "./features/mappings/MappingsPage";

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
            <ReconciliationProvider>

                <BrowserRouter>
                    <Routes>
                        <Route element={<AppLayout/>}>
                            <Route index element={<DashboardPage/>}/>

                            <Route
                                path="reconcile"
                                element={<ReconciliationPage/>}
                            />

                            <Route
                                path="inventory"
                                element={<InventoryPage/>}
                            />

                            <Route
                                path="mappings"
                                element={<MappingsPage/>}
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
            </ReconciliationProvider>
        </ThemeProvider>
    );
}