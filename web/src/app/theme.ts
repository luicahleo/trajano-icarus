import { alpha, createTheme } from '@mui/material/styles';

const colores = {
  aqua: '#007C83',
  aquaOscuro: '#005A61',
  aquaClaro: '#D9F3F4',
  terracota: '#D75A2D',
  terracotaOscura: '#AC3F1B',
  bruma: '#F4F8F8',
  papel: '#FFFFFF',
  grafito: '#12262A',
  neutro: '#54666A',
  borde: '#D5E2E3',
  bordeTabla: '#B9D0D2',
  blanco: '#FFFFFF',
} as const;

export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: colores.aqua,
      dark: colores.aquaOscuro,
      light: colores.aquaClaro,
      contrastText: colores.blanco,
    },
    secondary: {
      main: colores.terracota,
      dark: colores.terracotaOscura,
      contrastText: colores.blanco,
    },
    background: { default: colores.bruma, paper: colores.papel },
    text: { primary: colores.grafito, secondary: colores.neutro },
    divider: colores.borde,
  },
  typography: {
    fontFamily: '"Open Sans", Arial, sans-serif',
    h1: {
      fontFamily: '"Prompt", "Open Sans", sans-serif',
      fontWeight: 700,
      letterSpacing: '-0.03em',
    },
    h2: {
      fontFamily: '"Prompt", "Open Sans", sans-serif',
      fontWeight: 700,
      letterSpacing: '-0.025em',
    },
    h3: {
      fontFamily: '"Prompt", "Open Sans", sans-serif',
      fontWeight: 700,
      letterSpacing: '-0.02em',
    },
    h4: {
      fontFamily: '"Prompt", "Open Sans", sans-serif',
      fontWeight: 600,
      letterSpacing: '-0.02em',
    },
    h5: {
      fontFamily: '"Prompt", "Open Sans", sans-serif',
      fontWeight: 600,
      letterSpacing: '-0.015em',
    },
    h6: {
      fontFamily: '"Prompt", "Open Sans", sans-serif',
      fontWeight: 600,
      letterSpacing: '-0.01em',
    },
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
      styleOverrides: {
        root: {
          borderRadius: '12px',
          backgroundColor: colores.papel,
          '&.Mui-focused .MuiOutlinedInput-notchedOutline': { borderColor: colores.aqua },
          '&:hover:not(.Mui-disabled):not(.Mui-focused) .MuiOutlinedInput-notchedOutline': {
            borderColor: alpha(colores.aqua, 0.5),
          },
        },
      },
    },
    MuiCard: { styleOverrides: { root: { borderRadius: '16px', backgroundImage: 'none' } } },
    MuiPaper: { styleOverrides: { root: { backgroundImage: 'none' } } },
    MuiTable: {
      styleOverrides: { root: { borderCollapse: 'separate', borderSpacing: 0 } },
    },
    MuiTableContainer: {
      styleOverrides: {
        root: {
          border: `1px solid ${colores.bordeTabla}`,
          borderRadius: '16px',
        },
      },
    },
    MuiTableCell: {
      styleOverrides: {
        root: { borderBottom: `1px solid ${colores.bordeTabla}`, padding: '14px 16px' },
        head: {
          fontWeight: 700,
          textTransform: 'uppercase',
          fontSize: '0.75rem',
          letterSpacing: '0.08em',
          color: 'text.secondary',
          borderBottom: `2px solid ${colores.bordeTabla}`,
          backgroundColor: alpha(colores.aquaClaro, 0.3),
        },
      },
    },
    MuiTableBody: {
      styleOverrides: {
        root: {
          '& .MuiTableRow-root:hover': {
            backgroundColor: alpha(colores.aquaClaro, 0.4),
            transition: 'background-color 150ms ease',
          },
        },
      },
    },
    MuiDialog: {
      styleOverrides: { paper: { borderRadius: '20px' } },
    },
    MuiDialogTitle: {
      styleOverrides: {
        root: {
          fontFamily: '"Prompt", "Open Sans", sans-serif',
          fontWeight: 600,
          fontSize: '1.125rem',
          letterSpacing: '-0.01em',
          paddingBottom: '4px',
        },
      },
    },
    MuiDialogActions: {
      styleOverrides: { root: { padding: '8px 24px 20px', gap: 8 } },
    },
    MuiChip: {
      styleOverrides: { root: { borderRadius: '8px', fontWeight: 600 } },
    },
    MuiAlert: {
      styleOverrides: { root: { borderRadius: '12px' } },
    },
    MuiListItem: {
      styleOverrides: {
        root: {
          borderRadius: '12px',
          transition: 'background-color 150ms ease',
          '&:hover': { backgroundColor: alpha(colores.aquaClaro, 0.35) },
        },
      },
    },
  },
});
