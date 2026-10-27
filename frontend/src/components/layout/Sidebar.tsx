import {NavLink} from "react-router";
import {
    LayoutDashboard,
    RefreshCw,
    Package,
    Link2,
    History,
    Settings,
    Boxes,
    PanelLeftClose,
    PanelLeftOpen,
} from "lucide-react";

interface SidebarProps {
    collapsed: boolean;
    onToggle: () => void;
}

const navigation = [
    {
        name: "Dashboard",
        path: "/",
        icon: LayoutDashboard,
    },
    {
        name: "Reconciliation",
        path: "/reconcile",
        icon: RefreshCw,
    },
    {
        name: "Inventory",
        path: "/inventory",
        icon: Package,
    },
    {
        name: "SKU Mappings",
        path: "/mappings",
        icon: Link2,
    },
    {
        name: "History",
        path: "/history",
        icon: History,
    },
];

export default function Sidebar({
                                    collapsed,
                                    onToggle,
                                }: SidebarProps) {
    const getNavClasses = (isActive: boolean) =>
        `flex h-11 items-center rounded-lg text-sm font-medium transition-colors ${
            collapsed ? "justify-center px-2" : "gap-3 px-4"
        } ${
            isActive
                ? "bg-blue-600 text-white shadow-sm"
                : "text-slate-600 hover:bg-slate-200 hover:text-slate-900 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-white"
        }`;

    return (
        <aside
            className={`sticky top-0 flex h-screen shrink-0 flex-col border-r border-slate-200 bg-slate-50 transition-[width] duration-200 dark:border-slate-800 dark:bg-slate-900 ${
                collapsed ? "w-20" : "w-64"
            }`}
        >
            {/* Logo and toggle */}
            <div
                className={`flex h-20 items-center ${
                    collapsed
                        ? "justify-center"
                        : "justify-between px-4"
                }`}
            >
                {!collapsed && (
                    <div className="flex min-w-0 items-center gap-3">
                        <div
                            className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-blue-600 text-white">
                            <Boxes size={22}/>
                        </div>

                        <span className="text-xl font-bold tracking-tight text-slate-900 dark:text-white">
                            DealerSync
                        </span>
                    </div>
                )}

                <button
                    type="button"
                    onClick={onToggle}
                    aria-label={
                        collapsed ? "Expand sidebar" : "Collapse sidebar"
                    }
                    aria-expanded={!collapsed}
                    title={
                        collapsed ? "Expand sidebar" : "Collapse sidebar"
                    }
                    className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg text-slate-500 transition-colors hover:bg-slate-200 hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-white"
                >
                    {collapsed ? (
                        <PanelLeftOpen size={20}/>
                    ) : (
                        <PanelLeftClose size={20}/>
                    )}
                </button>
            </div>

            {/* Navigation */}
            <nav
                aria-label="Main navigation"
                className="flex-1 space-y-1 px-3"
            >
                {navigation.map((item) => {
                    const Icon = item.icon;

                    return (
                        <NavLink
                            key={item.path}
                            to={item.path}
                            end={item.path === "/"}
                            title={collapsed ? item.name : undefined}
                            aria-label={item.name}
                            className={({isActive}) =>
                                getNavClasses(isActive)
                            }
                        >
                            <Icon size={19} className="shrink-0"/>

                            {!collapsed && (
                                <span>{item.name}</span>
                            )}
                        </NavLink>
                    );
                })}
            </nav>

            {/* Settings */}
            <div className="border-t border-slate-200 p-3 dark:border-slate-800">
                <NavLink
                    to="/settings"
                    title={collapsed ? "Settings" : undefined}
                    aria-label="Settings"
                    className={({isActive}) =>
                        getNavClasses(isActive)
                    }
                >
                    <Settings size={19} className="shrink-0"/>

                    {!collapsed && (
                        <span>Settings</span>
                    )}
                </NavLink>
            </div>
        </aside>
    );
}