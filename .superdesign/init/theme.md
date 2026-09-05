# Tema

## Resumen compacto de tokens

- Material UI 9 con variables CSS y esquemas claro/oscuro.
- Cuerpo: Open Sans; títulos: Prompt.
- Primario claro: #007C83; oscuro: #005A61; tenue: #D9F3F4.
- Secundario: #D75A2D; oscuro: #AC3F1B.
- Fondo claro: #F4F8F8; papel: #FFFFFF; texto: #12262A.
- Tema oscuro: fondo #0E1A1C; papel #152528; texto #E4F0F0; aqua #4FC7CE.
- Bordes: #D5E2E3; tabla #B9D0D2; cabecera #EAF5F5.
- Radios: controles 12 px, tarjetas/tablas 16 px, diálogos 20 px.
- Breakpoints MUI: xs 0, sm 600, md 900, lg 1200, xl 1536.
- No hay globals.css, Tailwind ni módulos CSS; el estilo adicional usa `sx`.

## Fuente real completa

```ts
import { createTheme } from '@mui/material/styles';

declare module '@mui/material/styles' {
  interface Palette {
    marca: { fondo: string; texto: string };
    tabla: { borde: string; cabecera: string };
  }
  interface PaletteOptions {
    marca?: { fondo: string; texto: string };
    tabla?: { borde: string; cabecera: string };
  }
}

const claro = {
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
  cabeceraTabla: '#EAF5F5',
  marca: '#005A61',
  blanco: '#FFFFFF',
} as const;

const oscuro = {
  aqua: '#4FC7CE',
  aquaOscuro: '#2A9AA1',
  aquaClaro: '#8AE0E4',
  terracota: '#E8815A',
  terracotaOscura: '#C2603C',
  fondo: '#0E1A1C',
  papel: '#152528',
  texto: '#E4F0F0',
  neutro: '#A3B8BA',
  borde: '#2A4145',
  bordeTabla: '#2F4A4E',
  cabeceraTabla: '#1B3033',
  marca: '#0A2226',
  contraste: '#04262A',
} as const;

const variables = {
  bordeTabla: 'var(--mui-palette-tabla-borde)',
  cabeceraTabla: 'var(--mui-palette-tabla-cabecera)',
  papel: 'var(--mui-palette-background-paper)',
  primario: 'var(--mui-palette-primary-main)',
  primarioTenue: 'rgba(var(--mui-palette-primary-mainChannel) / 0.5)',
  primarioMuyTenue: 'rgba(var(--mui-palette-primary-mainChannel) / 0.08)',
  filaResaltada: 'rgba(var(--mui-palette-primary-mainChannel) / 0.06)',
} as const;

export const theme = createTheme({
  cssVariables: { colorSchemeSelector: 'class' },
  colorSchemes: {
    light: {
      palette: {
        primary: {
          main: claro.aqua,
          dark: claro.aquaOscuro,
          light: claro.aquaClaro,
          contrastText: claro.blanco,
        },
        secondary: {
          main: claro.terracota,
          dark: claro.terracotaOscura,
          contrastText: claro.blanco,
        },
        background: { default: claro.bruma, paper: claro.papel },
        text: { primary: claro.grafito, secondary: claro.neutro },
        divider: claro.borde,
        marca: { fondo: claro.marca, texto: claro.blanco },
        tabla: { borde: claro.bordeTabla, cabecera: claro.cabeceraTabla },
      },
    },
    dark: {
      palette: {
        primary: {
          main: oscuro.aqua,
          dark: oscuro.aquaOscuro,
          light: oscuro.aquaClaro,
          contrastText: oscuro.contraste,
        },
        secondary: {
          main: oscuro.terracota,
          dark: oscuro.terracotaOscura,
          contrastText: oscuro.contraste,
        },
        background: { default: oscuro.fondo, paper: oscuro.papel },
        text: { primary: oscuro.texto, secondary: oscuro.neutro },
        divider: oscuro.borde,
        marca: { fondo: oscuro.marca, texto: oscuro.texto },
        tabla: { borde: oscuro.bordeTabla, cabecera: oscuro.cabeceraTabla },
      },
    },
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
          backgroundColor: variables.papel,
          '&.Mui-focused .MuiOutlinedInput-notchedOutline': { borderColor: variables.primario },
          '&:hover:not(.Mui-disabled):not(.Mui-focused) .MuiOutlinedInput-notchedOutline': {
            borderColor: variables.primarioTenue,
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
          border: `1px solid ${variables.bordeTabla}`,
          borderRadius: '16px',
        },
      },
    },
    MuiTableCell: {
      styleOverrides: {
        root: { borderBottom: `1px solid ${variables.bordeTabla}`, padding: '14px 16px' },
        head: {
          fontWeight: 700,
          textTransform: 'uppercase',
          fontSize: '0.75rem',
          letterSpacing: '0.08em',
          borderBottom: `2px solid ${variables.bordeTabla}`,
          backgroundColor: variables.cabeceraTabla,
        },
      },
    },
    MuiTableBody: {
      styleOverrides: {
        root: {
          '& .MuiTableRow-root:hover': {
            backgroundColor: variables.filaResaltada,
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
          '&:hover': { backgroundColor: variables.primarioMuyTenue },
        },
      },
    },
  },
});

```
