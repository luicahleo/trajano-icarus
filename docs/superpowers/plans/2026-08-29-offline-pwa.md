# Modo offline de la PWA — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permitir que el trabajador registre recogida y mortalidad sin conexión
en la PWA, con cola en IndexedDB, precalentado de los datos del día tras el
login, sesión offline sin persistir el token y sincronización automática con
reintentos.

**Architecture:** Cola de operaciones en IndexedDB detrás de la interfaz
`AlmacenCola` (implementaciones IndexedDB y memoria), motor de sincronización
genérico en `web/src/lib/offline/`, coordinador singleton en
`web/src/app/offline/` que cablea almacén + motor + dispatcher avícola,
precalentado de caché para el rol Trabajador, snapshot de sesión offline sin
token, y cambios acotados en los diálogos de alta, el banner, el layout y
`AuthContext`. Spec:
`docs/superpowers/specs/2026-08-29-offline-pwa-design.md`.

**Tech Stack:** React 19 + TypeScript estricto, MUI 9, TanStack Query 5, Vitest +
Testing Library, IndexedDB nativo (sin dependencias de runtime nuevas),
`fake-indexeddb` solo como devDependency.

## Global Constraints

- Textos e identificadores en español correcto, UTF-8 sin BOM. Nunca mojibake.
- TypeScript estricto: sin `any` ni aserciones inseguras.
- Imports relativos (sin alias `@/`). `src/lib/` no importa de `features/` ni de
  `app/`.
- Anti-PII: la cola y la caché guardan solo datos de negocio; nunca tokens,
  credenciales ni datos nominales. Nada de `console.log` con respuestas.
- No añadir dependencias de runtime. Única dependencia nueva permitida:
  `fake-indexeddb` en devDependencies.
- TDD: cada test se ejecuta y se ve en rojo antes de implementar.
- Tests desde `web/`: `npm run test` (o `npx vitest run <archivo>` para el
  dirigido). Lint: `npm run lint`. Formato: `npm run format:check`.
- Antes de cada commit: puerta de calidad completa `./verify.ps1` desde la raíz
  del repo (Docker corriendo). Prohibido `--no-verify`.
- Los mensajes de commit siguen el estilo del repo: `feat(web): ...`,
  `test(web): ...`, en español.

---
### Task 1: Tipos y almacén de cola en memoria

**Files:**
- Create: `web/src/lib/offline/tipos.ts`
- Create: `web/src/lib/offline/almacenCola.ts`
- Test: `web/src/lib/offline/almacenCola.test.ts`

**Interfaces:**
- Consumes: nada (primer ladrillo del módulo).
- Produces (usado por Tasks 2, 3, 4, 7):
  - `TipoOperacionOffline = 'produccion.crear' | 'mortalidad.crear'`
  - `OperacionPendiente { id: string; tipo: TipoOperacionOffline; galponId: string; cuerpo: unknown; estado: 'pendiente' | 'error'; intentos: number; creadoEn: string; proximoIntentoEn: string | null }`
  - `AlmacenCola` con `agregar`, `listarPendientes(ahoraIso: string, limite: number)`, `listarTodas()`, `eliminar(id)`, `actualizar(id, cambios)`, `contar()`
  - `crearAlmacenColaMemoria(): AlmacenCola`

- [ ] **Step 1: Escribir el test que falla**

`web/src/lib/offline/almacenCola.test.ts`:

```ts
import { describe, expect, test } from 'vitest';
import { crearAlmacenColaMemoria } from './almacenCola';
import type { OperacionPendiente } from './tipos';

const op = (id: string, extra: Partial<OperacionPendiente> = {}): OperacionPendiente => ({
  id,
  tipo: 'produccion.crear',
  galponId: 'g1',
  cuerpo: { cantidadMaples: 1 },
  estado: 'pendiente',
  intentos: 0,
  creadoEn: '2026-08-29T10:00:00.000Z',
  proximoIntentoEn: null,
  ...extra,
});

describe('AlmacenCola en memoria', () => {
  test('agregar y contar', async () => {
    const a = crearAlmacenColaMemoria();
    await a.agregar(op('1'));
    await a.agregar(op('2'));
    expect(await a.contar()).toBe(2);
  });

  test('listarPendientes excluye error y respeta proximoIntentoEn', async () => {
    const a = crearAlmacenColaMemoria();
    await a.agregar(op('lista'));
    await a.agregar(op('error', { estado: 'error' }));
    await a.agregar(op('futura', { proximoIntentoEn: '2026-08-29T12:00:00.000Z' }));
    const r = await a.listarPendientes('2026-08-29T11:00:00.000Z', 50);
    expect(r.map((x) => x.id)).toEqual(['lista']);
  });

  test('listarPendientes respeta el límite', async () => {
    const a = crearAlmacenColaMemoria();
    await a.agregar(op('1'));
    await a.agregar(op('2'));
    await a.agregar(op('3'));
    expect((await a.listarPendientes('2026-08-29T11:00:00.000Z', 2)).length).toBe(2);
  });

  test('actualizar cambia estado, intentos y proximoIntentoEn', async () => {
    const a = crearAlmacenColaMemoria();
    await a.agregar(op('1'));
    await a.actualizar('1', { intentos: 1, proximoIntentoEn: '2026-08-29T11:02:00.000Z' });
    const [r] = await a.listarTodas();
    expect(r.intentos).toBe(1);
    expect(r.proximoIntentoEn).toBe('2026-08-29T11:02:00.000Z');
  });

  test('eliminar quita la operación', async () => {
    const a = crearAlmacenColaMemoria();
    await a.agregar(op('1'));
    await a.eliminar('1');
    expect(await a.contar()).toBe(0);
  });
});
```

- [ ] **Step 2: Ejecutar y verificar que falla**

Run: `cd web && npx vitest run src/lib/offline/almacenCola.test.ts`
Expected: FAIL — no existe `./almacenCola`.

- [ ] **Step 3: Implementación mínima**

`web/src/lib/offline/tipos.ts`:

```ts
// Cola offline: solo datos de negocio. Nunca tokens ni credenciales (anti-PII).
export type TipoOperacionOffline = 'produccion.crear' | 'mortalidad.crear';
export type EstadoOperacion = 'pendiente' | 'error';

export interface OperacionPendiente {
  id: string; // uuid local
  tipo: TipoOperacionOffline;
  galponId: string;
  cuerpo: unknown; // DatosRecogida | DatosBajas (definidos en features/avicola)
  estado: EstadoOperacion;
  intentos: number;
  creadoEn: string; // ISO
  proximoIntentoEn: string | null; // ISO; null = listo para enviar
}
```

`web/src/lib/offline/almacenCola.ts`:

```ts
import type { OperacionPendiente } from './tipos';

export interface AlmacenCola {
  agregar(op: OperacionPendiente): Promise<void>;
  // Pendientes listas para enviar: estado 'pendiente' y proximoIntentoEn vencido.
  listarPendientes(ahoraIso: string, limite: number): Promise<OperacionPendiente[]>;
  listarTodas(): Promise<OperacionPendiente[]>;
  eliminar(id: string): Promise<void>;
  actualizar(
    id: string,
    cambios: Partial<Pick<OperacionPendiente, 'estado' | 'intentos' | 'proximoIntentoEn'>>,
  ): Promise<void>;
  contar(): Promise<number>;
}

export function crearAlmacenColaMemoria(): AlmacenCola {
  const ops = new Map<string, OperacionPendiente>();
  return {
    async agregar(op) {
      ops.set(op.id, op);
    },
    async listarPendientes(ahoraIso, limite) {
      return [...ops.values()]
        .filter(
          (o) =>
            o.estado === 'pendiente' &&
            (o.proximoIntentoEn === null || o.proximoIntentoEn <= ahoraIso),
        )
        .slice(0, limite);
    },
    async listarTodas() {
      return [...ops.values()];
    },
    async eliminar(id) {
      ops.delete(id);
    },
    async actualizar(id, cambios) {
      const actual = ops.get(id);
      if (actual) ops.set(id, { ...actual, ...cambios });
    },
    async contar() {
      return ops.size;
    },
  };
}
```

- [ ] **Step 4: Ejecutar y verificar que pasa**

Run: `cd web && npx vitest run src/lib/offline/almacenCola.test.ts`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
./verify.ps1   # desde la raíz del repo, puerta completa
git add web/src/lib/offline/
git commit -m "feat(web): tipos y almacén en memoria de la cola offline"
```

---

### Task 2: Almacén IndexedDB (real) con fake-indexeddb para tests

**Files:**
- Create: `web/src/lib/offline/baseDatosOffline.ts`
- Create: `web/src/lib/offline/almacenIndexedDb.ts`
- Test: `web/src/lib/offline/almacenIndexedDb.test.ts`
- Modify: `web/package.json` (devDependency `fake-indexeddb`)

**Interfaces:**
- Consumes: `AlmacenCola`, `OperacionPendiente` de Task 1.
- Produces (usado por Tasks 4 y 8):
  - `abrirBaseDatosOffline(): Promise<IDBDatabase>` — crea los object stores
    `operaciones` (keyPath `id`) y `cache-lectura` (keyPath `clave`) en la
    versión 1, para no migrar después.
  - `crearAlmacenColaIndexedDb(): AlmacenCola`

- [ ] **Step 1: Instalar la devDependency**

```bash
cd web && npm install -D fake-indexeddb
```

- [ ] **Step 2: Escribir el test que falla**

`web/src/lib/offline/almacenIndexedDb.test.ts`:

```ts
import 'fake-indexeddb/auto';
import { describe, expect, test } from 'vitest';
import { crearAlmacenColaIndexedDb } from './almacenIndexedDb';
import type { OperacionPendiente } from './tipos';

const op = (id: string, extra: Partial<OperacionPendiente> = {}): OperacionPendiente => ({
  id,
  tipo: 'mortalidad.crear',
  galponId: 'g1',
  cuerpo: { cantidadMuertas: 2 },
  estado: 'pendiente',
  intentos: 0,
  creadoEn: '2026-08-29T10:00:00.000Z',
  proximoIntentoEn: null,
  ...extra,
});

describe('AlmacenCola IndexedDB', () => {
  test('mismo contrato que el almacén en memoria', async () => {
    const a = crearAlmacenColaIndexedDb();
    await a.agregar(op('lista'));
    await a.agregar(op('error', { estado: 'error' }));
    await a.agregar(op('futura', { proximoIntentoEn: '2026-08-29T12:00:00.000Z' }));
    expect(await a.contar()).toBe(3);
    const r = await a.listarPendientes('2026-08-29T11:00:00.000Z', 50);
    expect(r.map((x) => x.id)).toEqual(['lista']);
    await a.actualizar('lista', { intentos: 1 });
    expect((await a.listarTodas()).find((x) => x.id === 'lista')?.intentos).toBe(1);
    await a.eliminar('lista');
    expect(await a.contar()).toBe(2);
  });
});
```

- [ ] **Step 3: Ejecutar y verificar que falla**

Run: `cd web && npx vitest run src/lib/offline/almacenIndexedDb.test.ts`
Expected: FAIL — no existe `./almacenIndexedDb`.

- [ ] **Step 4: Implementación mínima**

`web/src/lib/offline/baseDatosOffline.ts`:

```ts
// Base única de la app para offline. Los dos stores se crean en la versión 1
// para no necesitar migraciones: operaciones (cola) y cache-lectura.
const NOMBRE_BD = 'icarus-offline';

