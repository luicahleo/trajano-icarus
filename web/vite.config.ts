/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';

const apiTarget = process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:8080';

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      strategies: 'generateSW',
      manifest: {
        name: 'Icarus',
        short_name: 'Icarus',
        description: 'Gestión de clientes y trabajadores',
        lang: 'es',
        theme_color: '#1B5E20',
        background_color: '#F8F6F1',
        display: 'standalone',
        start_url: '/',
        icons: [
          { src: 'pwa/pwa-192x192.png', sizes: '192x192', type: 'image/png' },
          { src: 'pwa/pwa-512x512.png', sizes: '512x512', type: 'image/png' },
          { src: 'pwa/pwa-maskable-192x192.png', sizes: '192x192', type: 'image/png', purpose: 'maskable' },
          { src: 'pwa/pwa-maskable-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
      },
      workbox: { globPatterns: ['**/*.{js,css,html,svg,png,woff2}'] },
    }),
  ],
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
