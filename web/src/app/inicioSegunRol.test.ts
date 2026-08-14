import { inicioSegunRol } from './inicioSegunRol';

describe('inicioSegunRol', () => {
  test('Administrador va a clientes', () => {
    expect(inicioSegunRol('Administrador')).toBe('/admin/clientes');
  });

  test('Cliente va a sus trabajadores', () => {
    expect(inicioSegunRol('Cliente')).toBe('/trabajadores');
  });

  test('Trabajador cae en el placeholder', () => {
    expect(inicioSegunRol('Trabajador')).toBe('/inicio');
  });
});