export function abrirBaseDatosOffline(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const pedido = indexedDB.open(NOMBRE_BD, 1);
    pedido.onupgradeneeded = () => {
      const bd = pedido.result;
      if (!bd.objectStoreNames.contains('operaciones')) {
        bd.createObjectStore('operaciones', { keyPath: 'id' });
      }
      if (!bd.objectStoreNames.contains('cache-lectura')) {
        bd.createObjectStore('cache-lectura', { keyPath: 'clave' });
      }
    };
    pedido.onsuccess = () => resolve(pedido.result);
    pedido.onerror = () => reject(pedido.error);
  });
}

export function promesaDePedido<T>(pedido: IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    pedido.onsuccess = () => resolve(pedido.result);
    pedido.onerror = () => reject(pedido.error);
  });
}
```

`web/src/lib/offline/almacenIndexedDb.ts`:

```ts
import { abrirBaseDatosOffline, promesaDePedido } from './baseDatosOffline';
import type { AlmacenCola } from './almacenCola';
import type { OperacionPendiente } from './tipos';

async function conStore<T>(
  modo: IDBTransactionMode,
  usar: (store: IDBObjectStore) => Promise<T>,
): Promise<T> {
  const bd = await abrirBaseDatosOffline();
  try {
    const tx = bd.transaction('operaciones', modo);
    return await usar(tx.objectStore('operaciones'));
  } finally {
    bd.close();
  }
}

export function crearAlmacenColaIndexedDb(): AlmacenCola {
  return {
    agregar: (op) => conStore('readwrite', (s) => promesaDePedido(s.put(op)).then(() => {})),
    listarPendientes: async (ahoraIso, limite) => {
      const todas = await conStore('readonly', (s) =>
        promesaDePedido(s.getAll() as IDBRequest<OperacionPendiente[]>),
      );
      return todas
        .filter(
          (o) =>
            o.estado === 'pendiente' &&
            (o.proximoIntentoEn === null || o.proximoIntentoEn <= ahoraIso),
        )
        .slice(0, limite);
    },
    listarTodas: () =>
      conStore('readonly', (s) => promesaDePedido(s.getAll() as IDBRequest<OperacionPendiente[]>)),
    eliminar: (id) => conStore('readwrite', (s) => promesaDePedido(s.delete(id)).then(() => {})),
    actualizar: async (id, cambios) => {
      await conStore('readwrite', async (s) => {
        const actual = await promesaDePedido(s.get(id) as IDBRequest<OperacionPendiente | undefined>);
        if (actual) await promesaDePedido(s.put({ ...actual, ...cambios }));
      });
    },
    contar: () => conStore('readonly', (s) => promesaDePedido(s.count())),
  };
}
```

- [ ] **Step 5: Ejecutar y verificar que pasa**

Run: `cd web && npx vitest run src/lib/offline/almacenIndexedDb.test.ts`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
./verify.ps1
git add web/src/lib/offline/ web/package.json web/package-lock.json
git commit -m "feat(web): almacén IndexedDB de la cola offline con fake-indexeddb en tests"
```

---
### Task 3: Motor de sincronización

**Files:**
- Create: `web/src/lib/offline/motorSincronizacion.ts`
- Test: `web/src/lib/offline/motorSincronizacion.test.ts`

**Interfaces:**
- Consumes: `AlmacenCola`, `OperacionPendiente` (Task 1), `ApiError` de
  `web/src/lib/http.ts`.
- Produces (usado por Task 4):
  - `crearMotorSincronizacion(deps: { almacen: AlmacenCola; despachar: (op: OperacionPendiente) => Promise<void>; conectado: () => boolean; ahora?: () => Date }): { sincronizar: () => Promise<void> }`

Reglas (spec sección 4): no bloqueante; lote de 50; corta al perder
conectividad; 401 → pausa el ciclo sin consumir intento; 4xx → `error`
terminal; fallo de red o 5xx → intento+1 con backoff 2^intentos minutos;
3 intentos → `error`.

- [ ] **Step 1: Escribir el test que falla**

`web/src/lib/offline/motorSincronizacion.test.ts`:

```ts
import { describe, expect, test, vi } from 'vitest';
import { ApiError } from '../http';
import { crearAlmacenColaMemoria } from './almacenCola';
import { crearMotorSincronizacion } from './motorSincronizacion';
import type { OperacionPendiente } from './tipos';

const AHORA = new Date('2026-08-29T10:00:00.000Z');
const op = (id: string, extra: Partial<OperacionPendiente> = {}): OperacionPendiente => ({
  id,
  tipo: 'produccion.crear',
  galponId: 'g1',
  cuerpo: {},
  estado: 'pendiente',
  intentos: 0,
  creadoEn: AHORA.toISOString(),
  proximoIntentoEn: null,
  ...extra,
});

const motorCon = (
  almacen: ReturnType<typeof crearAlmacenColaMemoria>,
  despachar: (op: OperacionPendiente) => Promise<void>,
  conectado: () => boolean = () => true,
) => crearMotorSincronizacion({ almacen, despachar, conectado, ahora: () => AHORA });

describe('motor de sincronización', () => {
  test('despacha pendientes y los elimina de la cola', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar(op('1'));
    const despachar = vi.fn(async () => {});
    await motorCon(almacen, despachar).sincronizar();
    expect(despachar).toHaveBeenCalledTimes(1);
    expect(await almacen.contar()).toBe(0);
  });

  test('no bloqueante: un segundo ciclo simultáneo no re-despacha', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar(op('1'));
    let liberar!: () => void;
    const despachar = vi.fn(() => new Promise<void>((r) => (liberar = r)));
    const motor = motorCon(almacen, despachar);
    const ciclo1 = motor.sincronizar();
    await motor.sincronizar(); // retorna de inmediato, sin despachar
    expect(despachar).toHaveBeenCalledTimes(1);
    liberar();
    await ciclo1;
  });

  test('fallo de red: intento+1 y backoff de 2 minutos', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar(op('1'));
    const despachar = vi.fn(async () => {
      throw new TypeError('fetch failed');
    });
    await motorCon(almacen, despachar).sincronizar();
    const [r] = await almacen.listarTodas();
    expect(r.estado).toBe('pendiente');
    expect(r.intentos).toBe(1);
    expect(r.proximoIntentoEn).toBe('2026-08-29T10:02:00.000Z');
  });

  test('tercer intento fallido pasa a error terminal', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar(op('1', { intentos: 2 }));
    const despachar = vi.fn(async () => {
      throw new TypeError('fetch failed');
    });
    await motorCon(almacen, despachar).sincronizar();
    const [r] = await almacen.listarTodas();
    expect(r.estado).toBe('error');
    expect(r.intentos).toBe(3);
  });

  test('4xx pasa a error sin consumir reintentos', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar(op('1'));
    const despachar = vi.fn(async () => {
      throw new ApiError({ status: 422, code: 'Validacion' });
    });
    await motorCon(almacen, despachar).sincronizar();
    const [r] = await almacen.listarTodas();
    expect(r.estado).toBe('error');
    expect(r.intentos).toBe(0);
  });

  test('401 pausa el ciclo sin consumir intentos', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar(op('1'));
    await almacen.agregar(op('2'));
    const despachar = vi.fn(async () => {
      throw new ApiError({ status: 401 });
    });
    await motorCon(almacen, despachar).sincronizar();
    expect(despachar).toHaveBeenCalledTimes(1); // no sigue con la segunda
    const [r] = await almacen.listarTodas();
    expect(r.intentos).toBe(0);
    expect(r.estado).toBe('pendiente');
  });

  test('corta el ciclo si se pierde la conectividad', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar(op('1'));
    await almacen.agregar(op('2'));
    let online = true;
    const despachar = vi.fn(async () => {
      online = false; // la red cae tras el primer envío
    });
    await motorCon(almacen, despachar, () => online).sincronizar();
    expect(despachar).toHaveBeenCalledTimes(1);
    expect(await almacen.contar()).toBe(1);
  });

  test('no despacha operaciones en backoff futuro', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar(op('1', { proximoIntentoEn: '2026-08-29T11:00:00.000Z' }));
    const despachar = vi.fn(async () => {});
    await motorCon(almacen, despachar).sincronizar();
    expect(despachar).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Ejecutar y verificar que falla**

Run: `cd web && npx vitest run src/lib/offline/motorSincronizacion.test.ts`
Expected: FAIL — no existe `./motorSincronizacion`.

- [ ] **Step 3: Implementación mínima**

`web/src/lib/offline/motorSincronizacion.ts`:

```ts
import { ApiError } from '../http';
import type { AlmacenCola } from './almacenCola';
import type { OperacionPendiente } from './tipos';

const LOTE = 50;
const MAX_INTENTOS = 3;

interface DependenciasMotor {
  almacen: AlmacenCola;
  despachar: (op: OperacionPendiente) => Promise<void>;
  conectado: () => boolean;
  ahora?: () => Date; // inyectable para tests
}

