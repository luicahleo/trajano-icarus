// Formato uniforme para la salida de los gates de calidad.
// No imprime: devuelve cadenas, para que los tests puedan verificarlas.

const VERDE = '\x1b[32m';
const ROJO = '\x1b[31m';
const AMARILLO = '\x1b[33m';
const NEGRITA = '\x1b[1m';
const RESET = '\x1b[0m';

export function titulo(texto) {
  return `${NEGRITA}== ${texto} ==${RESET}`;
}

export function exito(texto) {
  return `${VERDE}[OK]${RESET} ${texto}`;
}

export function fallo(texto, detalle) {
  const cabecera = `${ROJO}[FALLO]${RESET} ${texto}`;
  return detalle ? `${cabecera}\n       ${detalle}` : cabecera;
}

export function aviso(texto) {
  return `${AMARILLO}[AVISO]${RESET} ${texto}`;
}
