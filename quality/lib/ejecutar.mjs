// Ejecuta un comando externo y devuelve su código, salida combinada y duración.
// Nunca lanza: el llamador decide qué hacer con un código distinto de cero.

import { spawnSync } from 'node:child_process';

export function ejecutar(comando, args, opciones = {}) {
  // En Windows, los lanzadores .cmd (npm, dotnet) necesitan shell. Un .exe
  // como git no, y evitar el shell mantiene los argumentos intactos: es lo
  // que pide `sinShell`.
  const usarShell = opciones.sinShell ? false : process.platform === 'win32';

  const inicio = process.hrtime.bigint();
  const resultado = spawnSync(comando, args, {
    cwd: opciones.cwd ?? process.cwd(),
    encoding: 'utf8',
    shell: usarShell,
    maxBuffer: 32 * 1024 * 1024,
  });
  const duracionMs = Number((process.hrtime.bigint() - inicio) / 1_000_000n);
  const salida = `${resultado.stdout ?? ''}${resultado.stderr ?? ''}`;

  if (!opciones.silencioso) {
    process.stdout.write(salida);
  }

  // status es null cuando el proceso no llegó a arrancar o murió por señal.
  return { codigo: resultado.status ?? 1, salida, duracionMs };
}