// Motor genérico de la cola offline (spec sección 4). No conoce la API:
// el dispatcher lo aporta quien lo cablea. No bloqueante: un segundo ciclo
// simultáneo retorna de inmediato.
export function crearMotorSincronizacion(deps: DependenciasMotor): {
  sincronizar: () => Promise<void>;
} {
  const ahora = deps.ahora ?? (() => new Date());
  let enCurso = false;

  async function registrarFallo(op: OperacionPendiente): Promise<void> {
    const intentos = op.intentos + 1;
    if (intentos >= MAX_INTENTOS) {
      await deps.almacen.actualizar(op.id, { intentos, estado: 'error' });
      return;
    }
    const backoffMs = 2 ** intentos * 60_000;
    await deps.almacen.actualizar(op.id, {
      intentos,
      proximoIntentoEn: new Date(ahora().getTime() + backoffMs).toISOString(),
    });
  }

  async function sincronizar(): Promise<void> {
    if (enCurso) return;
    enCurso = true;
    try {
      const pendientes = await deps.almacen.listarPendientes(ahora().toISOString(), LOTE);
      for (const op of pendientes) {
        if (!deps.conectado()) break;
        try {
          await deps.despachar(op);
          await deps.almacen.eliminar(op.id);
        } catch (error) {
          if (error instanceof ApiError && error.status === 401) break; // sesión no renovable
          if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
            await deps.almacen.actualizar(op.id, { estado: 'error' }); // rechazo del backend
            continue;
          }
          await registrarFallo(op); // fallo de red o 5xx
        }
      }
    } finally {
      enCurso = false;
    }
  }

  return { sincronizar };
}
```

- [ ] **Step 4: Ejecutar y verificar que pasa**

Run: `cd web && npx vitest run src/lib/offline/motorSincronizacion.test.ts`
Expected: PASS (8 tests).

- [ ] **Step 5: Commit**

```bash
./verify.ps1
git add web/src/lib/offline/motorSincronizacion.ts web/src/lib/offline/motorSincronizacion.test.ts
git commit -m "feat(web): motor de sincronización offline con reintentos y backoff"
```

---

### Task 4: Coordinador singleton y dispatcher avícola

**Files:**
- Create: `web/src/app/offline/coordinador.ts`
- Test: `web/src/app/offline/coordinador.test.ts`
- Create: `web/src/features/avicola/offline.ts`
- Test: `web/src/features/avicola/offline.test.ts`

**Interfaces:**
- Consumes: `AlmacenCola` + `crearAlmacenColaMemoria` (Task 1),
  `crearAlmacenColaIndexedDb` (Task 2), `crearMotorSincronizacion` (Task 3),
  `registrarProduccion`, `registrarMortalidad`, `DatosRecogida`, `DatosBajas`
  de `web/src/features/avicola/api.ts`, `ApiError` de `web/src/lib/http.ts`.
- Produces (usado por Tasks 5, 6, 7, 8, 9):
  - Coordinador (`web/src/app/offline/coordinador.ts`):
    - `iniciarCoordinadorOffline(deps: { despachar: (op: OperacionPendiente) => Promise<void>; almacen?: AlmacenCola; intervaloMs?: number }): () => void`
      — crea el almacén (IndexedDB si no se inyecta), el motor, el listener
      `online` y el timer de respaldo (por defecto 5 min); devuelve cleanup.
    - `encolarOperacion(tipo: TipoOperacionOffline, galponId: string, cuerpo: unknown): Promise<void>`
    - `suscribirPendientes(aviso: () => void): () => void` y
      `obtenerConteoPendientes(): number` — para `useSyncExternalStore`.
    - `listarOperaciones(): Promise<OperacionPendiente[]>`
    - `reintentarOperacion(id: string): Promise<void>` — vuelve a `pendiente`
      con intentos 0 y dispara el motor.
    - `descartarOperacion(id: string): Promise<void>`
    - `suscribirAvisos(aviso: (mensaje: string) => void): () => void` — aviso
      «encolado» para el snackbar (Task 7).
  - Avícola (`web/src/features/avicola/offline.ts`):
    - `crearDespachadorAvicola(queryClient: QueryClient): (op: OperacionPendiente) => Promise<void>`
    - `guardarRecogida(galponId: string, d: DatosRecogida): Promise<boolean>`
      — true si quedó encolada.
    - `guardarBajas(galponId: string, d: DatosBajas): Promise<boolean>`

- [ ] **Step 1: Escribir el test del coordinador (falla)**

`web/src/app/offline/coordinador.test.ts`:

```ts
import { afterEach, describe, expect, test, vi } from 'vitest';
import { crearAlmacenColaMemoria } from '../../lib/offline/almacenCola';
import type { OperacionPendiente } from '../../lib/offline/tipos';
import {
  descartarOperacion,
  encolarOperacion,
  iniciarCoordinadorOffline,
  listarOperaciones,
  obtenerConteoPendientes,
  reintentarOperacion,
  suscribirAvisos,
  suscribirPendientes,
} from './coordinador';

describe('coordinador offline', () => {
  let limpiar: (() => void) | undefined;
  afterEach(() => limpiar?.());

  const arrancar = (despachar: (op: OperacionPendiente) => Promise<void>) => {
    limpiar = iniciarCoordinadorOffline({
      despachar,
      almacen: crearAlmacenColaMemoria(),
      intervaloMs: 60_000,
    });
  };

  test('encolar notifica suscriptores, avisa y dispara sync si hay red', async () => {
    const despachar = vi.fn(async () => {});
    arrancar(despachar);
    const avisoPendientes = vi.fn();
    const avisoSnackbar = vi.fn();
    suscribirPendientes(avisoPendientes);
    suscribirAvisos(avisoSnackbar);
    await encolarOperacion('produccion.crear', 'g1', { cantidadMaples: 1 });
    expect(avisoPendientes).toHaveBeenCalled();
    expect(avisoSnackbar).toHaveBeenCalledWith(
      'Guardado sin conexión: se sincronizará al volver la red.',
    );
    await vi.waitFor(() => expect(despachar).toHaveBeenCalledTimes(1));
    await vi.waitFor(() => expect(obtenerConteoPendientes()).toBe(0));
  });

  test('reintentar resetea intentos y descartar elimina', async () => {
    const despachar = vi.fn(async () => {
      throw new TypeError('sin red');
    });
    arrancar(despachar);
    await encolarOperacion('mortalidad.crear', 'g1', { cantidadMuertas: 2 });
    await vi.waitFor(async () => {
      const [op] = await listarOperaciones();
      expect(op.intentos).toBe(1);
    });
    // Sin red simulada: reintentar no re-dispara el sync y el resultado es determinista.
    const onlineSpy = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    const [encolada] = await listarOperaciones();
    await reintentarOperacion(encolada.id);
    const [op] = await listarOperaciones();
    expect(op.intentos).toBe(0);
    expect(op.estado).toBe('pendiente');
    await descartarOperacion(op.id);
    expect(obtenerConteoPendientes()).toBe(0);
    onlineSpy.mockRestore();
  });
});
```

Nota para el implementador: el test usa `vi.waitFor` porque el sync tras
encolar es fire-and-forget. `navigator.onLine` es true en jsdom, así que el
motor se dispara al encolar.

- [ ] **Step 2: Ejecutar y verificar que falla**

Run: `cd web && npx vitest run src/app/offline/coordinador.test.ts`
Expected: FAIL — no existe `./coordinador`.

- [ ] **Step 3: Implementación del coordinador**

`web/src/app/offline/coordinador.ts`:

```ts
import type { AlmacenCola } from '../../lib/offline/almacenCola';
import { crearAlmacenColaIndexedDb } from '../../lib/offline/almacenIndexedDb';
import { crearMotorSincronizacion } from '../../lib/offline/motorSincronizacion';
import type { OperacionPendiente, TipoOperacionOffline } from '../../lib/offline/tipos';

// Singleton: una cola y un motor por pestaña. Los datos son solo de negocio
// (anti-PII); el token nunca pasa por aquí.
let almacen: AlmacenCola | null = null;
let sincronizar: (() => Promise<void>) | null = null;
let conteo = 0;
const avisosPendientes = new Set<() => void>();
const avisosSnackbar = new Set<(mensaje: string) => void>();

function notificar(): void {
  avisosPendientes.forEach((a) => a());
}

async function refrescarConteo(): Promise<void> {
  if (!almacen) return;
  conteo = await almacen.contar();
  notificar();
}

export function iniciarCoordinadorOffline(deps: {
  despachar: (op: OperacionPendiente) => Promise<void>;
  almacen?: AlmacenCola;
  intervaloMs?: number;
}): () => void {
  almacen = deps.almacen ?? crearAlmacenColaIndexedDb();
  const motor = crearMotorSincronizacion({
    almacen,
    despachar: async (op) => {
      await deps.despachar(op);
    },
    conectado: () => navigator.onLine,
  });
  sincronizar = async () => {
    await motor.sincronizar();
    await refrescarConteo();
  };
  const alConectar = () => void sincronizar?.();
  window.addEventListener('online', alConectar);
  const timer = window.setInterval(alConectar, deps.intervaloMs ?? 5 * 60_000);
  void refrescarConteo();
  void sincronizar(); // ciclo inicial: vacía la cola si quedó de otra sesión
  return () => {
    window.removeEventListener('online', alConectar);
    window.clearInterval(timer);
    almacen = null;
    sincronizar = null;
    conteo = 0;
  };
}

export async function encolarOperacion(
  tipo: TipoOperacionOffline,
  galponId: string,
  cuerpo: unknown,
): Promise<void> {
  if (!almacen) throw new Error('Coordinador offline no iniciado.');
  await almacen.agregar({
    id: crypto.randomUUID(),
    tipo,
    galponId,
    cuerpo,
    estado: 'pendiente',
    intentos: 0,
    creadoEn: new Date().toISOString(),
    proximoIntentoEn: null,
  });
  await refrescarConteo();
  avisosSnackbar.forEach((a) => a('Guardado sin conexión: se sincronizará al volver la red.'));
  if (navigator.onLine) void sincronizar?.(); // fire-and-forget
}

export function suscribirPendientes(aviso: () => void): () => void {
  avisosPendientes.add(aviso);
  return () => avisosPendientes.delete(aviso);
}

export function obtenerConteoPendientes(): number {
  return conteo;
}

export function suscribirAvisos(aviso: (mensaje: string) => void): () => void {
  avisosSnackbar.add(aviso);
  return () => avisosSnackbar.delete(aviso);
}

export async function listarOperaciones(): Promise<OperacionPendiente[]> {
  return almacen ? almacen.listarTodas() : [];
}

export async function reintentarOperacion(id: string): Promise<void> {
  await almacen?.actualizar(id, { estado: 'pendiente', intentos: 0, proximoIntentoEn: null });
  await refrescarConteo();
  if (navigator.onLine) void sincronizar?.();
}

export async function descartarOperacion(id: string): Promise<void> {
  await almacen?.eliminar(id);
  await refrescarConteo();
}
```

- [ ] **Step 4: Ejecutar y verificar que pasa el test del coordinador**

Run: `cd web && npx vitest run src/app/offline/coordinador.test.ts`
Expected: PASS (2 tests).

- [ ] **Step 5: Escribir el test del dispatcher avícola (falla)**

`web/src/features/avicola/offline.test.ts`:

```ts
import { QueryClient } from '@tanstack/react-query';
import { afterEach, describe, expect, test, vi } from 'vitest';
import { ApiError } from '../../lib/http';
import { iniciarCoordinadorOffline, listarOperaciones } from '../../app/offline/coordinador';
import { crearAlmacenColaMemoria } from '../../lib/offline/almacenCola';
import { crearDespachadorAvicola, guardarBajas, guardarRecogida } from './offline';

const recogida = {
  hora: '10:30',
  cantidadMaples: 1,
  unidadesIncompletas: 2,
  maplesDescarte: 0,
  unidadesDescarte: 0,
  idempotencyKey: 'k1',
};
const bajas = { hora: '06:15', cantidadMuertas: 2, idempotencyKey: 'k2' };

