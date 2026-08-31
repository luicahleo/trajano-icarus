/// <reference types="vite/client" />
/// <reference types="vite-plugin-pwa/client" />

interface ImportMetaEnv {
  readonly VITE_HABILITAR_DIAGNOSTICO_MANUAL?: string;
  readonly VITE_RELEASE?: string;
}

// Sello del build inyectado por vite.config.ts (define).
declare const __APP_BUILD__: string;
