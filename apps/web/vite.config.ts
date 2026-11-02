import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

/** Deployment chooses the entry point; secrets and user data never select this mode. */
export function accountEntryHtml(html: string, mode = process.env.QUICKPROMPT_WEB_MODE): string {
  if (!mode || mode === 'local') return html;
  if (mode !== 'accounts') throw new Error('QUICKPROMPT_WEB_MODE must be local or accounts.');
  const entries = (html.match(/<meta\b[^>]*>/gi) ?? []).filter(tag => /\bname\s*=\s*["']quickprompt-mode["']/i.test(tag));
  if (entries.length) {
    if (entries.length === 1 && /\bcontent\s*=\s*["']accounts["']/i.test(entries[0])) return html;
    throw new Error('The account entry point has conflicting or duplicate mode metadata.');
  }
  if (!/<head\b[^>]*>/i.test(html)) throw new Error('The account entry point requires a head element.');
  return html.replace(/<head\b[^>]*>/i, head => `${head}<meta name="quickprompt-mode" content="accounts" />`);
}

export default defineConfig({
  plugins: [react(), tailwindcss(), { name: 'quickprompt-account-entry', transformIndexHtml: html => accountEntryHtml(html) }],
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:5190',
        changeOrigin: true,
        // Explicit development proxy; API still checks its own exact origin.
        configure(proxy) {
          proxy.on('proxyReq', (request, incoming) => {
            if (incoming.headers.origin === 'http://127.0.0.1:5173') request.setHeader('Origin', 'http://127.0.0.1:5190');
          });
        },
      },
    },
  },
  test: { environment: 'jsdom', include: ['tests/**/*.test.ts', 'tests/**/*.test.tsx'], restoreMocks: true },
});