describe('offline avícola', () => {
  let limpiar: (() => void) | undefined;
  afterEach(() => {
    limpiar?.();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  test('guardarRecogida online envía directo y no encola', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(JSON.stringify({ id: 'p' }), { status: 201 })),
    );
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
    });
    expect(await guardarRecogida('g1', recogida)).toBe(false);
    expect(await listarOperaciones()).toEqual([]);
  });

  test('fallo de red encola; 4xx no encola y propaga', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('fetch failed');
      }),
    );
    // El despachador rechaza (sin red): el sync automático tras encolar falla y
    // la operación permanece en la cola, que es lo que verifica la aserción.
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {
        throw new TypeError('sin red');
      }),
      almacen: crearAlmacenColaMemoria(),
    });
    expect(await guardarBajas('g1', bajas)).toBe(true);
    expect((await listarOperaciones()).length).toBe(1);

    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(JSON.stringify({ title: 'Validacion' }), {
            status: 422,
            headers: { 'content-type': 'application/json' },
          }),
      ),
    );
    await expect(guardarRecogida('g1', recogida)).rejects.toBeInstanceOf(ApiError);
    expect((await listarOperaciones()).length).toBe(1); // no encoló el 4xx
  });

  test('offline encola sin llamar a la API', async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    const onlineSpy = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
    });
    expect(await guardarRecogida('g1', recogida)).toBe(true);
    expect(fetchMock).not.toHaveBeenCalled();
    onlineSpy.mockRestore();
  });

  test('despachador llama al endpoint correcto e invalida queries', async () => {
    const fetchMock = vi.fn(
      async () => new Response(JSON.stringify({ id: 'p' }), { status: 201 }),
    );
    vi.stubGlobal('fetch', fetchMock);
    const qc = new QueryClient();
    const invalidar = vi.spyOn(qc, 'invalidateQueries');
    const despachar = crearDespachadorAvicola(qc);
    await despachar({
      id: 'op1',
      tipo: 'produccion.crear',
      galponId: 'g1',
      cuerpo: recogida,
      estado: 'pendiente',
      intentos: 0,
      creadoEn: '2026-08-29T10:00:00.000Z',
      proximoIntentoEn: null,
    });
    const req = fetchMock.mock.calls.at(0)?.[0] as unknown as Request;
    expect(req.url).toContain('/api/galpones/g1/produccion');
    expect(invalidar).toHaveBeenCalledWith({ queryKey: ['avicola'] });
  });
});
```

- [ ] **Step 6: Ejecutar y verificar que falla**

Run: `cd web && npx vitest run src/features/avicola/offline.test.ts`
Expected: FAIL — no existe `./offline`.

- [ ] **Step 7: Implementación del dispatcher y helpers**

`web/src/features/avicola/offline.ts`:

```ts
import type { QueryClient } from '@tanstack/react-query';
import { encolarOperacion } from '../../app/offline/coordinador';
import { ApiError } from '../../lib/http';
import type { OperacionPendiente } from '../../lib/offline/tipos';
import {
  registrarMortalidad,
  registrarProduccion,
  type DatosBajas,
  type DatosRecogida,
} from './api';

// Mapea la operación encolada a su endpoint y refresca la UI al sincronizar.
export function crearDespachadorAvicola(
  queryClient: QueryClient,
): (op: OperacionPendiente) => Promise<void> {
  return async (op) => {
    if (op.tipo === 'produccion.crear') {
      await registrarProduccion(op.galponId, op.cuerpo as DatosRecogida);
    } else {
      await registrarMortalidad(op.galponId, op.cuerpo as DatosBajas);
    }
    await queryClient.invalidateQueries({ queryKey: ['avicola'] });
  };
}

// Criterio del spec: encolar solo ante fallo de transporte. Un ApiError
// (4xx/5xx) es un rechazo del backend y se propaga al diálogo.
async function conCola(
  tipo: 'produccion.crear' | 'mortalidad.crear',
  galponId: string,
  cuerpo: unknown,
  enviar: () => Promise<unknown>,
): Promise<boolean> {
  if (navigator.onLine) {
    try {
      await enviar();
      return false;
    } catch (error) {
      if (error instanceof ApiError) throw error;
    }
  }
  await encolarOperacion(tipo, galponId, cuerpo);
  return true;
}

export const guardarRecogida = (galponId: string, d: DatosRecogida): Promise<boolean> =>
  conCola('produccion.crear', galponId, d, () => registrarProduccion(galponId, d));

export const guardarBajas = (galponId: string, d: DatosBajas): Promise<boolean> =>
  conCola('mortalidad.crear', galponId, d, () => registrarMortalidad(galponId, d));
```

- [ ] **Step 8: Ejecutar y verificar que pasa**

Run: `cd web && npx vitest run src/features/avicola/offline.test.ts`
Expected: PASS (4 tests).

- [ ] **Step 9: Commit**

```bash
./verify.ps1
git add web/src/app/offline/ web/src/features/avicola/offline.ts web/src/features/avicola/offline.test.ts
git commit -m "feat(web): coordinador offline y dispatcher avícola con cola ante fallo de red"
```

---
### Task 5: Diálogos de alta guardan offline

**Files:**
- Modify: `web/src/features/avicola/RegistrarRecogidaDialog.tsx`
- Modify: `web/src/features/avicola/RegistrarBajasDialog.tsx`
- Modify: `web/src/features/avicola/RegistrarBajasDialog.test.tsx`
- Test (nuevo): `web/src/features/avicola/RegistrarRecogidaDialog.test.tsx`

**Interfaces:**
- Consumes: `guardarRecogida`, `guardarBajas` (Task 4),
  `iniciarCoordinadorOffline` (Task 4, para los tests), `crearAlmacenColaMemoria`
  (Task 1).
- Produces: ninguna nueva; los diálogos quedan como consumidores finales.

- [ ] **Step 1: Escribir los tests que fallan**

Nuevo `web/src/features/avicola/RegistrarRecogidaDialog.test.tsx`:

```tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, describe, expect, test, vi } from 'vitest';
import { crearAlmacenColaMemoria } from '../../lib/offline/almacenCola';
import {
  iniciarCoordinadorOffline,
  listarOperaciones,
} from '../../app/offline/coordinador';
import { RegistrarRecogidaDialog } from './RegistrarRecogidaDialog';

const envolver = (ui: React.ReactElement) => (
  <QueryClientProvider client={new QueryClient()}>{ui}</QueryClientProvider>
);

describe('RegistrarRecogidaDialog offline', () => {
  let limpiar: (() => void) | undefined;
  afterEach(() => {
    limpiar?.();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  test('guardar habilitado sin conexión y encola', async () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
    });
    const alCerrar = vi.fn();
    render(envolver(<RegistrarRecogidaDialog galponId="g1" abierto alCerrar={alCerrar} />));
    await userEvent.type(screen.getByLabelText('Maples'), '3');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    await waitFor(() => expect(alCerrar).toHaveBeenCalled());
    expect(fetchMock).not.toHaveBeenCalled();
    const ops = await listarOperaciones();
    expect(ops.length).toBe(1);
    expect(ops[0].tipo).toBe('produccion.crear');
  });

  test('fallo de red durante el guardado encola y cierra', async () => {
    // El despachador rechaza: con navigator.onLine=true (default en jsdom) el
    // sync automático disparado por encolarOperacion falla y la operación
    // permanece en la cola, que es justo lo que el test verifica.
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {
        throw new TypeError('sin red');
      }),
      almacen: crearAlmacenColaMemoria(),
    });
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('fetch failed');
      }),
    );
    const alCerrar = vi.fn();
    render(envolver(<RegistrarRecogidaDialog galponId="g1" abierto alCerrar={alCerrar} />));
    await userEvent.type(screen.getByLabelText('Maples'), '3');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    await waitFor(() => expect(alCerrar).toHaveBeenCalled());
    expect((await listarOperaciones()).length).toBe(1);
  });
});
```

Añadir a `web/src/features/avicola/RegistrarBajasDialog.test.tsx` (mantener
intactos los tests existentes del archivo):

```tsx
import { crearAlmacenColaMemoria } from '../../lib/offline/almacenCola';
import {
  iniciarCoordinadorOffline,
  listarOperaciones,
} from '../../app/offline/coordinador';

test('guardar habilitado sin conexión y encola la baja', async () => {
  vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
  const fetchMock = vi.fn();
  vi.stubGlobal('fetch', fetchMock);
  const limpiar = iniciarCoordinadorOffline({
    despachar: vi.fn(async () => {}),
    almacen: crearAlmacenColaMemoria(),
  });
  try {
    const alCerrar = vi.fn();
    render(
      <QueryClientProvider client={new QueryClient()}>
        <RegistrarBajasDialog galponId="g1" abierto alCerrar={alCerrar} />
      </QueryClientProvider>,
    );
    await userEvent.type(screen.getByLabelText('Gallinas muertas'), '2');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    await waitFor(() => expect(alCerrar).toHaveBeenCalled());
    expect(fetchMock).not.toHaveBeenCalled();
    const ops = await listarOperaciones();
    expect(ops.length).toBe(1);
    expect(ops[0].tipo).toBe('mortalidad.crear');
  } finally {
    limpiar();
  }
});
```

Nota: si el archivo de test existente ya envuelve con `QueryClientProvider` y
usa `userEvent`, reutilizar sus imports y helpers; lo que se añade es el test
de arriba.

- [ ] **Step 2: Ejecutar y verificar que falla**

Run: `cd web && npx vitest run src/features/avicola/RegistrarRecogidaDialog.test.tsx`
Expected: FAIL — Guardar está deshabilitado offline (`disabled={!online || ...}`).

- [ ] **Step 3: Implementación mínima**

En `RegistrarRecogidaDialog.tsx`:

```tsx
// import: reemplazar registrarProduccion por guardarRecogida
import { guardarRecogida } from './offline';
// eliminar: import { useConexion } from '../../app/useConexion'; y const online = useConexion();

const guardar = useMutation({
  mutationFn: () => {
    const d: DatosRecogida = {
      hora,
      cantidadMaples: Number(maples) || 0,
      unidadesIncompletas: Number(sueltos) || 0,
      maplesDescarte: Number(descarteMaples) || 0,
      unidadesDescarte: Number(descarteSueltos) || 0,
      idempotencyKey: crypto.randomUUID(),
    };
    return guardarRecogida(galponId, d); // true si quedó encolada
  },
  onSuccess: (encolada) => {
    if (!encolada) void qc.invalidateQueries({ queryKey: ['avicola'] });
    alCerrar(); // si encoló, el coordinador muestra el aviso «Guardado sin conexión»
  },
});
// Botón: disabled={guardar.isPending} (sin !online)
```

En `RegistrarBajasDialog.tsx`: mismo cambio con `guardarBajas`:

```tsx
import { guardarBajas } from './offline';
// eliminar useConexion

