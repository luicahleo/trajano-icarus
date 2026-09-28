import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { CssBaseline, ThemeProvider } from '@mui/material';
import '@fontsource/open-sans/latin-400.css';
import '@fontsource/open-sans/latin-600.css';
import '@fontsource/prompt/latin-600.css';
import '@fontsource/prompt/latin-700.css';
import { theme } from '../app/theme';
import { AppKiosco } from './AppKiosco';

// Entrada aislada: sin AuthProvider, sin token administrativo y sin registrar
// el service worker de la PWA. Solo la cookie restringida del kiosco.
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider theme={theme} defaultMode="light">
      <CssBaseline />
      <AppKiosco />
    </ThemeProvider>
  </StrictMode>,
);
