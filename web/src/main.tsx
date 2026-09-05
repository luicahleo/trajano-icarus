import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { CssBaseline, ThemeProvider } from '@mui/material';
import '@fontsource/open-sans/latin-400.css';
import '@fontsource/open-sans/latin-600.css';
import '@fontsource/open-sans/latin-700.css';
import '@fontsource/prompt/latin-600.css';
import '@fontsource/prompt/latin-700.css';
import { theme } from './app/theme';
import { instalarServiceWorker } from './pwa/registro';
import { registrarEventoFlujo } from './lib/sesionDiagnostico';
import App from './App';
import { CapturaErroresGlobales } from './app/CapturaErroresGlobales';

// Primer evento del buffer: prueba qué build está sirviendo el service worker.
registrarEventoFlujo({ eventName: 'flow.app', detail: `Arranque build ${__APP_BUILD__}` });
instalarServiceWorker();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider theme={theme} defaultMode="system">
      <CssBaseline />
      <CapturaErroresGlobales />
      <App />
    </ThemeProvider>
  </StrictMode>,
);
