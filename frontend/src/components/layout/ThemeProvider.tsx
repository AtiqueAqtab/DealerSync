import {
    useEffect,
    useState,
    type ReactNode,
} from "react";

import {
    ThemeContext,
    type Theme,
} from "./ThemeContext";

const STORAGE_KEY = "dealersync-theme";

function getInitialTheme(): Theme {
    try {
        return localStorage.getItem(STORAGE_KEY) === "dark"
            ? "dark"
            : "light";
    } catch {
        return "light";
    }
}

export function ThemeProvider({
                                  children,
                              }: {
    children: ReactNode;
}) {
    const [theme, setTheme] = useState<Theme>(getInitialTheme);

    useEffect(() => {
        document.documentElement.classList.toggle(
            "dark",
            theme === "dark"
        );

        document.documentElement.style.colorScheme = theme;

        try {
            localStorage.setItem(STORAGE_KEY, theme);
        } catch {
            // Continue without persistence if storage is unavailable.
        }
    }, [theme]);

    const toggleTheme = () => {
        setTheme((current) =>
            current === "light" ? "dark" : "light"
        );
    };

    return (
        <ThemeContext.Provider value={{theme, toggleTheme}}>
            {children}
        </ThemeContext.Provider>
    );
}