const guardar = useMutation({
  mutationFn: (datos: DatosFormulario) =>
    guardarBajas(galponId, { ...datos, idempotencyKey: crypto.randomUUID() }),
  onSuccess: (encolada) => {
    if (!encolada) {
      void queryClient.invalidateQueries({ queryKey: ['avicola', 'mortalidad'] });
      void queryClient.invalidateQueries({ queryKey: ['avicola', 'galpon'] });
      void queryClient.invalidateQueries({ queryKey: ['avicola', 'eficiencia'] });
    }
    alCerrar();
  },
});
// Botón: disabled={guardar.isPending}
```

- [ ] **Step 4: Ejecutar y verificar que pasan**

Run: `cd web && npx vitest run src/features/avicola/RegistrarRecogidaDialog.test.tsx src/features/avicola/RegistrarBajasDialog.test.tsx`
Expected: PASS (nuevos + existentes).

- [ ] **Step 5: Commit**

```bash
./verify.ps1
git add web/src/features/avicola/RegistrarRecogidaDialog.tsx web/src/features/avicola/RegistrarRecogidaDialog.test.tsx web/src/features/avicola/RegistrarBajasDialog.tsx web/src/features/avicola/RegistrarBajasDialog.test.tsx
git commit -m "feat(web): alta de recogida y bajas encola sin conexión"
```

---

### Task 6: Banner sin conexión con nuevo texto y contador

**Files:**
- Create: `web/src/app/offline/usePendientesOffline.ts`
- Modify: `web/src/app/BannerSinConexion.tsx`
- Modify: `web/src/app/BannerSinConexion.test.tsx`

**Interfaces:**
- Consumes: `suscribirPendientes`, `obtenerConteoPendientes` (Task 4),
  `useConexion` (existente), `iniciarCoordinadorOffline` (tests).
- Produces (usado por Task 7): `usePendientesOffline(): number`.

- [ ] **Step 1: Escribir el test que falla**

Añadir a `web/src/app/BannerSinConexion.test.tsx` (los dos tests existentes se
mantienen: el nuevo texto sigue conteniendo «Sin conexión»):

```tsx
import { iniciarCoordinadorOffline, encolarOperacion } from './offline/coordinador';
import { crearAlmacenColaMemoria } from '../lib/offline/almacenCola';

test('offline muestra el nuevo texto y el contador de pendientes', async () => {
  // despachar rechaza: la operación queda en la cola y el contador se mantiene en 1.
  const limpiar = iniciarCoordinadorOffline({
    despachar: async () => {
      throw new TypeError('sin red');
    },
    almacen: crearAlmacenColaMemoria(),
    intervaloMs: 60_000,
  });
  try {
    render(<BannerSinConexion />);
    act(() => window.dispatchEvent(new Event('offline')));
    expect(
      screen.getByText(/los registros se guardan en este dispositivo/i),
    ).toBeInTheDocument();
    await encolarOperacion('produccion.crear', 'g1', {});
    expect(await screen.findByText(/1 registro pendiente/i)).toBeInTheDocument();
  } finally {
    limpiar();
    act(() => window.dispatchEvent(new Event('online')));
  }
});
```

- [ ] **Step 2: Ejecutar y verificar que falla**

Run: `cd web && npx vitest run src/app/BannerSinConexion.test.tsx`
Expected: FAIL — el texto nuevo no existe.

- [ ] **Step 3: Implementación mínima**

`web/src/app/offline/usePendientesOffline.ts`:

```ts
import { useSyncExternalStore } from 'react';
import { obtenerConteoPendientes, suscribirPendientes } from './coordinador';

// Número de operaciones en la cola offline (pendientes + en error).
export function usePendientesOffline(): number {
  return useSyncExternalStore(suscribirPendientes, obtenerConteoPendientes, () => 0);
}
```

`web/src/app/BannerSinConexion.tsx`:

```tsx
import { Alert } from '@mui/material';
import { useConexion } from './useConexion';
import { usePendientesOffline } from './offline/usePendientesOffline';

export function BannerSinConexion() {
  const online = useConexion();
  const pendientes = usePendientesOffline();
  if (online) return null;
  const conteo =
    pendientes === 0
      ? ''
      : pendientes === 1
        ? ' 1 registro pendiente de sincronizar.'
        : ` ${pendientes} registros pendientes de sincronizar.`;
  return (
    <Alert severity="warning" sx={{ borderRadius: 0 }}>
      Sin conexión: los registros se guardan en este dispositivo y se sincronizarán al volver la
      red.{conteo}
    </Alert>
  );
}
```

- [ ] **Step 4: Ejecutar y verificar que pasa**

Run: `cd web && npx vitest run src/app/BannerSinConexion.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
./verify.ps1
git add web/src/app/offline/usePendientesOffline.ts web/src/app/BannerSinConexion.tsx web/src/app/BannerSinConexion.test.tsx
git commit -m "feat(web): banner sin conexión informa guardado local y pendientes"
```

---

### Task 7: Chip de pendientes con reintento/descarte y snackbar de aviso

**Files:**
- Create: `web/src/app/offline/PendientesOffline.tsx`
- Test: `web/src/app/offline/PendientesOffline.test.tsx`
- Modify: `web/src/app/AppLayout.tsx` (añadir `<PendientesOffline />` en el
  `Toolbar`, entre `<SelectorTema />` y el botón «Cerrar sesión»)

**Interfaces:**
- Consumes: `usePendientesOffline` (Task 6), `listarOperaciones`,
  `reintentarOperacion`, `descartarOperacion`, `suscribirAvisos` (Task 4),
  `DialogoConfirmacion` de `../ui/DialogoConfirmacion` (existente: props
  `abierto`, `titulo`, `mensaje`, `etiquetaConfirmar`, `color`, `pendiente`,
  `onCancelar`, `onConfirmar`).
- Produces: componente `<PendientesOffline />` sin props.

- [ ] **Step 1: Escribir el test que falla**

`web/src/app/offline/PendientesOffline.test.tsx`:

```tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, test, vi } from 'vitest';
import { crearAlmacenColaMemoria } from '../../lib/offline/almacenCola';
import {
  descartarOperacion,
  encolarOperacion,
  iniciarCoordinadorOffline,
  listarOperaciones,
} from './coordinador';
import { PendientesOffline } from './PendientesOffline';

describe('PendientesOffline', () => {
  let limpiar: (() => void) | undefined;
  afterEach(() => limpiar?.());

  test('sin operaciones no muestra el chip', () => {
    limpiar = iniciarCoordinadorOffline({
      despachar: async () => {},
      almacen: crearAlmacenColaMemoria(),
      intervaloMs: 60_000,
    });
    render(<PendientesOffline />);
    expect(screen.queryByRole('button', { name: /pendiente/i })).not.toBeInTheDocument();
  });

  test('muestra el contador, lista y permite descartar', async () => {
    limpiar = iniciarCoordinadorOffline({
      despachar: async () => {
        throw new TypeError('sin red');
      },
      almacen: crearAlmacenColaMemoria(),
      intervaloMs: 60_000,
    });
    render(<PendientesOffline />);
    await encolarOperacion('produccion.crear', 'g1', {});
    expect(await screen.findByRole('button', { name: /1 pendiente/i })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /1 pendiente/i }));
    expect(await screen.findByText('Recogida')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Descartar' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Confirmar' }));
    await waitFor(async () => expect(await listarOperaciones()).toEqual([]));
  });

  test('muestra snackbar al encolar', async () => {
    limpiar = iniciarCoordinadorOffline({
      despachar: async () => {},
      almacen: crearAlmacenColaMemoria(),
      intervaloMs: 60_000,
    });
    render(<PendientesOffline />);
    await encolarOperacion('mortalidad.crear', 'g1', {});
    expect(await screen.findByText(/Guardado sin conexión/)).toBeInTheDocument();
  });
});
```

Nota: con `despachar` que rechaza, la operación queda en la cola (backoff de
2 min), así que el chip sigue visible para el segundo test.

- [ ] **Step 2: Ejecutar y verificar que falla**

Run: `cd web && npx vitest run src/app/offline/PendientesOffline.test.tsx`
Expected: FAIL — no existe `./PendientesOffline`.

- [ ] **Step 3: Implementación mínima**

`web/src/app/offline/PendientesOffline.tsx`:

```tsx
import CloudUploadRoundedIcon from '@mui/icons-material/CloudUploadRounded';
import {
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Button,
  List,
  ListItem,
  ListItemText,
  Snackbar,
} from '@mui/material';
import { useEffect, useState } from 'react';
import type { OperacionPendiente } from '../../lib/offline/tipos';
import { DialogoConfirmacion } from '../ui/DialogoConfirmacion';
import {
  descartarOperacion,
  listarOperaciones,
  reintentarOperacion,
  suscribirAvisos,
} from './coordinador';
import { usePendientesOffline } from './usePendientesOffline';

const tituloTipo = (op: OperacionPendiente) =>
  op.tipo === 'produccion.crear' ? 'Recogida' : 'Bajas';

