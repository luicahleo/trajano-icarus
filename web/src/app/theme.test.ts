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
