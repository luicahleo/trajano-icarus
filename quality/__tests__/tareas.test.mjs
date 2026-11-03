import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  ESTADOS,
  DUENOS,
  ROLES,
  reconstruir,
  validarTransicion,
  destinoDeRechazo,
  validarBitacora,
  detectarSecretos,
} from '../lib/tareas.mjs';

const ev = (tipo, datos = {}) => ({
  ts: '2026-10-30T10:00:00.000Z',
  tipo,
  rol: 'orquestador',
  proveedor: 'anthropic',
  ...datos,
});
const paso = (de, a, datos = {}) => ev('estado', { de, a, ...datos });
const base = [ev('creada', { titulo: 'Prueba' })];
const verde = (arbol) => ev('verificacion', { resultado: 'verde', codigo: 0, arbol, rol: 'verificador' });
const rojo = (arbol) => ev('verificacion', { resultado: 'rojo', codigo: 1, arbol, rol: 'verificador' });
const contexto = (c = {}) => ({ arbol: 'a1', limpio: true, sinPush: 0, problemasFicha: [], ...c });

test('una tarea nueva empieza en RECIBIDA con el orquestador como dueño', () => {
  const t = reconstruir(base);
  assert.equal(t.estado, 'RECIBIDA');
  assert.equal(t.responsable, 'orquestador');
});

test('el estado se deriva de la bitácora', () => {
  const t = reconstruir([...base, paso('RECIBIDA', 'DESARROLLO')]);
  assert.equal(t.estado, 'DESARROLLO');
  assert.equal(t.responsable, 'desarrollador');
});

test('todo estado no terminal tiene dueño', () => {
  for (const e of ESTADOS.filter((x) => !['CERRADA', 'CANCELADA'].includes(x))) {
    assert.ok(DUENOS[e], `sin dueño: ${e}`);
  }
});

test('rechaza una transición que no existe', () => {
  const t = reconstruir(base);
  assert.match(
    validarTransicion(t, 'PRUEBA_MANUAL', { rol: 'orquestador', ...contexto() }),
    /no permitida/,
  );
});

test('rechaza a un rol que no es dueño del estado actual', () => {
  const t = reconstruir([...base, paso('RECIBIDA', 'DESARROLLO')]);
  assert.match(
    validarTransicion(t, 'VERIFICACION', { rol: 'revisor', ...contexto() }),
    /dueño/,
  );
});

test('el orquestador puede encaminar cualquier estado', () => {
  const t = reconstruir([...base, paso('RECIBIDA', 'DESARROLLO')]);
  assert.equal(validarTransicion(t, 'VERIFICACION', { rol: 'orquestador', ...contexto() }), null);
});

test('no se sale de VERIFICACION hacia adelante sin evidencia verde', () => {
  const pasos = [...base, paso('RECIBIDA', 'DESARROLLO'), paso('DESARROLLO', 'VERIFICACION')];
  const sinEvidencia = validarTransicion(reconstruir(pasos), 'ENTORNO', { rol: 'verificador', ...contexto() });
  assert.match(sinEvidencia, /evidencia verde/);
  const conRojo = validarTransicion(reconstruir([...pasos, rojo('a1')]), 'ENTORNO', { rol: 'verificador', ...contexto() });
  assert.match(conRojo, /evidencia verde/);
});

test('la evidencia verde de otro árbol no vale', () => {
  const pasos = [...base, paso('RECIBIDA', 'DESARROLLO'), paso('DESARROLLO', 'VERIFICACION'), verde('viejo')];
  assert.match(
    validarTransicion(reconstruir(pasos), 'ENTORNO', { rol: 'verificador', ...contexto({ arbol: 'nuevo' }) }),
    /cambió/,
  );
});

test('con evidencia verde del árbol actual se avanza', () => {
  const pasos = [...base, paso('RECIBIDA', 'DESARROLLO'), paso('DESARROLLO', 'VERIFICACION'), verde('a1')];
  assert.equal(validarTransicion(reconstruir(pasos), 'ENTORNO', { rol: 'verificador', ...contexto() }), null);
});

test('un rojo se puede devolver a DESARROLLO', () => {
  const pasos = [...base, paso('RECIBIDA', 'DESARROLLO'), paso('DESARROLLO', 'VERIFICACION'), rojo('a1')];
  assert.equal(validarTransicion(reconstruir(pasos), 'DESARROLLO', { rol: 'verificador', ...contexto() }), null);
});