export function PendientesOffline() {
  const pendientes = usePendientesOffline();
  const [abierto, setAbierto] = useState(false);
  const [operaciones, setOperaciones] = useState<OperacionPendiente[]>([]);
  const [aDescartar, setADescartar] = useState<OperacionPendiente | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);

  useEffect(() => suscribirAvisos(setAviso), []);

  const abrir = async () => {
    setOperaciones(await listarOperaciones());
    setAbierto(true);
  };
  const refrescar = async () => setOperaciones(await listarOperaciones());

  return (
    <>
      {pendientes > 0 && (
        <Chip
          icon={<CloudUploadRoundedIcon />}
          color="warning"
          size="small"
          component="button"
          onClick={() => void abrir()}
          aria-label={pendientes === 1 ? '1 pendiente de sincronizar' : `${pendientes} pendientes de sincronizar`}
          label={pendientes === 1 ? '1 pendiente' : `${pendientes} pendientes`}
        />
      )}
      <Dialog open={abierto} onClose={() => setAbierto(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Registros pendientes de sincronizar</DialogTitle>
        <DialogContent>
          <List>
            {operaciones.map((op) => (
              <ListItem
                key={op.id}
                secondaryAction={
                  <>
                    {op.estado === 'error' && (
                      <Button
                        size="small"
                        onClick={() => void reintentarOperacion(op.id).then(refrescar)}
                      >
                        Reintentar
                      </Button>
                    )}
                    <Button size="small" color="error" onClick={() => setADescartar(op)}>
                      Descartar
                    </Button>
                  </>
                }
              >
                <ListItemText
                  primary={tituloTipo(op)}
                  secondary={`${new Date(op.creadoEn).toLocaleString()} · ${
                    op.estado === 'error' ? 'Error al sincronizar' : 'Pendiente'
                  }`}
                />
              </ListItem>
            ))}
          </List>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAbierto(false)}>Cerrar</Button>
        </DialogActions>
      </Dialog>
      <DialogoConfirmacion
        abierto={aDescartar !== null}
        titulo="Descartar registro"
        mensaje="El registro no se sincronizará y se perderá. ¿Continuar?"
        etiquetaConfirmar="Confirmar"
        color="error"
        pendiente={false}
        onCancelar={() => setADescartar(null)}
        onConfirmar={() => {
          if (aDescartar) void descartarOperacion(aDescartar.id).then(refrescar);
          setADescartar(null);
        }}
      />
      <Snackbar
        open={aviso !== null}
        autoHideDuration={4000}
        onClose={() => setAviso(null)}
        message={aviso ?? ''}
      />
    </>
  );
}
```

En `AppLayout.tsx`: importar `PendientesOffline` de
`./offline/PendientesOffline` y renderizar `<PendientesOffline />` en el
`Toolbar` justo después de `<SelectorTema />`.

- [ ] **Step 4: Ejecutar y verificar que pasa**

Run: `cd web && npx vitest run src/app/offline/PendientesOffline.test.tsx src/app/AppLayout.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
./verify.ps1
git add web/src/app/offline/PendientesOffline.tsx web/src/app/offline/PendientesOffline.test.tsx web/src/app/AppLayout.tsx
git commit -m "feat(web): indicador de pendientes offline con reintento y descarte"
```

---
### Task 8: Caché de lectura (granjas, galpones, producción y mortalidad del día)

**Files:**
- Create: `web/src/lib/offline/cacheLectura.ts` (interfaz + memoria)
- Create: `web/src/lib/offline/cacheLecturaIndexedDb.ts`
- Test: `web/src/lib/offline/cacheLectura.test.ts`
- Create: `web/src/features/avicola/cacheAvicola.ts` (contiene `conCacheLectura`)
- Modify: `web/src/app/offline/coordinador.ts` (añade `obtenerCacheLectura` y el
  parámetro opcional `cache` en `iniciarCoordinadorOffline`)
- Modify: `web/src/features/avicola/api.ts` (envuelve `listarGranjas`,
  `listarGalpones`, `obtenerGalpon`, `listarProduccion`, `listarMortalidad`)
- Test: `web/src/features/avicola/offline.test.ts` (añade tests de caché)

> `conCacheLectura` vive en `cacheAvicola.ts`, NO en `offline.ts`: como
> `api.ts` llama al wrapper y `offline.ts` ya importa funciones de `api.ts`,
> meterlo en `offline.ts` crearía un ciclo de módulos (api → offline → api).
> Con `cacheAvicola.ts` el grafo es acíclico: `api → cacheAvicola → coordinador`,
> y `offline → api, coordinador`.

**Interfaces:**
- Consumes: `abrirBaseDatosOffline`, `promesaDePedido` (Task 2), coordinador
  (Task 4), `peticion`/`ApiError` de `lib/http`.
- Produces:
  - `CacheLectura { obtener(clave: string): Promise<unknown>; guardar(clave: string, valor: unknown): Promise<void> }`
  - `crearCacheLecturaMemoria(): CacheLectura`
  - `crearCacheLecturaIndexedDb(): CacheLectura`
  - Coordinador: `obtenerCacheLectura(): CacheLectura | null` (null si no se
    inició — así los tests existentes de `api.ts` no cambian).
  - `conCacheLectura<T>(clave: string, obtenerDatos: () => Promise<T>): Promise<T>`

- [ ] **Step 1: Escribir los tests que fallan**

`web/src/lib/offline/cacheLectura.test.ts`:

```ts
import 'fake-indexeddb/auto';
import { describe, expect, test } from 'vitest';
import { crearCacheLecturaMemoria } from './cacheLectura';
import { crearCacheLecturaIndexedDb } from './cacheLecturaIndexedDb';

const contrato = (nombre: string, crear: () => import('./cacheLectura').CacheLectura) =>
  describe(nombre, () => {
    test('guarda y recupera; clave ausente da undefined', async () => {
      const c = crear();
      expect(await c.obtener('nada')).toBeUndefined();
      await c.guardar('granjas', [{ id: 'g1' }]);
      expect(await c.obtener('granjas')).toEqual([{ id: 'g1' }]);
      await c.guardar('granjas', [{ id: 'g2' }]); // sobrescribe
      expect(await c.obtener('granjas')).toEqual([{ id: 'g2' }]);
    });
  });

contrato('memoria', crearCacheLecturaMemoria);
contrato('indexeddb', crearCacheLecturaIndexedDb);
```

Añadir a `web/src/features/avicola/offline.test.ts`:

```ts
import { listarGranjas } from './api';
import { crearCacheLecturaMemoria } from '../../lib/offline/cacheLectura';

test('listarGranjas sirve desde caché cuando falla la red', async () => {
  const cache = crearCacheLecturaMemoria();
  limpiar = iniciarCoordinadorOffline({
    despachar: vi.fn(async () => {}),
    almacen: crearAlmacenColaMemoria(),
    cache,
  });
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => new Response(JSON.stringify([{ id: 'g1' }]), { status: 200 })),
  );
  expect(await listarGranjas()).toEqual([{ id: 'g1' }]); // llena la caché
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => {
      throw new TypeError('fetch failed');
    }),
  );
  expect(await listarGranjas()).toEqual([{ id: 'g1' }]); // sirve la caché
});
```

- [ ] **Step 2: Ejecutar y verificar que falla**

Run: `cd web && npx vitest run src/lib/offline/cacheLectura.test.ts src/features/avicola/offline.test.ts`
Expected: FAIL — no existen `cacheLectura` ni `conCacheLectura`.

- [ ] **Step 3: Implementación mínima**

`web/src/lib/offline/cacheLectura.ts`:

```ts
// Caché de lectura offline: solo datos de negocio (anti-PII), nunca tokens.
export interface CacheLectura {
  obtener(clave: string): Promise<unknown>;
  guardar(clave: string, valor: unknown): Promise<void>;
}

export function crearCacheLecturaMemoria(): CacheLectura {
  const datos = new Map<string, unknown>();
  return {
    async obtener(clave) {
      return datos.get(clave);
    },
    async guardar(clave, valor) {
      datos.set(clave, valor);
    },
  };
}
```

`web/src/lib/offline/cacheLecturaIndexedDb.ts`:

```ts
import { abrirBaseDatosOffline, promesaDePedido } from './baseDatosOffline';
import type { CacheLectura } from './cacheLectura';

interface EntradaCache {
  clave: string;
  valor: unknown;
}

export function crearCacheLecturaIndexedDb(): CacheLectura {
  return {
    async obtener(clave) {
      const bd = await abrirBaseDatosOffline();
      try {
        const tx = bd.transaction('cache-lectura', 'readonly');
        const entrada = await promesaDePedido(
          tx.objectStore('cache-lectura').get(clave) as IDBRequest<EntradaCache | undefined>,
        );
        return entrada?.valor;
      } finally {
        bd.close();
      }
    },
    async guardar(clave, valor) {
      const bd = await abrirBaseDatosOffline();
      try {
        const tx = bd.transaction('cache-lectura', 'readwrite');
        await promesaDePedido(tx.objectStore('cache-lectura').put({ clave, valor }));
      } finally {
        bd.close();
      }
    },
  };
}
```

En `coordinador.ts` añadir (junto a las variables del singleton):

```ts
import type { CacheLectura } from '../../lib/offline/cacheLectura';
import { crearCacheLecturaIndexedDb } from '../../lib/offline/cacheLecturaIndexedDb';

let cache: CacheLectura | null = null;
// en iniciarCoordinadorOffline, parámetro nuevo `cache?: CacheLectura`:
//   cache = deps.cache ?? crearCacheLecturaIndexedDb();
// y en el cleanup: cache = null;
export function obtenerCacheLectura(): CacheLectura | null {
  return cache;
}
```

`web/src/features/avicola/cacheAvicola.ts` (nuevo; importa del coordinador y de
lib/http, no de `api.ts`, para evitar el ciclo):

```ts
import { obtenerCacheLectura } from '../../app/offline/coordinador';
import { ApiError } from '../../lib/http';

// Lectura con respaldo offline: éxito → actualiza la caché; fallo de red →
// sirve la caché si existe. ApiError (4xx/5xx) siempre se propaga.
export async function conCacheLectura<T>(
  clave: string,
  obtenerDatos: () => Promise<T>,
): Promise<T> {
  const cache = obtenerCacheLectura();
  if (!cache) return obtenerDatos();
  try {
    const valor = await obtenerDatos();
    await cache.guardar(clave, valor);
    return valor;
  } catch (error) {
    if (error instanceof ApiError) throw error;
    const cacheado = await cache.obtener(clave);
    if (cacheado !== undefined) return cacheado as T;
    throw error;
  }
}
```

En `web/src/features/avicola/api.ts` envolver las cinco lecturas (importar
`conCacheLectura` de `./cacheAvicola`). La clave incluye los parámetros: las
llamadas con `fecha` explícita (consultas de otros días) usan su propia clave y
también quedan cubiertas tras la primera visita:

```ts
export const listarGranjas = () =>
  conCacheLectura('granjas', () => peticion<Granja[]>({ ruta: '/granjas' }));
export const listarGalpones = (id: string) =>
  conCacheLectura(`granjas/${id}/galpones`, () =>
    peticion<Galpon[]>({ ruta: `/granjas/${id}/galpones` }),
  );
export const obtenerGalpon = (id: string) =>
  conCacheLectura(`galpones/${id}`, () => peticion<Galpon>({ ruta: `/galpones/${id}` }));
export const listarProduccion = (id: string, fecha?: string) =>
  conCacheLectura(`galpones/${id}/produccion/${fecha ?? 'hoy'}`, () =>
    peticion<ProduccionDia>({ ruta: `/galpones/${id}/produccion${fecha ? `?fecha=${fecha}` : ''}` }),
  );
export const listarMortalidad = (id: string, fecha?: string) =>
  conCacheLectura(`galpones/${id}/mortalidad/${fecha ?? 'hoy'}`, () =>
    peticion<MortalidadDia>({ ruta: `/galpones/${id}/mortalidad${fecha ? `?fecha=${fecha}` : ''}` }),
  );
