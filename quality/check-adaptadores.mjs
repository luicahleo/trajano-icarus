#!/usr/bin/env node
// Gate de adaptadores: cada archivo generado debe coincidir exactamente con lo
// que el manifiesto declara. Es lo que impide que un adaptador editado a mano
// se convierta en una segunda fuente de instrucciones.

import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { ADAPTADORES } from './adaptadores/manifiesto.mjs';
import { exito, fallo } from './lib/salida.mjs';

export function verificarAdaptadores(adaptadores, leer) {
  const desvios = [];
  for (const { ruta, contenido } of adaptadores) {
    const actual = leer(ruta);
    if (actual === null) desvios.push({ ruta, motivo: 'falta' });
    else if (actual !== contenido) desvios.push({ ruta, motivo: 'difiere' });
  }
  return desvios;
}

if (process.argv[1] && process.argv[1].endsWith('check-adaptadores.mjs')) {
  const raiz = fileURLToPath(new URL('..', import.meta.url));
  const leer = (ruta) => {
    const destino = join(raiz, ruta);
    return existsSync(destino) ? readFileSync(destino, 'utf8') : null;
  };

  const desvios = verificarAdaptadores(ADAPTADORES, leer);

  if (desvios.length > 0) {
    console.log(fallo(`Gate de adaptadores: ${desvios.length} desvío(s)`));
    for (const d of desvios) {
      console.log(`       ${d.ruta}: ${d.motivo === 'falta' ? 'no existe' : 'no coincide con el manifiesto'}`);
    }
    console.log('       Corré: node quality/generar-adaptadores.mjs');
    console.log('       Si el cambio era deliberado, va en el manifiesto, no en el archivo generado.');
    process.exit(1);
  }

  console.log(exito(`Gate de adaptadores: ${ADAPTADORES.length} archivo(s) al día.`));
}
