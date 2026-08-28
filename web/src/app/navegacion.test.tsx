import { describe, expect, test } from 'vitest';
import { obtenerEnlacesNavegacion, obtenerTituloRuta } from './navegacion';

describe('obtenerEnlacesNavegacion', () => {
  test('incluye los módulos administrativos', () => {
    expect(
      obtenerEnlacesNavegacion('Administrador', false).map(({ etiqueta }) => etiqueta),
    ).toEqual(['Clientes', 'Vacunación']);
  });

  test('incluye gestión avícola para el cliente', () => {
    expect(obtenerEnlacesNavegacion('Cliente', false).map(({ etiqueta }) => etiqueta)).toEqual([
      'Trabajadores',
      'Gestión Avícola',
    ]);
  });

  test('limita al trabajador según sus funcionalidades', () => {
    expect(obtenerEnlacesNavegacion('Trabajador', false)).toEqual([]);
    expect(obtenerEnlacesNavegacion('Trabajador', true).map(({ etiqueta }) => etiqueta)).toEqual([
      'Gestión Avícola',
    ]);
  });
});

describe('obtenerTituloRuta', () => {
  const enlaces = obtenerEnlacesNavegacion('Administrador', false);

  test('resuelve la sección para rutas hijas', () => {
    expect(obtenerTituloRuta('/admin/clientes/nuevo', enlaces)).toBe('Clientes');
  });

  test('usa Inicio cuando la ruta no pertenece a un módulo', () => {
    expect(obtenerTituloRuta('/inicio', enlaces)).toBe('Inicio');
  });
});
