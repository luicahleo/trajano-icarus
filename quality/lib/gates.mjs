// Configuración de la puerta de calidad. Portable: valida, carga y resuelve los
// gates que cada proyecto declara en quality/gates.json. Falla cerrado: ante
// cualquier problema no devuelve ningún gate, y una puerta sin gates no da verde.

import { existsSync, readFileSync } from 'node:fs';
import { dirname, isAbsolute, join, resolve } from 'node:path';

export const RUTA_CONFIGURACION = 'quality/gates.json';
export const TIPOS_GATE = ['node', 'npm', 'dotnet'];

const CLAVES_POR_TIPO = {
  node: ['nombre', 'tipo', 'args', 'cwd', 'motivo'],
  npm: ['nombre', 'tipo', 'script', 'cwd', 'motivo'],
  dotnet: ['nombre', 'tipo', 'args', 'cwd', 'motivo'],
};

// Falla cerrado: sin shell ni espacios, el script no puede encadenar comandos.
const SCRIPT_NPM = /^[\w:-]+$/;
const esTexto = (valor) => typeof valor === 'string' && valor.trim() !== '';
const esObjeto = (valor) => valor !== null && typeof valor === 'object' && !Array.isArray(valor);

// Relativa y dentro del repositorio. isAbsolute de POSIX no reconoce «C:\», así
// que la unidad se comprueba aparte.
const cwdAdmitido = (cwd) =>
  esTexto(cwd) && !isAbsolute(cwd) && !/^[a-zA-Z]:/.test(cwd) && !cwd.split(/[\\/]/).includes('..');

function validarGate(gate, indice, nombres) {
  const donde = `Gate ${indice + 1}`;
  if (!esObjeto(gate)) return [`${donde}: no es un objeto.`];

  const problemas = [];
  let etiqueta = donde;
  if (!esTexto(gate.nombre)) {
    problemas.push(`${donde}: falta el nombre.`);
  } else if (nombres.has(gate.nombre)) {
    problemas.push(`${donde}: nombre repetido: ${gate.nombre}.`);
  } else {
    nombres.add(gate.nombre);
    etiqueta = `${donde} (${gate.nombre})`;
  }

  if (!TIPOS_GATE.includes(gate.tipo)) {
    problemas.push(`${etiqueta}: tipo desconocido: ${gate.tipo}. Usa ${TIPOS_GATE.join(', ')}.`);
    return problemas;
  }
  for (const clave of Object.keys(gate).filter((k) => !CLAVES_POR_TIPO[gate.tipo].includes(k))) {
    problemas.push(`${etiqueta}: clave no admitida para ${gate.tipo}: ${clave}.`);
  }
  if (gate.tipo === 'npm') {
    if (!esTexto(gate.script)) problemas.push(`${etiqueta}: falta el script de npm.`);
    else if (!SCRIPT_NPM.test(gate.script)) problemas.push(`${etiqueta}: el script de npm solo admite letras, números, guion, guion bajo y dos puntos.`);
  } else if (!Array.isArray(gate.args) || gate.args.length === 0 || !gate.args.every(esTexto)) {
    problemas.push(`${etiqueta}: args debe ser una lista no vacía de textos.`);
  }
  if (gate.cwd !== undefined && !cwdAdmitido(gate.cwd)) {
    problemas.push(`${etiqueta}: cwd debe ser una ruta relativa dentro del repositorio.`);
  }
  if (gate.motivo !== undefined && !esTexto(gate.motivo)) problemas.push(`${etiqueta}: motivo debe ser un texto.`);
  return problemas;
}

export function validarConfiguracion(datos) {
  if (!esObjeto(datos)) return ['La configuración debe ser un objeto con la lista «gates».'];
  const problemas = Object.keys(datos)
    .filter((clave) => clave !== 'gates')
    .map((clave) => `Clave no admitida: ${clave}.`);
  if (!Array.isArray(datos.gates) || datos.gates.length === 0) {
    return [...problemas, 'La lista «gates» falta o está vacía: una puerta sin gates no comprueba nada.'];
  }
  const nombres = new Set();
  datos.gates.forEach((gate, indice) => problemas.push(...validarGate(gate, indice, nombres)));
  return problemas;
}

export function cargarGates(raiz, ruta = RUTA_CONFIGURACION) {
  const archivo = join(raiz, ruta);
  if (!existsSync(archivo)) return { gates: [], problemas: [`No existe ${ruta}.`] };
  let datos;
  try {
    datos = JSON.parse(readFileSync(archivo, 'utf8'));
  } catch (error) {
    return { gates: [], problemas: [`${ruta} no es JSON válido: ${error.message}`] };
  }
  const problemas = validarConfiguracion(datos);
  return problemas.length > 0 ? { gates: [], problemas } : { gates: datos.gates, problemas: [] };
}

// npm en Windows es npm.cmd: lanzarlo con shell dispara el aviso DEP0190 de Node
// (argumentos concatenados sin escapar). Se invoca el CLI de npm con el propio
// node, sin shell. En POSIX npm es un binario directo (el ejecutor solo aplica shell
// en win32); si en Windows no existe el CLI, el nombre del script ya está validado.
export function resolverGate(gate, { raiz, plataforma, execPath, existe }) {
  const conCwd = gate.cwd === undefined ? {} : { cwd: resolve(raiz, gate.cwd) };
  if (gate.tipo !== 'npm') return { nombre: gate.nombre, comando: gate.tipo, args: [...gate.args], ...conCwd };

  const npmCli = join(dirname(execPath), 'node_modules', 'npm', 'bin', 'npm-cli.js');
  return plataforma === 'win32' && existe(npmCli)
    ? { nombre: gate.nombre, comando: execPath, args: [npmCli, 'run', gate.script], ...conCwd }
    : { nombre: gate.nombre, comando: 'npm', args: ['run', gate.script], ...conCwd, shell: true };
}
