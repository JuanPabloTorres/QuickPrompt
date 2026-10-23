import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  plugins: [react(), tailwindcss()],
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
