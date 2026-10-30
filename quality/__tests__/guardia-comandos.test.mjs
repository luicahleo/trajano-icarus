import { test } from 'node:test';
import assert from 'node:assert/strict';
import { evaluarComando, evaluarEvento } from '../guardia-comandos.mjs';

const conTarea = { tareaActiva: true };
const sinTarea = { tareaActiva: false };

test('--no-verify se bloquea siempre', () => {
  assert.equal(evaluarComando('git commit --no-verify -m x', sinTarea).permitido, false);
  assert.equal(evaluarComando('git push --no-verify', conTarea).permitido, false);
});

test('fuera de una tarea no interfiere con el despliegue autorizado', () => {
  assert.equal(evaluarComando('./deploy-produccion.ps1 -Confirmar', sinTarea).permitido, true);
  assert.equal(evaluarComando('git push origin master', sinTarea).permitido, true);
});

for (const comando of [
  'git push origin master',
  'git push origin HEAD:master',
  'git push origin develop:master',
  'git push --force origin develop',
  'git push -f',
  'git push --force-with-lease',
  './deploy-produccion.ps1 -Confirmar',
  '.\\deploy-produccion.ps1 -Confirmar -Watch',
  'gh workflow run deploy.yml -f confirmar=PRODUCCION',
  'git reset --hard HEAD~1',
  'git clean -fd',
  './iniciar-pc1.ps1 -RecrearDatos -ConfirmarBorradoDatos',
  'docker compose down --volumes',
  'docker compose -f a.yml down -v',
  'docker volume rm icarus_datos',
  'docker volume prune -f',
  'docker system prune -a',
  'cat .env',
  'Get-Content ./.env',
  'node quality/tarea.mjs aprobar 2026-10-30-x',
]) {
  test(`con tarea activa bloquea: ${comando}`, () => {
    assert.equal(evaluarComando(comando, conTarea).permitido, false);
  });
}

for (const comando of [
  'git push origin develop',
  'git push',
  'git status --short',
  'git log --oneline master..develop',
  './verify.ps1',
  './iniciar-pc1.ps1',
  'docker compose ps',
  'docker compose down',
  'cat .env.example',
  'node quality/tarea.mjs resumen 2026-10-30-x',
  'grep -n "env" docs/proyecto/operacion-calidad.md',
]) {
  test(`con tarea activa permite: ${comando}`, () => {
    assert.equal(evaluarComando(comando, conTarea).permitido, true);
  });
}

test('el rechazo explica el motivo', () => {
  assert.match(evaluarComando('git push origin master', conTarea).motivo, /master/);
});

test('evaluarEvento mira el comando de Bash y de PowerShell', () => {
  const bash = { tool_name: 'Bash', tool_input: { command: 'git push origin master' } };
  const ps = { tool_name: 'PowerShell', tool_input: { command: 'git push origin master' } };
  assert.equal(evaluarEvento(bash, conTarea).permitido, false);
  assert.equal(evaluarEvento(ps, conTarea).permitido, false);
});

test('evaluarEvento bloquea leer .env con la ruta de Claude y la de Kimi', () => {
  const claude = { tool_name: 'Read', tool_input: { file_path: 'C:\\repo\\.env' } };
  const kimi = { tool_name: 'Read', tool_input: { path: 'C:/repo/.env' } };
  assert.equal(evaluarEvento(claude, conTarea).permitido, false);
  assert.equal(evaluarEvento(kimi, conTarea).permitido, false);
});

test('evaluarEvento permite leer .env.example y otras herramientas', () => {
  assert.equal(evaluarEvento({ tool_name: 'Read', tool_input: { path: '.env.example' } }, conTarea).permitido, true);
  assert.equal(evaluarEvento({ tool_name: 'Grep', tool_input: { pattern: 'x' } }, conTarea).permitido, true);
});