const hastaEntorno = [
  ...base,
  paso('RECIBIDA', 'DESARROLLO'),
  paso('DESARROLLO', 'VERIFICACION'),
  verde('a1'),
  paso('VERIFICACION', 'ENTORNO'),
];

test('PRUEBA_MANUAL exige árbol limpio', () => {
  const t = reconstruir(hastaEntorno);
  assert.match(
    validarTransicion(t, 'PRUEBA_MANUAL', { rol: 'operaciones', ...contexto({ limpio: false }) }),
    /sin commitear/,
  );
  assert.equal(validarTransicion(t, 'PRUEBA_MANUAL', { rol: 'operaciones', ...contexto() }), null);
});

test('PRUEBA_MANUAL exige que el árbol siga siendo el verificado', () => {
  assert.match(
    validarTransicion(reconstruir(hastaEntorno), 'PRUEBA_MANUAL', { rol: 'operaciones', ...contexto({ arbol: 'otro' }) }),
    /cambió/,
  );
});

test('ningún agente alcanza APROBADA por una transición normal', () => {
  const t = reconstruir([...hastaEntorno, paso('ENTORNO', 'PRUEBA_MANUAL')]);
  assert.match(
    validarTransicion(t, 'APROBADA', { rol: 'orquestador', ...contexto() }),
    /no permitida|aprobar/,
  );
});

test('el rechazo se encamina según la clasificación', () => {
  assert.equal(destinoDeRechazo('funcional'), 'DESARROLLO');
  assert.equal(destinoDeRechazo('entorno'), 'ENTORNO');
  assert.equal(destinoDeRechazo('negocio'), 'ESPERA_DECISION');
  assert.equal(destinoDeRechazo('alcance'), 'PLANIFICACION');
  assert.equal(destinoDeRechazo('otra'), null);
});

const aprobada = [
  ...hastaEntorno,
  paso('ENTORNO', 'PRUEBA_MANUAL'),
  ev('aprobacion', { rol: 'humano', arbol: 'a1', commit: 'abc1234' }),
];

test('la aprobación deja la tarea en APROBADA', () => {
  assert.equal(reconstruir(aprobada).estado, 'APROBADA');
});

const documentando = [...aprobada, paso('APROBADA', 'DOCUMENTACION')];

test('DOCUMENTACION es del documentador', () => {
  assert.ok(ROLES.includes('documentador'));
  assert.equal(DUENOS.DOCUMENTACION, 'documentador');
  assert.equal(reconstruir(documentando).responsable, 'documentador');
});

test('tras la aprobación se documenta antes de cerrar', () => {
  const t = reconstruir(aprobada);
  assert.equal(validarTransicion(t, 'DOCUMENTACION', { rol: 'orquestador', ...contexto() }), null);
  assert.match(validarTransicion(t, 'CERRADA', { rol: 'orquestador', ...contexto() }), /DOCUMENTACION/);
});

test('DOCUMENTACION solo sale hacia CERRADA', () => {
  const t = reconstruir(documentando);
  assert.match(validarTransicion(t, 'DESARROLLO', { rol: 'orquestador', ...contexto() }), /no permitida/);
});

test('CERRADA exige el árbol aprobado, la ficha comprobada y ningún commit sin push', () => {
  const t = reconstruir(documentando);
  const cerrar = (c) => validarTransicion(t, 'CERRADA', { rol: 'documentador', ...contexto(c) });
  assert.match(cerrar({ arbol: 'otro' }), /aprobado/);
  assert.match(cerrar({ problemasFicha: undefined }), /ficha/);
  assert.match(cerrar({ problemasFicha: ['Falta ficha.md.'] }), /ficha.*Falta ficha\.md/);
  assert.match(cerrar({ sinPush: 2 }), /push/);
  assert.equal(cerrar(), null);
});

test('validarBitacora acepta el cierre directo de las tareas anteriores al documentador', () => {
  assert.deepEqual(validarBitacora([...aprobada, paso('APROBADA', 'CERRADA')]), []);
  assert.deepEqual(validarBitacora([...documentando, paso('DOCUMENTACION', 'CERRADA')]), []);
});

test('un rechazo deja la tarea en su destino', () => {
  const t = reconstruir([
    ...hastaEntorno,
    paso('ENTORNO', 'PRUEBA_MANUAL'),
    ev('rechazo', { clasificacion: 'negocio', a: 'ESPERA_DECISION' }),
  ]);
  assert.equal(t.estado, 'ESPERA_DECISION');
  assert.equal(t.rechazos, 1);
});

