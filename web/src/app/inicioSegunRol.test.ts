import { inicioSegunRol } from './inicioSegunRol';

describe('inicioSegunRol', () => {
  test('Administrador va a clientes', () => {
    expect(inicioSegunRol('Administrador')).toBe('/admin/clientes');
  });

  test('Cliente va a gestión avícola', () => {
    expect(inicioSegunRol('Cliente')).toBe('/avicola');
  });

  test('Trabajador con producción va a gestión avícola', () => {
    expect(inicioSegunRol('Trabajador', ['ProduccionHuevos'])).toBe('/avicola');
  });

  test('Trabajador sin funcionalidades va al inicio terminal', () => {
    expect(inicioSegunRol('Trabajador', [])).toBe('/inicio');
  });

  test('Trabajador con vacunación va a gestión avícola', () => {
    expect(inicioSegunRol('Trabajador', ['Vacunacion'])).toBe('/avicola');
  });
});
