// Propia de Trajano-Icarus. Congela la lista de gates: quitar, reordenar o
// debilitar uno exige cambiar esta prueba en el mismo diff, y el revisor lo ve.
// Añadir un gate también pasa por aquí.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { cargarGates } from '../lib/gates.mjs';

const raiz = fileURLToPath(new URL('../..', import.meta.url));

test('la configuración del proyecto declara los diez gates vigentes en orden y con sus argumentos', () => {
  const { gates, problemas } = cargarGates(raiz);
  assert.deepEqual(problemas, []);
  assert.deepEqual(
    gates.map(({ motivo, ...gate }) => gate),
    [
      { nombre: 'Tests de la puerta', tipo: 'node', args: ['--test', 'quality/__tests__/*.test.mjs'] },
      { nombre: 'Adaptadores', tipo: 'node', args: ['quality/check-adaptadores.mjs'] },
      { nombre: 'Mojibake', tipo: 'node', args: ['quality/check-mojibake.mjs'] },
      { nombre: 'Enlaces', tipo: 'node', args: ['quality/check-enlaces.mjs'] },
      { nombre: 'Tareas', tipo: 'node', args: ['quality/check-tareas.mjs'] },
      { nombre: 'Frontend lint', tipo: 'npm', script: 'lint', cwd: 'web' },
      { nombre: 'Frontend build', tipo: 'npm', script: 'build', cwd: 'web' },
      { nombre: 'Frontend tests', tipo: 'npm', script: 'test', cwd: 'web' },
      { nombre: 'Backend build', tipo: 'dotnet', args: ['build', 'Icarus/Icarus.sln', '--nologo'] },
      { nombre: 'Backend tests', tipo: 'dotnet', args: ['test', 'Icarus/Icarus.sln', '--nologo', '--no-build'] },
    ],
  );
});