```

Ojo: «hoy» como clave asume que la app no cruza la medianoche abierta sin red;
el precalentado (Task 9) y la navegación normal la refrescan en cada sesión con
red. No inventar una clave con la fecha real del cliente: la ruta sin `fecha`
ya significa «día actual» para el backend.

- [ ] **Step 4: Ejecutar y verificar que pasan (nuevos y existentes)**

Run: `cd web && npx vitest run src/lib/offline/cacheLectura.test.ts src/features/avicola/offline.test.ts src/features/avicola/api.test.ts`
Expected: PASS — los tests existentes de `api.test.ts` siguen verdes porque sin
coordinador iniciado la caché es null (passthrough).

- [ ] **Step 5: Commit**

```bash
./verify.ps1
git add web/src/lib/offline/cacheLectura.ts web/src/lib/offline/cacheLecturaIndexedDb.ts web/src/lib/offline/cacheLectura.test.ts web/src/app/offline/coordinador.ts web/src/features/avicola/cacheAvicola.ts web/src/features/avicola/api.ts web/src/features/avicola/offline.test.ts
git commit -m "feat(web): caché de lectura offline para granjas y galpones"
```

---

### Task 9: Precalentado de caché tras el login del trabajador

**Files:**
- Modify: `web/src/features/avicola/offline.ts` (añade `precalentarCacheAvicola`)
- Create: `web/src/app/offline/PrecalentadoOffline.tsx` (efecto por rol)
- Test: `web/src/app/offline/PrecalentadoOffline.test.tsx`
- Modify: `web/src/app/AppLayout.tsx` (monta `<PrecalentadoOffline />`)

**Interfaces:**
- Consumes: `listarGranjas`, `listarGalpones`, `obtenerGalpon`,
  `listarProduccion`, `listarMortalidad` ya envueltas por `conCacheLectura`
  (Task 8); `useAuth` de `../../features/auth/AuthContext`; `useConexion`.
- Produces: `precalentarCacheAvicola(): Promise<void>` y el componente
  `<PrecalentadoOffline />` (sin props, no renderiza nada).

- [ ] **Step 1: Escribir el test que falla**

`web/src/app/offline/PrecalentadoOffline.test.tsx`:

```tsx
import { render, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, test, vi } from 'vitest';
import { crearAlmacenColaMemoria } from '../../lib/offline/almacenCola';
import { crearCacheLecturaMemoria } from '../../lib/offline/cacheLectura';
import { iniciarCoordinadorOffline } from './coordinador';
import { PrecalentadoOffline } from './PrecalentadoOffline';

// AuthContext real es pesado para este test: se mockea useAuth.
const authMock = vi.fn();
vi.mock('../../features/auth/AuthContext', () => ({
  useAuth: () => authMock(),
}));

const respuesta = (cuerpo: unknown) =>
  new Response(JSON.stringify(cuerpo), {
    status: 200,
    headers: { 'content-type': 'application/json' },
  });

describe('PrecalentadoOffline', () => {
  let limpiar: (() => void) | undefined;
  afterEach(() => {
    limpiar?.();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  test('con rol Trabajador descarga y cachea los datos del día', async () => {
    const cache = crearCacheLecturaMemoria();
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
      cache,
      intervaloMs: 60_000,
    });
    authMock.mockReturnValue({ rol: 'Trabajador', estaAutenticado: true });
    const fetchMock = vi.fn(async (input: Request) => {
      const url = typeof input === 'string' ? input : input.url;
      if (url.includes('/galpones/g1/produccion')) return respuesta({ recogidas: [] });
      if (url.includes('/galpones/g1/mortalidad')) return respuesta({ registros: [] });
      if (url.endsWith('/galpones/g1')) return respuesta({ id: 'g1' });
      if (url.includes('/granjas/f1/galpones')) return respuesta([{ id: 'g1' }]);
      return respuesta([{ id: 'f1' }]); // /granjas
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<PrecalentadoOffline />);
    await waitFor(async () =>
      expect(await cache.obtener('galpones/g1/produccion/hoy')).toBeDefined(),
    );
    expect(await cache.obtener('granjas')).toBeDefined();
    expect(await cache.obtener('granjas/f1/galpones')).toBeDefined();
    expect(await cache.obtener('galpones/g1/mortalidad/hoy')).toBeDefined();
  });

  test('con otro rol no precalienta', async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
      cache: crearCacheLecturaMemoria(),
      intervaloMs: 60_000,
    });
    authMock.mockReturnValue({ rol: 'Cliente', estaAutenticado: true });
    render(<PrecalentadoOffline />);
    await new Promise((r) => setTimeout(r, 50));
    const llamadas = fetchMock.mock.calls.filter((c) =>
      String(typeof c[0] === 'string' ? c[0] : (c[0] as Request).url).includes('/granjas'),
    );
    expect(llamadas).toEqual([]);
  });
});
```

- [ ] **Step 2: Ejecutar y verificar que falla**

Run: `cd web && npx vitest run src/app/offline/PrecalentadoOffline.test.tsx`
Expected: FAIL — no existe `./PrecalentadoOffline`.

- [ ] **Step 3: Implementación mínima**

En `web/src/features/avicola/offline.ts` añadir:

```ts
import {
  listarGalpones,
  listarGranjas,
  listarMortalidad,
  listarProduccion,
  obtenerGalpon,
} from './api';

// Descarga los datos del día para operar sin red (spec decisión 5). Las
// funciones de api.ts ya escriben en la caché; aquí solo se recorren.
// Fallos individuales no abortan el precalentado.
export async function precalentarCacheAvicola(): Promise<void> {
  const granjas = await listarGranjas();
  for (const granja of granjas) {
    const galpones = await listarGalpones(granja.id);
    for (const galpon of galpones) {
      await Promise.all([
        obtenerGalpon(galpon.id).catch(() => undefined),
        listarProduccion(galpon.id).catch(() => undefined),
        listarMortalidad(galpon.id).catch(() => undefined),
      ]);
    }
  }
}
```

`web/src/app/offline/PrecalentadoOffline.tsx`:

```tsx
import { useEffect, useRef } from 'react';
import { useAuth } from '../../features/auth/AuthContext';
import { precalentarCacheAvicola } from '../../features/avicola/offline';
import { useConexion } from '../useConexion';

// Efecto sin UI: precalienta la caché del día para el rol Trabajador (spec
// decisión 5). Se reintenta en cada reconexión mientras dure la sesión.
export function PrecalentadoOffline() {
  const { rol, estaAutenticado } = useAuth();
  const online = useConexion();
  const ultimaVez = useRef<string | null>(null);
  useEffect(() => {
    if (!estaAutenticado || rol !== 'Trabajador' || !online) return;
    const hoy = new Date().toDateString();
    if (ultimaVez.current === hoy) return;
    ultimaVez.current = hoy;
    void precalentarCacheAvicola().catch(() => {
      ultimaVez.current = null; // permite reintentar si falló a medias
    });
  }, [estaAutenticado, rol, online]);
  return null;
}
```

En `AppLayout.tsx`: importar y montar `<PrecalentadoOffline />` junto a
`<BannerSinConexion />`.

- [ ] **Step 4: Ejecutar y verificar que pasa**

Run: `cd web && npx vitest run src/app/offline/PrecalentadoOffline.test.tsx src/app/AppLayout.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
./verify.ps1
git add web/src/features/avicola/offline.ts web/src/app/offline/PrecalentadoOffline.tsx web/src/app/offline/PrecalentadoOffline.test.tsx web/src/app/AppLayout.tsx
git commit -m "feat(web): precalentado de caché del día tras el login del trabajador"
```

---

### Task 10: Sesión offline del trabajador (snapshot sin token)

**Files:**
- Create: `web/src/app/offline/sesionOffline.ts`
- Test: `web/src/app/offline/sesionOffline.test.ts`
- Modify: `web/src/features/auth/AuthContext.tsx` (guardar snapshot al
  autenticar, restaurar offline, revalidar al reconectar, borrar en logout)
- Modify: `web/src/features/auth/AuthContext.test.tsx`

**Interfaces:**
- Consumes: `crearCacheLecturaIndexedDb` (Task 8) — se usa directamente, NO
  vía coordinador: el efecto de restauración de `AuthProvider` corre antes que
  el arranque del coordinador. `renovarSesion` de `../../lib/http` (devuelve
  false ante rechazo del backend, lanza ante fallo de red), `obtenerMe` de
  `./api`.
- Produces:
  - `guardarSesionOffline(usuario: UsuarioActual): Promise<void>` — solo si
    `rol === 'Trabajador'`; en caso contrario borra el snapshot.
  - `obtenerSesionOffline(): Promise<UsuarioActual | null>`
  - `borrarSesionOffline(): Promise<void>`

- [ ] **Step 1: Escribir los tests que fallan**

`web/src/app/offline/sesionOffline.test.ts`:

```ts
import 'fake-indexeddb/auto';
import { describe, expect, test } from 'vitest';
import { crearCacheLecturaIndexedDb } from '../../lib/offline/cacheLecturaIndexedDb';
import type { UsuarioActual } from '../../lib/tipos';
import {
  borrarSesionOffline,
  guardarSesionOffline,
  obtenerSesionOffline,
} from './sesionOffline';

const trabajador: UsuarioActual = {
  usuarioId: 'u1',
  correo: 'campo@example.com',
  rol: 'Trabajador',
  clienteId: 'c1',
  trabajadorId: 't1',
  modulos: ['GestionAvicola'],
  funcionalidades: ['ProduccionHuevos', 'Mortalidad'],
};

describe('sesión offline', () => {
  test('guarda snapshot del trabajador sin correo ni token', async () => {
    await guardarSesionOffline(trabajador);
    const snap = await obtenerSesionOffline();
    expect(snap?.rol).toBe('Trabajador');
    expect(snap?.funcionalidades).toEqual(['ProduccionHuevos', 'Mortalidad']);
    expect(snap?.correo).toBeNull();
    expect(JSON.stringify(snap)).not.toContain('campo@example.com');
  });

  test('guardar con otro rol borra el snapshot', async () => {
    await guardarSesionOffline(trabajador);
    await guardarSesionOffline({ ...trabajador, rol: 'Cliente', clienteId: 'c1' });
    expect(await obtenerSesionOffline()).toBeNull();
  });

  test('borrarSesionOffline lo elimina', async () => {
    await guardarSesionOffline(trabajador);
    await borrarSesionOffline();
    expect(await obtenerSesionOffline()).toBeNull();
  });

  test('snapshot con más de 12 horas no se restaura y se borra', async () => {
    // Sembrar un snapshot expirado escribiendo el wrapper crudo directamente.
    await crearCacheLecturaIndexedDb().guardar('sesion-offline', {
      guardadoEn: new Date('2026-08-28T20:00:00.000Z').toISOString(),
      usuario: { ...trabajador, correo: null },
    });
    const ahora = new Date('2026-08-29T10:00:00.000Z');
    expect(await obtenerSesionOffline(ahora)).toBeNull();
    expect(await crearCacheLecturaIndexedDb().obtener('sesion-offline')).toBeNull();
  });

  test('snapshot fresco se restaura', async () => {
    await guardarSesionOffline(trabajador);
    const snap = await obtenerSesionOffline(new Date('2026-08-29T10:00:00.000Z'));
    expect(snap?.rol).toBe('Trabajador');
  });
});
```

Añadir a `web/src/features/auth/AuthContext.test.tsx` (importar
`'fake-indexeddb/auto'` y las funciones de sesión offline):

```tsx
const snapshotTrabajador: UsuarioActual = {
  usuarioId: 'u1',
  correo: null,
  rol: 'Trabajador',
  clienteId: 'c1',
  trabajadorId: 't1',
  modulos: ['GestionAvicola'],
  funcionalidades: ['ProduccionHuevos'],
};

