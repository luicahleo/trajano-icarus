// Máquina de estados de las tareas de agentes. Lógica pura: el estado se deriva
// siempre de la bitácora, así que no existe un archivo de estado que pueda
// desincronizarse. Ver docs/ai/agentes/PROTOCOLO.md.

export const TERMINALES = ['CERRADA', 'CANCELADA'];

export const DUENOS = {
  RECIBIDA: 'orquestador',
  PLANIFICACION: 'planificador',
  ESPERA_DECISION: 'humano',
  DESARROLLO: 'desarrollador',
  VERIFICACION: 'verificador',
  REVISION: 'revisor',
  ENTORNO: 'operaciones',
  PRUEBA_MANUAL: 'humano',
  APROBADA: 'orquestador',
  DOCUMENTACION: 'documentador',
  BLOQUEADA: 'orquestador',
};

export const ESTADOS = [...Object.keys(DUENOS), ...TERMINALES];

export const ROLES = [
  'orquestador',
  'planificador',
  'desarrollador',
  'verificador',
  'revisor',
  'operaciones',
  'documentador',
];

// Sin PRUEBA_MANUAL: de ahí solo se sale con los eventos de aprobación y
// rechazo. Sin BLOQUEADA: vuelve únicamente a su estado previo. APROBADA a
// CERRADA solo sobrevive para validar bitácoras anteriores al documentador:
// validarTransicion la prohíbe.
const SIGUIENTES = {
  RECIBIDA: ['PLANIFICACION', 'DESARROLLO', 'ESPERA_DECISION'],
  PLANIFICACION: ['DESARROLLO', 'ESPERA_DECISION'],
  ESPERA_DECISION: ['PLANIFICACION', 'DESARROLLO'],
  DESARROLLO: ['VERIFICACION', 'PLANIFICACION', 'ESPERA_DECISION'],
  VERIFICACION: ['REVISION', 'ENTORNO', 'DESARROLLO'],
  REVISION: ['ENTORNO', 'DESARROLLO', 'ESPERA_DECISION'],
  ENTORNO: ['PRUEBA_MANUAL', 'DESARROLLO'],
  PRUEBA_MANUAL: [],
  APROBADA: ['DOCUMENTACION', 'CERRADA'],
  DOCUMENTACION: ['CERRADA'],
  BLOQUEADA: [],
};

const DESTINOS_RECHAZO = {
  funcional: 'DESARROLLO',
  entorno: 'ENTORNO',
  negocio: 'ESPERA_DECISION',
  alcance: 'PLANIFICACION',
};

const TIPOS_EVENTO = [
  'creada',
  'estado',
  'verificacion',
  'rechazo',
  'aprobacion',
  'relevo',
  'decision',
  'bloqueo',
  'comando',
  'nota',
];

export const destinoDeRechazo = (clasificacion) => DESTINOS_RECHAZO[clasificacion] ?? null;

function aplicar(tarea, ev) {
  const t = { ...tarea, proveedor: ev.rol === 'humano' ? tarea.proveedor : ev.proveedor };
  switch (ev.tipo) {
    case 'creada':
      return { ...t, titulo: ev.titulo, estado: 'RECIBIDA' };
    case 'estado':
      return {
        ...t,
        estado: ev.a,
        estadoPrevio: ev.a === 'BLOQUEADA' ? ev.de : t.estadoPrevio,
      };
    case 'verificacion':
      return { ...t, ultimaVerificacion: ev };
    case 'aprobacion':
      return { ...t, estado: 'APROBADA', aprobacion: ev };
    case 'rechazo':
      return { ...t, estado: ev.a, rechazos: t.rechazos + 1 };
    default:
      return t;
  }
}

const TAREA_VACIA = {
  estado: null,
  estadoPrevio: null,
  proveedor: null,
  ultimaVerificacion: null,
  aprobacion: null,
  rechazos: 0,
};

const conResponsable = (t) => ({ ...t, responsable: DUENOS[t.estado] ?? null });

export const reconstruir = (eventos) => conResponsable(eventos.reduce(aplicar, TAREA_VACIA));

function transicionExiste(tarea, destino) {
  if (TERMINALES.includes(tarea.estado)) return false;
  if (destino === 'BLOQUEADA' || destino === 'CANCELADA') return true;
  if (tarea.estado === 'BLOQUEADA') return destino === tarea.estadoPrevio;
  return SIGUIENTES[tarea.estado]?.includes(destino) ?? false;
}