test('BLOQUEADA solo vuelve al estado previo', () => {
  const t = reconstruir([...base, paso('RECIBIDA', 'DESARROLLO'), paso('DESARROLLO', 'BLOQUEADA')]);
  assert.equal(t.estadoPrevio, 'DESARROLLO');
  assert.equal(validarTransicion(t, 'DESARROLLO', { rol: 'orquestador', ...contexto() }), null);
  assert.match(validarTransicion(t, 'ENTORNO', { rol: 'orquestador', ...contexto() }), /no permitida/);
});

test('todo estado no terminal puede bloquearse o cancelarse', () => {
  const t = reconstruir([...base, paso('RECIBIDA', 'DESARROLLO')]);
  assert.equal(validarTransicion(t, 'BLOQUEADA', { rol: 'desarrollador', ...contexto() }), null);
  assert.equal(validarTransicion(t, 'CANCELADA', { rol: 'orquestador', ...contexto() }), null);
});

test('un estado terminal no admite salidas', () => {
  const t = reconstruir([...base, paso('RECIBIDA', 'CANCELADA')]);
  assert.match(validarTransicion(t, 'DESARROLLO', { rol: 'orquestador', ...contexto() }), /terminal/);
});

test('el relevo no cambia el estado', () => {
  const t = reconstruir([...base, paso('RECIBIDA', 'DESARROLLO'), ev('relevo', { proveedor: 'moonshot', motivo: 'sin tokens' })]);
  assert.equal(t.estado, 'DESARROLLO');
  assert.equal(t.proveedor, 'moonshot');
});

test('validarBitacora acepta una historia legal', () => {
  assert.deepEqual(validarBitacora(aprobada), []);
});

test('validarBitacora detecta un estado que no encadena', () => {
  const errores = validarBitacora([...base, paso('DESARROLLO', 'VERIFICACION')]);
  assert.equal(errores.length, 1);
  assert.match(errores[0], /no encadena/);
});

test('validarBitacora detecta una transición inexistente', () => {
  const errores = validarBitacora([...base, paso('RECIBIDA', 'APROBADA')]);
  assert.match(errores.join(), /no permitida/);
});

test('validarBitacora exige que empiece con creada', () => {
  assert.match(validarBitacora([paso('RECIBIDA', 'DESARROLLO')]).join(), /creada/);
});

test('validarBitacora rechaza una aprobación que no venga de PRUEBA_MANUAL', () => {
  const errores = validarBitacora([...base, ev('aprobacion', { rol: 'humano', arbol: 'a1' })]);
  assert.match(errores.join(), /PRUEBA_MANUAL/);
});

test('validarBitacora rechaza una aprobación que no sea humana', () => {
  const errores = validarBitacora([
    ...hastaEntorno,
    paso('ENTORNO', 'PRUEBA_MANUAL'),
    ev('aprobacion', { rol: 'orquestador', arbol: 'a1' }),
  ]);
  assert.match(errores.join(), /humano/);
});

test('detectarSecretos reconoce credenciales típicas', () => {
  for (const texto of [
    'Authorization: eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTYifQ.abc',
    'clave sk-ant-api03-abcdefghijklmnopqrstuvwx',
    'ghp_abcdefghijklmnopqrstuvwxyz0123456789',
    'password=SuperSecreta1',
    'contraseña: hunter22222',
    'ApiKey = 9f8e7d6c5b4a',
  ]) {
    assert.equal(detectarSecretos(texto), true, texto);
  }
});

test('detectarSecretos no marca texto normal', () => {
  for (const texto of [
    'Falla el token de refresco al expirar la sesión',
    'Se cambió la validación de password en el formulario',
    'commit b5d30d2 con árbol 9f2c1e7',
  ]) {
    assert.equal(detectarSecretos(texto), false, texto);
  }
});

test('PLANIFICACION a DESARROLLO exige criterios comprobados y sin problemas', () => {
  const t = reconstruir([...base, paso('RECIBIDA', 'PLANIFICACION')]);
  const desarrollar = (c) => validarTransicion(t, 'DESARROLLO', { rol: 'planificador', ...contexto(c) });
  assert.match(desarrollar(), /No se comprobaron los criterios/);
  assert.match(
    desarrollar({ problemasCriterios: ['Criterio 1: sin línea «Evidencia:».'] }),
    /criterios no están listos: Criterio 1: sin línea/,
  );
  assert.equal(desarrollar({ problemasCriterios: [] }), null);
});
