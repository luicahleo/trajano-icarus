import { inicioSegunRol } from './inicioSegunRol';

describe('inicioSegunRol', () => {
  test('Administrador va a clientes', () => {
    expect(inicioSegunRol('Administrador')).toBe('/admin/clientes');
  });

  test('Cliente va a gestión avícola', () => {
    expect(inicioSegunRol('Cliente')).toBe('/avicola');
  });

  test('Trabajador va a gestión avícola', () => {
    expect(inicioSegunRol('Trabajador')).toBe('/avicola');
  });
});
