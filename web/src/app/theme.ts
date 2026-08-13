import { createTheme } from '@mui/material/styles';

const colores = {
  pino: '#1B5E20',
  pinoOscuro: '#124316',
  pinoClaro: '#DCE8DC',
  terracota: '#D75A2D',
  terracotaOscura: '#AC3F1B',
  crema: '#F8F6F1',
  papel: '#FFFEFC',
  grafito: '#1D2924',
  salvia: '#5E6B64',
  borde: '#DEDCD5',
  blanco: '#FFFFFF',
} as const;

export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: colores.pino,
      dark: colores.pinoOscuro,
      light: colores.pinoClaro,
      contrastText: colores.blanco,
    },
    secondary: {
      main: colores.terracota,
      dark: colores.terracotaOscura,
      contrastText: colores.blanco,
    },
    background: { default: colores.crema, paper: colores.papel },
    text: { primary: colores.grafito, secondary: colores.salvia },
    divider: colores.borde,
  },
  typography: {
    fontFamily: '"Open Sans", Arial, sans-serif',
    h1: { fontFamily: '"Prompt", "Open Sans", sans-serif', fontWeight: 700, letterSpacing: '-0.03em' },
    h2: { fontFamily: '"Prompt", "Open Sans", sans-serif', fontWeight: 700, letterSpacing: '-0.025em' },
    h3: { fontFamily: '"Prompt", "Open Sans", sans-serif', fontWeight: 700, letterSpacing: '-0.02em' },
    h4: { fontFamily: '"Prompt", "Open Sans", sans-serif', fontWeight: 600, letterSpacing: '-0.02em' },
    h5: { fontFamily: '"Prompt", "Open Sans", sans-serif', fontWeight: 600, letterSpacing: '-0.015em' },
    h6: { fontFamily: '"Prompt", "Open Sans", sans-serif', fontWeight: 600, letterSpacing: '-0.01em' },
    button: { fontWeight: 700, textTransform: 'none' },
  },
  shape: { borderRadius: 12 },
  components: {
    MuiButton: {
      defaultProps: { disableElevation: true },
      styleOverrides: {
        root: {
          borderRadius: '12px',
          minHeight: '40px',
          '&:active': { transform: 'translateY(1px)' },
          '@media (prefers-reduced-motion: reduce)': { transition: 'none' },
        },
      },
    },
    MuiOutlinedInput: {
      styleOverrides: { root: { borderRadius: '12px', backgroundColor: colores.papel } },
    },
    MuiCard: { styleOverrides: { root: { borderRadius: '16px', backgroundImage: 'none' } } },
    MuiPaper: { styleOverrides: { root: { backgroundImage: 'none' } } },
  },
});
