/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';

const apiTarget = process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:8080';
const allowedHost = process.env.VITE_ALLOWED_HOST;

export function crearHostsPermitidos(host: string | undefined): string[] {
  return host ? [host] : [];
}

export default defineConfig(({ mode }) => ({
  // Sello del build: permite saber qué bundle está ejecutando realmente el
  // navegador (diagnóstico de service workers obsoletos) sin tocar el código.
  define: { __APP_BUILD__: JSON.stringify(new Date().toISOString()) },
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
          {
            src: 'pwa/pwa-maskable-192x192.png',
            sizes: '192x192',
            type: 'image/png',
            purpose: 'maskable',
          },
          {
            src: 'pwa/pwa-maskable-512x512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'maskable',
          },
        ],
      },
      workbox: { globPatterns: ['**/*.{js,css,html,svg,png,woff2}'] },
    }),
  ],
  build: {
    // Los source maps solo se generan con --mode sourcemaps, ocultos (sin la
    // anotación //# sourceMappingURL) y se extraen de dist por
    // scripts/extraer-sourcemaps.mjs antes de publicar (spec: artefacto privado).
    sourcemap: mode === 'sourcemaps' ? 'hidden' : false,
  },
  server: {
    host: true,
    allowedHosts: crearHostsPermitidos(allowedHost),
    proxy: {
      // La API vive bajo /api (paridad con el despliegue productivo): el proxy
      // reenvía la base al backend sin reescribir la ruta.
      '/api': {
        target: apiTarget,
        changeOrigin: true,
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    maxWorkers: 2,
    // Los tests de node (scripts/*.test.mjs) corren con `node --test`, no aquí.
    include: ['src/**/*.{test,spec}.?(c|m)[jt]s?(x)'],
  },
}));
