import { theme } from './theme';

const componentes = theme.components as {
  MuiTableContainer?: { styleOverrides?: { root?: { border?: string } } };
  MuiTableCell?: {
    styleOverrides?: {
      root?: { borderBottom?: string };
      head?: { borderBottom?: string };
    };
  };
};

const esquemas = (
  theme as unknown as {
    colorSchemes: {
      light: { palette: Record<string, Record<string, string>> };
      dark: { palette: Record<string, Record<string, string>> };
    };
  }
).colorSchemes;

test('las tablas tienen bordes visibles en el contenedor', () => {
  const borde = componentes.MuiTableContainer?.styleOverrides?.root?.border;
  expect(borde).toMatch(/1px solid/);
});

test('las celdas de tabla tienen separación visible entre filas', () => {
  const borde = componentes.MuiTableCell?.styleOverrides?.root?.borderBottom;
  expect(borde).toMatch(/1px solid/);
});

test('el encabezado de tabla se separa con un borde más marcado', () => {
  const borde = componentes.MuiTableCell?.styleOverrides?.head?.borderBottom;
  expect(borde).toMatch(/2px solid/);
});

test('la identidad principal usa azul aqua accesible', () => {
  expect(esquemas.light.palette.primary.main).toBe('#007C83');
  expect(esquemas.light.palette.primary.dark).toBe('#005A61');
  expect(esquemas.light.palette.primary.light).toBe('#D9F3F4');
  expect(esquemas.light.palette.primary.contrastText).toBe('#FFFFFF');
});

test('las superficies y divisores acompañan la identidad aqua', () => {
  expect(esquemas.light.palette.background.default).toBe('#F4F8F8');
  expect(esquemas.light.palette.background.paper).toBe('#FFFFFF');
  expect(esquemas.light.palette.divider).toBe('#D5E2E3' as unknown as Record<string, string>);
});

test('el esquema oscuro invierte las superficies y conserva el aqua', () => {
  expect(esquemas.dark.palette.background.default).toBe('#0E1A1C');
  expect(esquemas.dark.palette.background.paper).toBe('#152528');
  expect(esquemas.dark.palette.primary.main).toBe('#4FC7CE');
  expect(esquemas.dark.palette.text.primary).toBe('#E4F0F0');
});

test('la banda de marca y las tablas tienen tokens propios en ambos esquemas', () => {
  expect(esquemas.light.palette.marca.fondo).toBe('#005A61');
  expect(esquemas.dark.palette.marca.fondo).toBe('#0A2226');
  expect(esquemas.light.palette.tabla.borde).toBe('#B9D0D2');
  expect(esquemas.dark.palette.tabla.borde).toBe('#2F4A4E');
});

test('los componentes se pintan con variables CSS, no con colores fijos', () => {
  expect(componentes.MuiTableContainer?.styleOverrides?.root?.border).toContain(
    'var(--mui-palette-tabla-borde)',
  );
});
