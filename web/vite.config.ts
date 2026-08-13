/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const apiTarget = process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:8080';

export default defineConfig({
  plugins: [react()],
  server: {
    host: true,
    proxy: {
      // El backend sirve bajo la raíz sin prefijo (spec): el frontend usa la base
      // /api y el proxy reescribe a la API real (compose la publica en :8080).
      '/api': {
        target: apiTarget,
        changeOrigin: true,
        rewrite: (p) => p.replace(/^\/api/, ''),
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    maxWorkers: 2,
  },
});
