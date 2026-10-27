import {useState} from "react";
import {Outlet} from "react-router";

import Sidebar from "./Sidebar";
import Header from "./Header";

export default function AppLayout() {
    const [sidebarCollapsed, setSidebarCollapsed] = useState(false);

    return (
        <div className="flex min-h-screen bg-slate-50 dark:bg-slate-950">
            <Sidebar
                collapsed={sidebarCollapsed}
                onToggle={() =>
                    setSidebarCollapsed((current) => !current)
                }
            />

            <div className="flex min-w-0 flex-1 flex-col">
                <Header/>

                <main className="min-w-0 flex-1 p-8">
                    <Outlet/>
                </main>
            </div>
        </div>
    );
}