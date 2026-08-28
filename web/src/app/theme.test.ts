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
  expect(theme.palette.primary.main).toBe('#007C83');
  expect(theme.palette.primary.dark).toBe('#005A61');
  expect(theme.palette.primary.light).toBe('#D9F3F4');
  expect(theme.palette.primary.contrastText).toBe('#FFFFFF');
});

test('las superficies y divisores acompañan la identidad aqua', () => {
  expect(theme.palette.background.default).toBe('#F4F8F8');
  expect(theme.palette.background.paper).toBe('#FFFFFF');
  expect(theme.palette.divider).toBe('#D5E2E3');
});