function exigirEvidencia(tarea, arbol) {
  const v = tarea.ultimaVerificacion;
  if (!v || v.resultado !== 'verde') return 'Hace falta evidencia verde: ejecuta verificar.';
  if (v.arbol !== arbol) return 'El contenido cambió desde la última verificación: repite verificar.';
  return null;
}

// Devuelve el motivo del rechazo, o null si la transición es válida.
export function validarTransicion(tarea, destino, { rol, arbol, limpio, sinPush, problemasFicha, problemasCriterios }) {
  if (TERMINALES.includes(tarea.estado)) return `La tarea está en un estado terminal: ${tarea.estado}.`;
  if (!transicionExiste(tarea, destino)) return `Transición no permitida: ${tarea.estado} a ${destino}.`;
  if (tarea.estado === 'APROBADA' && destino === 'CERRADA') {
    return 'Tras la aprobación, la tarea pasa por DOCUMENTACION antes de cerrarse.';
  }

  const dueno = DUENOS[tarea.estado];
  if (rol !== 'orquestador' && rol !== dueno) {
    return `El rol ${rol} no es dueño de ${tarea.estado}; su dueño es ${dueno}.`;
  }

  const haciaAdelante =
    (tarea.estado === 'VERIFICACION' && ['REVISION', 'ENTORNO'].includes(destino)) ||
    (tarea.estado === 'REVISION' && destino === 'ENTORNO') ||
    destino === 'PRUEBA_MANUAL';
  if (haciaAdelante) {
    const motivo = exigirEvidencia(tarea, arbol);
    if (motivo) return motivo;
  }

  if (tarea.estado === 'PLANIFICACION' && destino === 'DESARROLLO') {
    if (!Array.isArray(problemasCriterios)) return 'No se comprobaron los criterios de aceptación.';
    if (problemasCriterios.length > 0) return `Los criterios no están listos: ${problemasCriterios.join(' ')}`;
  }

  if (destino === 'PRUEBA_MANUAL' && !limpio) {
    return 'Hay cambios sin commitear: la prueba manual debe correr sobre un commit.';
  }

  if (destino === 'CERRADA') {
    if (tarea.aprobacion?.arbol !== arbol) return 'El contenido no es el aprobado por el humano.';
    if (!Array.isArray(problemasFicha)) return 'No se comprobó la ficha de la tarea.';
    if (problemasFicha.length > 0) return `La ficha no está lista: ${problemasFicha.join(' ')}`;
    if (sinPush !== 0) return `Hay ${sinPush} commit(s) sin push.`;
  }

  return null;
}

// Rehace la historia completa. La usa el gate para detectar bitácoras editadas.
export function validarBitacora(eventos) {
  if (eventos[0]?.tipo !== 'creada') return ['La bitácora debe empezar con un evento creada.'];

  const errores = [];
  let tarea = aplicar(TAREA_VACIA, eventos[0]);

  eventos.slice(1).forEach((ev, i) => {
    const linea = i + 2;
    if (!TIPOS_EVENTO.includes(ev.tipo)) {
      errores.push(`Línea ${linea}: tipo de evento desconocido: ${ev.tipo}.`);
    } else if (ev.tipo === 'estado') {
      if (ev.de !== tarea.estado) {
        errores.push(`Línea ${linea}: el estado ${ev.de} no encadena con ${tarea.estado}.`);
      } else if (!transicionExiste(tarea, ev.a)) {
        errores.push(`Línea ${linea}: transición no permitida: ${ev.de} a ${ev.a}.`);
      }
    } else if (ev.tipo === 'aprobacion' || ev.tipo === 'rechazo') {
      if (tarea.estado !== 'PRUEBA_MANUAL') {
        errores.push(`Línea ${linea}: ${ev.tipo} fuera de PRUEBA_MANUAL.`);
      }
      if (ev.tipo === 'aprobacion' && ev.rol !== 'humano') {
        errores.push(`Línea ${linea}: la aprobación solo puede ser del humano.`);
      }
    }
    tarea = aplicar(tarea, ev);
  });

  return errores;
}

const PATRONES_SECRETO = [
  /eyJ[\w-]{8,}\.[\w-]{8,}\.[\w-]*/,
  /\bsk-[\w-]{20,}/,
  /\bgh[pousr]_[A-Za-z0-9]{20,}/,
  /\b(?:password|contrase(?:ñ|n)a|api[_ -]?key|secret|token|clave)\s*[:=]\s*\S{6,}/i,
];

export const detectarSecretos = (texto) => PATRONES_SECRETO.some((p) => p.test(texto));
