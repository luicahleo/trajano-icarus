// Criterios de aceptación con evidencia. Portable: lógica pura que no conoce
// rutas ni estados de ningún proyecto; quien llama inyecta la lectura de las
// pruebas. Formato en docs/ai/tareas/README.md.

export const MINIMO_JUSTIFICACION = 10;
export const MINIMO_NOMBRE_PRUEBA = 8;

// Patrón genérico y portable de un archivo de pruebas: carpeta __tests__, tests o
// *Tests, o archivo *.test.* / *.spec.* / *Tests.cs. Las rutas bajo
// docs/ai/tareas/ (tarea.md, bitácora, ficha) nunca cuentan como prueba.
const RUTA_DE_PRUEBAS = /(^|\/)(__tests__|tests?|[^/]*Tests)\/|\.(test|spec)\.[^/]+$|Tests\.cs$/;
const RUTA_DE_TAREAS = /^(\.\/)?docs\/ai\/tareas\//;
export const esRutaDePruebas = (ruta) => {
  const normal = ruta.replace(/\\/g, '/');
  return !RUTA_DE_TAREAS.test(normal) && RUTA_DE_PRUEBAS.test(normal);
};

const SECCION = /^## Criterios de aceptación\s*$/;
const OTRA_SECCION = /^## /;
const ENCABEZADO = /^#{1,6} /;
const ITEM = /^(\d+)\.\s/;
const EVIDENCIA = /^Evidencia:\s*(.*)$/;
const TEST = /^test\s+`([^`]+)`\s+«([^»]+)»$/;
const MANUAL = /^manual\s+[—-]\s+(.+)$/;
const ROJO = /^Rojo criterio (\d+):/;

function lineasDeLaSeccion(tareaMd) {
  const lineas = tareaMd.replace(/\r\n/g, '\n').split('\n');
  const inicio = lineas.findIndex((linea) => SECCION.test(linea));
  if (inicio === -1) return [];
  const resto = lineas.slice(inicio + 1);
  const fin = resto.findIndex((linea) => OTRA_SECCION.test(linea));
  return fin === -1 ? resto : resto.slice(0, fin);
}

export function leerEvidencia(texto) {
  const test = texto.match(TEST);
  if (test) return { tipo: 'test', ruta: test[1], nombre: test[2] };
  const manual = texto.match(MANUAL);
  if (manual) return { tipo: 'manual', justificacion: manual[1].trim() };
  return { tipo: 'desconocida', texto };
}

// Un criterio empieza en una línea «N. » sin sangría y dura hasta el siguiente
// criterio o encabezado. Sus evidencias son las líneas «Evidencia:» de ese tramo.
export function leerCriterios(tareaMd) {
  const criterios = [];
  let actual = null;
  for (const linea of lineasDeLaSeccion(tareaMd)) {
    const item = linea.match(ITEM);
    if (item) {
      actual = { numero: Number(item[1]), evidencias: [] };
      criterios.push(actual);
    } else if (ENCABEZADO.test(linea)) {
      actual = null;
    } else {
      const evidencia = linea.trim().match(EVIDENCIA);
      if (evidencia && actual) actual.evidencias.push(leerEvidencia(evidencia[1].trim()));
    }
  }
  return criterios;
}

export function validarCriterios(criterios, { exigirAlMenosUno = false } = {}) {
  const problemas = [];
  if (exigirAlMenosUno && criterios.length === 0) problemas.push('No hay criterios de aceptación numerados.');

  const vistos = new Set();
  for (const { numero, evidencias } of criterios) {
    if (vistos.has(numero)) problemas.push(`Criterio ${numero}: número repetido.`);
    vistos.add(numero);
    if (evidencias.length === 0) problemas.push(`Criterio ${numero}: sin línea «Evidencia:».`);
    for (const evidencia of evidencias) {
      if (evidencia.tipo === 'desconocida') {
        problemas.push(`Criterio ${numero}: evidencia con formato desconocido: «${evidencia.texto}».`);
      } else if (evidencia.tipo === 'test' && !esRutaDePruebas(evidencia.ruta)) {
        problemas.push(
          `Criterio ${numero}: ${evidencia.ruta} no es un archivo de pruebas reconocible (__tests__/, tests/, Tests/, .test., .spec. o *Tests.cs) o está bajo docs/ai/tareas/.`,
        );
      } else if (evidencia.tipo === 'test' && evidencia.nombre.trim().length < MINIMO_NOMBRE_PRUEBA) {
        problemas.push(`Criterio ${numero}: el nombre de la prueba debe tener al menos ${MINIMO_NOMBRE_PRUEBA} caracteres.`);
      } else if (evidencia.tipo === 'manual' && evidencia.justificacion.length < MINIMO_JUSTIFICACION) {
        problemas.push(
          `Criterio ${numero}: la evidencia manual necesita una justificación de al menos ${MINIMO_JUSTIFICACION} caracteres.`,
        );
      }
    }
  }
  return problemas;
}

// El rojo lo registra el desarrollador con un evento comando cuyo texto empieza
// por «Rojo criterio N:». Basta uno por criterio.
export const rojosRegistrados = (eventos) =>
  new Set(
    eventos
      .filter((evento) => evento.tipo === 'comando' && typeof evento.texto === 'string')
      .map((evento) => evento.texto.match(ROJO))
      .filter(Boolean)
      .map((coincidencia) => Number(coincidencia[1])),
  );

// leerPrueba(ruta) devuelve el contenido del archivo, o null si no existe o la
// ruta no es admisible.
export function comprobarEvidencias(criterios, { leerPrueba, rojos }) {
  const problemas = [];
  for (const { numero, evidencias } of criterios) {
    const pruebas = evidencias.filter((evidencia) => evidencia.tipo === 'test');
    for (const { ruta, nombre } of pruebas) {
      const contenido = leerPrueba(ruta);
      if (contenido === null) problemas.push(`Criterio ${numero}: no existe la prueba ${ruta}.`);
      else if (!contenido.includes(nombre)) problemas.push(`Criterio ${numero}: ${ruta} no contiene la prueba «${nombre}».`);
    }
    if (pruebas.length > 0 && !rojos.has(numero)) {
      problemas.push(`Criterio ${numero}: falta registrar la prueba vista en rojo con «Rojo criterio ${numero}: ...».`);
    }
  }
  return problemas;
}