test('sin red restaura desde el snapshot del trabajador', async () => {
  await guardarSesionOffline(snapshotTrabajador);
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => {
      throw new TypeError('fetch failed');
    }),
  );
  // render del probe del archivo existente
  expect(await screen.findByTestId('rol')).toHaveTextContent('Trabajador');
});

test('rechazo del backend (no red) NO usa el snapshot', async () => {
  await guardarSesionOffline(snapshotTrabajador);
  vi.stubGlobal('fetch', vi.fn(async () => new Response('', { status: 401 })));
  // render del probe
  expect(await screen.findByTestId('rol')).toHaveTextContent('sin-rol');
});
```

Nota: el snapshot se guarda sin `correo`, así que la barra no mostrará correo
en sesión offline — comportamiento deseado (anti-PII). Ajustar los datos del
snapshot al helper/probe que ya use el archivo de test existente.

- [ ] **Step 2: Ejecutar y verificar que falla**

Run: `cd web && npx vitest run src/app/offline/sesionOffline.test.ts src/features/auth/AuthContext.test.tsx`
Expected: FAIL — no existe `./sesionOffline` y AuthContext no restaura offline.

- [ ] **Step 3: Implementación mínima**

`web/src/app/offline/sesionOffline.ts`:

```ts
import { crearCacheLecturaIndexedDb } from '../../lib/offline/cacheLecturaIndexedDb';
import type { UsuarioActual } from '../../lib/tipos';

// Snapshot mínimo para abrir la PWA sin red (spec decisión 6). NUNCA guarda
// token ni correo (anti-PII). Se accede a IndexedDB directamente porque la
// restauración de sesión corre antes que el coordinador offline.
const CLAVE = 'sesion-offline';
// Caducidad de 12 h (spec decisión 6): obliga a login diario, y ese login con
// red es lo que vacía la cola (ciclo inicial del motor).
const VALIDEZ_MS = 12 * 60 * 60 * 1000;

interface SnapshotGuardado {
  guardadoEn: string; // ISO
  usuario: UsuarioActual;
}

export async function guardarSesionOffline(usuario: UsuarioActual): Promise<void> {
  const cache = crearCacheLecturaIndexedDb();
  if (usuario.rol !== 'Trabajador') {
    await cache.guardar(CLAVE, null); // otro rol → borra (dispositivo compartido)
    return;
  }
  const snapshot: UsuarioActual = {
    usuarioId: usuario.usuarioId,
    correo: null,
    rol: usuario.rol,
    clienteId: usuario.clienteId,
    trabajadorId: usuario.trabajadorId,
    modulos: usuario.modulos,
    funcionalidades: usuario.funcionalidades,
  };
  const guardado: SnapshotGuardado = { guardadoEn: new Date().toISOString(), usuario: snapshot };
  await cache.guardar(CLAVE, guardado);
}

export async function obtenerSesionOffline(ahora: Date = new Date()): Promise<UsuarioActual | null> {
  const valor = await crearCacheLecturaIndexedDb().obtener(CLAVE);
  if (!valor || typeof valor !== 'object') return null;
  const { guardadoEn, usuario } = valor as SnapshotGuardado;
  if (ahora.getTime() - new Date(guardadoEn).getTime() > VALIDEZ_MS) {
    await crearCacheLecturaIndexedDb().guardar(CLAVE, null); // expirado: se borra
    return null;
  }
  return usuario;
}

export async function borrarSesionOffline(): Promise<void> {
  await crearCacheLecturaIndexedDb().guardar(CLAVE, null);
}
```

En `AuthContext.tsx`:

```tsx
import {
  borrarSesionOffline,
  guardarSesionOffline,
  obtenerSesionOffline,
} from '../../app/offline/sesionOffline';

// en el useEffect de restauración:
void (async () => {
  try {
    const restaurada = await renovarSesion();
    if (!restaurada || !activo) return; // rechazo del backend: sin fallback
    const me = await obtenerMe();
    if (activo) setUsuario(me);
    await guardarSesionOffline(me); // trabajador → snapshot; otro rol → borra
  } catch {
    // fallo de red: intentar sesión offline del trabajador
    const snapshot = await obtenerSesionOffline().catch(() => null);
    if (activo && snapshot) {
      setUsuario(snapshot);
      setEsSnapshot(true);
    }
  } finally {
    if (activo) setCargando(false);
  }
})();

// estado nuevo: const [esSnapshot, setEsSnapshot] = useState(false);

// revalidación al reconectar (efecto aparte):
useEffect(() => {
  if (!esSnapshot) return;
  const revalidar = () => {
    void (async () => {
      try {
        if (await renovarSesion()) {
          const me = await obtenerMe();
          setUsuario(me);
          setEsSnapshot(false);
          await guardarSesionOffline(me);
        }
      } catch {
        // sigue sin red real; se reintenta en el próximo evento online
      }
    })();
  };
  window.addEventListener('online', revalidar);
  return () => window.removeEventListener('online', revalidar);
}, [esSnapshot]);

// en iniciarSesionFn, tras setUsuario(me):
//   await guardarSesionOffline(me);
// en cerrarSesionFn:
//   clearAccessToken(); setUsuario(null); setEsSnapshot(false);
//   void borrarSesionOffline();
```

- [ ] **Step 4: Ejecutar y verificar que pasan**

Run: `cd web && npx vitest run src/app/offline/sesionOffline.test.ts src/features/auth/`
Expected: PASS (nuevos + existentes de auth).

- [ ] **Step 5: Commit**

```bash
./verify.ps1
git add web/src/app/offline/sesionOffline.ts web/src/app/offline/sesionOffline.test.ts web/src/features/auth/AuthContext.tsx web/src/features/auth/AuthContext.test.tsx
git commit -m "feat(web): sesión offline del trabajador sin persistir el token"
```

---

### Task 11: Cableado en providers, documentación e integración

**Files:**
- Modify: `web/src/app/providers.tsx` (arranque del coordinador)
- Modify: `web/AGENTS.md` (sección Organización: nuevos módulos offline)
- Modify: `AGENTS.md` raíz (la descripción del frontend ya no es online-only)
- Modify: `docs/superpowers/plans/2026-08-29-offline-pwa.md` (marcar checkboxes)
- Modify: `docs/ai/HANDOFF.md` (si la feature cierra, borrarlo; si no,
  actualizarlo desde la plantilla)

**Interfaces:**
- Consumes: `iniciarCoordinadorOffline` (Task 4, extendido en Task 8),
  `crearDespachadorAvicola` (Task 4), `queryClient` de `providers.tsx`.
- Produces: aplicación completa cableada.

- [ ] **Step 1: Test de humo del arranque**

Añadir a `web/src/app/offline/coordinador.test.ts`:

```ts
test('al iniciar con cola previa y red, vacía la cola (ciclo inicial)', async () => {
  const almacen = crearAlmacenColaMemoria();
  await almacen.agregar({
    id: 'previa',
    tipo: 'produccion.crear',
    galponId: 'g1',
    cuerpo: {},
    estado: 'pendiente',
    intentos: 0,
    creadoEn: '2026-08-29T09:00:00.000Z',
    proximoIntentoEn: null,
  });
  const despachar = vi.fn(async () => {});
  limpiar = iniciarCoordinadorOffline({ despachar, almacen, intervaloMs: 60_000 });
  await vi.waitFor(() => expect(despachar).toHaveBeenCalledTimes(1));
  expect(await almacen.contar()).toBe(0);
});
```

Run: `cd web && npx vitest run src/app/offline/coordinador.test.ts`
Expected: PASS ya (el ciclo inicial se implementó en Task 4). Si falla,
corregir el coordinador — no el test.

- [ ] **Step 2: Cablear en providers**

`web/src/app/providers.tsx`:

```tsx
import { useEffect, type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from '../features/auth/AuthContext';
import { crearDespachadorAvicola } from '../features/avicola/offline';
import { iniciarCoordinadorOffline } from './offline/coordinador';

const queryClient = new QueryClient();

export function AppProviders({ children }: { children: ReactNode }) {
  useEffect(() => iniciarCoordinadorOffline({ despachar: crearDespachadorAvicola(queryClient) }), []);
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>{children}</AuthProvider>
    </QueryClientProvider>
  );
}
```

- [ ] **Step 3: Actualizar la documentación de agentes**

En `web/AGENTS.md`, sección Organización, añadir:

```
- `src/lib/offline/`: cola y caché offline en IndexedDB (tipos, almacenes,
  motor de sincronización, caché de lectura). No importa de `features/` ni de
  `app/`.
- `src/app/offline/`: coordinador singleton que cablea almacén + motor +
  dispatcher, hook de pendientes, snapshot de sesión offline del trabajador
  (sin token ni correo), precalentado de caché y UI (chip, diálogo, snackbar).
```

En el `AGENTS.md` raíz, en la descripción del frontend, cambiar
«online-first» por «offline-first para recogida y mortalidad en el rol
Trabajador (cola IndexedDB, precalentado del día y sincronización automática)».

- [ ] **Step 4: Verificación completa**

```bash
cd web && npm run lint && npm run format:check && npm run test && npm run build
cd .. && ./verify.ps1
```

Expected: todo en verde. La puerta exige Docker corriendo (tests de
integración del backend con Testcontainers).

- [ ] **Step 5: Cierre del ciclo**

- Marcar `- [x]` todos los checkboxes de este plan.
- Si la feature queda completa y verificada: borrar `docs/ai/HANDOFF.md`
  (está en `.gitignore`; la sesión de análisis cerró). Si queda algo a medias,
  reescribir `docs/ai/HANDOFF.md` desde `docs/ai/HANDOFF.template.md`.

```bash
git add web/src/app/providers.tsx web/AGENTS.md AGENTS.md docs/superpowers/plans/2026-08-29-offline-pwa.md
git commit -m "feat(web): cableado del modo offline en providers y documentación de agentes"
```

---

## Notas de ejecución

- **Orden estricto**: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11. Cada tarea
  depende de las interfaces de las anteriores.
- **Tests dirigidos primero**: en cada tarea se corre solo el test de esa
  tarea; la suite completa (`npm run test`) en Task 9 y en cada `./verify.ps1`.
- **Errores conocidos al ejecutar**: si `fake-indexeddb` no está disponible en
  el registro npm interno, la alternativa autorizada es dejar el almacén
  IndexedDB sin test directo (cubierto por el contrato en memoria) y anotarlo
  en el commit; no instalar otra librería sin consultar.
- El motor de IMGA usa el mismo patrón de backoff y lote; las diferencias de la
  PWA (token en memoria, GUIDs) están en el spec y no deben «corregirse» hacia
  el modelo IMGA.
