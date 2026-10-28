import { useEffect, useState } from 'react';

function initialTheme() {
  try { return localStorage.getItem('nova-theme') === 'light' ? 'light' : 'dark'; }
  catch { return 'dark'; }
}

/** Device-only appearance preference shared by local and account workspaces. */
export function useInterfaceTheme() {
  const [theme, setTheme] = useState(initialTheme);
  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    document.querySelector('meta[name="theme-color"]')?.setAttribute('content', getComputedStyle(document.documentElement).getPropertyValue('--background').trim());
    try { localStorage.setItem('nova-theme', theme); }
    catch { /* Theme remains usable without storage. */ }
  }, [theme]);
  return [theme, setTheme] as const;
}
