# Campana general de notificaciones y tareas de vacunación del trabajador — Plan de implementación

> **Para agentes:** ejecutar tarea por tarea con TDD (rojo, verde, limpieza). Los
> pasos usan casillas (`- [ ]`). Spec:
> [`2026-11-01-campana-tareas-vacunacion-trabajador-design.md`](../specs/2026-11-01-campana-tareas-vacunacion-trabajador-design.md).
> Tarea del sistema de agentes: `2026-10-30-gestor-veterinario-vacunacion`
> (ampliación de alcance del 2026-11-01, revisada para una campana única).
>
> **Antes de empezar:** las decisiones marcadas «Pendiente» en la tabla de
> decisiones de la spec deben estar confirmadas por el usuario. Si alguna cambia,
> el planificador ajusta este plan antes de implementar.

**Objetivo:** una sola campana general de notificaciones, siempre visible con
sesión, cuya insignia suma los pendientes de todas las fuentes y cuyo menú se
agrupa por secciones. Fuentes iniciales: «Vacunación» (Trabajador con
`Vacunacion`: una entrada-resumen hacia `/avicola/tareas`) y «Control de
acceso» (tenant con `ControlAcceso`: el comportamiento actual). La página
`/avicola/tareas` agrupa por galpón las pendientes vencidas y de hoy con
«Completar». En el galpón queda solo un aviso con enlace. El Cliente no cambia
en la vacunación.

**Arquitectura:** solo PWA. La campana vive en `web/src/app/notificaciones/` y
recibe una lista de fuentes declarativas (`FuenteNotificaciones`) de
`useFuentesNotificaciones`; cada fuente es un hook junto a su feature, con su
propia clave de React Query y `enabled` según su visibilidad. Se reutiliza
`GET /api/vacunacion/tareas` con la clave compartida
`CLAVE_NOTIFICACION_VACUNACION`, y se aplica en el cliente el filtro
`fechaProgramada <= hoyIso()`. Las funciones puras de agrupación y de textos
están en `tareasPendientes.ts`. Un hook concentra la consulta con refresco de
60 s.

**Stack:** React 19, MUI, React Query, React Router, Vitest + Testing Library.

## Restricciones globales

- Español correcto y neutro, sin voseo, con acentos, en UTF-8 sin BOM.
- Anti-PII: no registrar eventos de diagnóstico con vacunas, observaciones ni
  datos de trabajadores. Errores genéricos.
- No tocar la API ni el backend.
- No tocar la cola offline, IndexedDB ni el precalentado. La vacunación sigue
  online.
- No hacer commit ni push. Según `docs/ai/agentes/PROTOCOLO.md`, el
  desarrollador deja los cambios sin commitear y el orquestador commitea.
- Registrar con la CLI mientras se trabaja:
  `node quality/tarea.mjs registrar 2026-10-30-gestor-veterinario-vacunacion comando --texto "<comando y resultado>" --rol desarrollador --proveedor <p>`.
- Shell: PowerShell 5.1, sin `&&`. Pruebas dirigidas con
  `npm --prefix web test -- <patrón>`.
- Prohibido relajar baselines o umbrales de `quality/`.

## Mapa de archivos

| Acción | Archivo |
|---|---|
| Crear | `web/src/test/vacunacion.ts` |
| Crear | `web/src/features/avicola/tareasPendientes.ts` y `.test.ts` |
| Crear | `web/src/features/avicola/useTareasPendientesVacunacion.ts` |
| Crear | `web/src/app/notificaciones/fuentes.ts` y `.test.ts` |
| Crear | `web/src/app/notificaciones/useFuentesNotificaciones.ts` |
| Crear | `web/src/app/notificaciones/CampanaNotificaciones.tsx` y `.test.tsx` |
| Crear | `web/src/features/avicola/useFuenteNotificacionesVacunacion.ts` |
| Crear | `web/src/features/control-acceso/useFuenteNotificacionesAcceso.tsx` |
| Eliminar | `web/src/features/control-acceso/CampanaNotificaciones.tsx` y `.test.tsx` (con `git mv` de la prueba) |
| Crear | `web/src/features/avicola/TextoRecortado.tsx` |
| Crear | `web/src/features/avicola/TareasVacunacionPage.tsx` y `.test.tsx` |
| Modificar | `web/src/app/AppLayout.tsx` y `AppLayout.test.tsx` |
| Modificar | `web/src/app/paginasDiferidas.tsx`, `web/src/app/router.tsx` |
| Modificar | `web/src/features/avicola/GalponPage.tsx` y `GalponPage.test.tsx` |
| Modificar | `web/src/features/avicola/VacunacionNotificacion.tsx` y `.test.tsx` |
| Modificar | `docs/proyecto/gestion-avicola.md`, `docs/proyecto/frontend-pwa.md` |

---

## Tarea 1: funciones puras y utilidades de prueba

**Archivos:** `web/src/test/vacunacion.ts`,
`web/src/features/avicola/tareasPendientes.ts`,
`web/src/features/avicola/tareasPendientes.test.ts`.

- [ ] **1.1 Utilidades de prueba.** Crear `web/src/test/vacunacion.ts`:

```ts
import type { TareaVacunacionResumen } from '../lib/tipos';

// Fecha local AAAA-MM-DD desplazada en días respecto de hoy.
export function isoDesplazado(dias: number): string {
  const fecha = new Date();
  fecha.setDate(fecha.getDate() + dias);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${fecha.getFullYear()}-${p(fecha.getMonth() + 1)}-${p(fecha.getDate())}`;
}

export function tareaVacunacion(
  id: string,
  galponId: string,
  fechaProgramada: string,
  extra: Partial<TareaVacunacionResumen> = {},
): TareaVacunacionResumen {
  return {
    id,
    galponId,
    edadDia: 7,
    vacuna: `Vacuna ${id}`,
    modoAplicacion: null,
    fechaProgramada,
    estado: 'Pendiente',
    fechaAplicacion: null,
    avesVacunadas: null,
    observacionesProgramadas: null,
    observacionesAplicacion: null,
    motivoCancelacion: null,
    programaNombre: 'PLAN CAISY 1000',
    ...extra,
  };
}
```

- [ ] **1.2 Rojo.** Crear `tareasPendientes.test.ts`:

```ts
import type { Galpon } from '../../lib/tipos';
import { tareaVacunacion as tarea } from '../../test/vacunacion';
import {
  agruparPorGalpon,
  pendientesHastaHoy,
  textoPendientesGalpon,
  textoResumenVacunacion,
  textoTituloGrupo,
} from './tareasPendientes';

const HOY = '2026-11-01';

function galpon(id: string, numero: string): Galpon {
  return {
    id,
    numero,
    capacidadMaxima: 1000,
    gallinasActuales: 900,
    fechaNacimientoLote: '2026-01-01',
    fechaDescarteLote: null,
    descripcion: null,
  };
}

describe('pendientesHastaHoy', () => {
  test('deja solo pendientes vencidas y de hoy, de la más antigua a la más reciente', () => {
    const resultado = pendientesHastaHoy(
      [
        tarea('hoy', 'g1', HOY),
        tarea('manana', 'g1', '2026-11-02'),
        tarea('ayer', 'g1', '2026-10-31'),
        tarea('hecha', 'g1', '2026-10-30', { estado: 'Completada' }),
      ],
      HOY,
    );
    expect(resultado.map((t) => t.id)).toEqual(['ayer', 'hoy']);
  });
});

describe('agruparPorGalpon', () => {
  test('agrupa, ordena por la tarea más antigua y luego por número de galpón', () => {
    const grupos = agruparPorGalpon(
      [
        tarea('a', 'g10', HOY),
        tarea('b', 'g2', HOY),
        tarea('c', 'g3', '2026-10-20'),
        tarea('d', 'g2', '2026-10-25'),
      ],
      [galpon('g10', '10'), galpon('g2', '2'), galpon('g3', '3')],
    );
    expect(grupos.map((g) => g.numero)).toEqual(['3', '2', '10']);
    expect(grupos[1].tareas.map((t) => t.id)).toEqual(['d', 'b']);
  });

  test('con empate de fecha ordena por número de forma numérica', () => {
    const grupos = agruparPorGalpon(
      [tarea('a', 'g10', HOY), tarea('b', 'g2', HOY)],
      [galpon('g10', '10'), galpon('g2', '2')],
    );
    expect(grupos.map((g) => g.numero)).toEqual(['2', '10']);
  });

  test('un galpón desconocido queda sin número', () => {
    const [grupo] = agruparPorGalpon([tarea('a', 'gx', HOY)], []);
    expect(grupo.numero).toBeNull();
    expect(textoTituloGrupo(grupo)).toBe('Galpón — · 1 pendiente');
  });
});

describe('textos', () => {
  test('título del grupo en plural', () => {
    const [grupo] = agruparPorGalpon(
      [tarea('a', 'g1', HOY), tarea('b', 'g1', HOY)],
      [galpon('g1', '1')],
    );
    expect(textoTituloGrupo(grupo)).toBe('Galpón 1 · 2 pendientes');
  });

  test('aviso del galpón en singular y plural', () => {
    expect(textoPendientesGalpon(1)).toBe('1 tarea pendiente en este galpón');
    expect(textoPendientesGalpon(3)).toBe('3 tareas pendientes en este galpón');
  });

  test('entrada-resumen de la campana, con y sin vencidas', () => {
    expect(textoResumenVacunacion(1, 0)).toBe('1 tarea pendiente');
    expect(textoResumenVacunacion(2, 0)).toBe('2 tareas pendientes');
    expect(textoResumenVacunacion(3, 1)).toBe('3 tareas pendientes (1 vencida)');
    expect(textoResumenVacunacion(3, 2)).toBe('3 tareas pendientes (2 vencidas)');
  });
});
```

Ejecutar `npm --prefix web test -- tareasPendientes`. Debe fallar porque el
módulo no existe.

- [ ] **1.3 Verde.** Crear `tareasPendientes.ts`:

```ts
import type { Galpon, TareaVacunacionResumen } from '../../lib/tipos';
import { hoyIso } from './constantes';

// Umbral fijo para recortar textos a dos líneas con «Ver más». Es fijo y no
// medido para que la regla sea comprobable en jsdom.
export const LIMITE_TEXTO_LARGO = 80;

export interface GrupoTareasGalpon {
  galponId: string;
  numero: string | null;
  tareas: TareaVacunacionResumen[];
}

const porFecha = (a: TareaVacunacionResumen, b: TareaVacunacionResumen) =>
  a.fechaProgramada.localeCompare(b.fechaProgramada);

// Regla de visibilidad del trabajador: pendientes con fecha programada anterior
// o igual a la fecha local. El servidor calcula «hoy» en UTC, y desde las 20:00
// de Bolivia ya incluye las tareas de mañana. Este filtro las descarta.
export function pendientesHastaHoy(
  tareas: TareaVacunacionResumen[],
  hoy: string = hoyIso(),
): TareaVacunacionResumen[] {
  return tareas
    .filter((t) => t.estado === 'Pendiente' && t.fechaProgramada <= hoy)
    .sort(porFecha);
}

// Grupos por galpón. Primero va el que tiene la tarea más antigua, de modo que
// las vencidas quedan arriba. El empate se resuelve por número de galpón, con
// orden numérico.
export function agruparPorGalpon(
  tareas: TareaVacunacionResumen[],
  galpones: Galpon[],
): GrupoTareasGalpon[] {
  const porGalpon = new Map<string, TareaVacunacionResumen[]>();
  for (const tarea of [...tareas].sort(porFecha)) {
    const lista = porGalpon.get(tarea.galponId) ?? [];
    lista.push(tarea);
    porGalpon.set(tarea.galponId, lista);
  }
  return [...porGalpon.entries()]
    .map(([galponId, lista]) => ({
      galponId,
      numero: galpones.find((g) => g.id === galponId)?.numero ?? null,
      tareas: lista,
    }))
    .sort(
      (a, b) =>
        porFecha(a.tareas[0], b.tareas[0]) ||
        (a.numero ?? '').localeCompare(b.numero ?? '', 'es', { numeric: true }),
    );
}

export function textoTituloGrupo(grupo: GrupoTareasGalpon): string {
  const cantidad = grupo.tareas.length;
  return `Galpón ${grupo.numero ?? '—'} · ${cantidad} ${cantidad === 1 ? 'pendiente' : 'pendientes'}`;
}

export function textoPendientesGalpon(cantidad: number): string {
  return cantidad === 1
    ? '1 tarea pendiente en este galpón'
    : `${cantidad} tareas pendientes en este galpón`;
}

// Texto de la única entrada de la sección «Vacunación» de la campana general.
export function textoResumenVacunacion(cantidad: number, vencidas: number): string {
  const base = cantidad === 1 ? '1 tarea pendiente' : `${cantidad} tareas pendientes`;
  if (vencidas === 0) return base;
  return `${base} (${vencidas} ${vencidas === 1 ? 'vencida' : 'vencidas'})`;
}
```

Ejecutar de nuevo la prueba dirigida: verde.

---

## Tarea 2: campana general, fuentes y cabecera

**Archivos:** `web/src/app/notificaciones/fuentes.ts` y `.test.ts`,
`web/src/app/notificaciones/useFuentesNotificaciones.ts`,
`web/src/app/notificaciones/CampanaNotificaciones.tsx` y `.test.tsx`,
`web/src/features/avicola/useTareasPendientesVacunacion.ts`,
`web/src/features/avicola/useFuenteNotificacionesVacunacion.ts`,
`web/src/features/control-acceso/useFuenteNotificacionesAcceso.tsx`,
`web/src/app/AppLayout.tsx`, `web/src/app/AppLayout.test.tsx`. Se eliminan
`web/src/features/control-acceso/CampanaNotificaciones.tsx` y su prueba, que se
muda.

- [ ] **2.1 Rojo: contrato de fuentes.** Crear
  `web/src/app/notificaciones/fuentes.test.ts`:

```ts
import { etiquetaCampana, totalPendientes, type FuenteNotificaciones } from './fuentes';

const fuente = (id: string, visible: boolean, conteo: number): FuenteNotificaciones => ({
  id,
  seccion: id,
  visible,
  conteo,
  entradas: [],
  error: false,
});

describe('fuentes de notificaciones', () => {
  test('la insignia suma solo las fuentes visibles', () => {
    expect(
      totalPendientes([fuente('a', true, 2), fuente('b', true, 1), fuente('c', false, 5)]),
    ).toBe(3);
    expect(totalPendientes([])).toBe(0);
  });

  test('nombre accesible de la campana', () => {
    expect(etiquetaCampana(0)).toBe('Notificaciones sin pendientes');
    expect(etiquetaCampana(1)).toBe('Notificaciones: 1 pendiente');
    expect(etiquetaCampana(4)).toBe('Notificaciones: 4 pendientes');
  });
});
```

Ejecutar `npm --prefix web test -- fuentes`: rojo porque el módulo no existe.

- [ ] **2.2 Verde: contrato.** Crear `web/src/app/notificaciones/fuentes.ts`:

```ts
import type { ReactNode } from 'react';

// Contrato de una fuente de la campana general. Para añadir una fuente: crear
// su hook junto a su feature y sumarlo en useFuentesNotificaciones. La campana
// no cambia.
export interface EntradaNotificacion {
  id: string;
  texto: string;
  detalle?: string;
  icono?: ReactNode;
  destino: string;
  // Acción previa a navegar, por ejemplo marcar como leída. Si falla, no se navega.
  alSeleccionar?: () => Promise<void>;
}

export interface FuenteNotificaciones {
  id: string;
  seccion: string;
  // false: el usuario no puede ver la sección y su consulta no se ejecuta.
  visible: boolean;
  conteo: number;
  entradas: EntradaNotificacion[];
  // La consulta falló y no hay datos previos.
  error: boolean;
}

export const MENSAJE_ERROR_FUENTE = 'No se pudieron cargar estas notificaciones.';

export function totalPendientes(fuentes: FuenteNotificaciones[]): number {
  return fuentes.filter((f) => f.visible).reduce((suma, f) => suma + f.conteo, 0);
}

export function etiquetaCampana(total: number): string {
  if (total === 0) return 'Notificaciones sin pendientes';
  return `Notificaciones: ${total} ${total === 1 ? 'pendiente' : 'pendientes'}`;
}
```

Ejecutar la prueba dirigida: verde.

- [ ] **2.3 Rojo: campana general.** Mudar la prueba con
  `git mv web/src/features/control-acceso/CampanaNotificaciones.test.tsx web/src/app/notificaciones/CampanaNotificaciones.test.tsx`
  y reescribirla así. Las dos primeras pruebas son las existentes: conservan sus
  comprobaciones (contador de sin leer, menú, `marcar-leida` y navegación a
  incidencias) y solo añaden la sesión, porque la campana ahora la necesita.

```tsx
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { AuthProvider, useAuth } from '../../features/auth/AuthContext';
import type { Funcionalidad, Modulo, Rol } from '../../lib/tipos';
import { isoDesplazado, tareaVacunacion } from '../../test/vacunacion';
import { CampanaNotificaciones } from './CampanaNotificaciones';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function fetchSimulado(reglas: Record<string, Response | Response[]>) {
  const colas = new Map(
    Object.entries(reglas).map(([clave, valor]) => [
      clave,
      Array.isArray(valor) ? [...valor] : [valor],
    ]),
  );
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = input instanceof Request ? input : new Request(String(input), init);
    const clave = `${req.method} ${new URL(req.url).pathname}`;
    const cola = colas.get(clave);
    const valor = cola?.shift();
    return valor ?? new Response('', { status: 404 });
  });
  vi.stubGlobal('fetch', fn);
  return fn;
}

function sesion(rol: Rol, modulos: Modulo[], funcionalidades: Funcionalidad[] = []) {
  return {
    'POST /api/identidad/sesion/renovar': respuesta(200, {
      accessToken: 't',
      expiraEnSegundos: 900,
    }),
    'GET /api/identidad/me': respuesta(200, {
      usuarioId: 'u1',
      correo: 'persona@icarus.test',
      rol,
      clienteId: 'cli1',
      trabajadorId: null,
      modulos,
      funcionalidades,
    }),
  };
}

const INCIDENCIA = {
  id: 'n1',
  incidenciaId: 'i1',
  tipo: 'IncidenciaAcceso',
  fechaUtc: '2026-09-27T12:00:00Z',
  leida: false,
};

function RutaActual() {
  const { pathname } = useLocation();
  return <span data-testid="ruta-actual">{pathname}</span>;
}

// Permite esperar a que la sesión cargue antes de afirmar que algo no se pidió.
function RolActual() {
  const { rol } = useAuth();
  return <span data-testid="rol">{rol ?? ''}</span>;
}

function renderCampana(reglas: Record<string, Response | Response[]>) {
  const fetchMock = fetchSimulado(reglas);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/inicio']}>
        <AuthProvider>
          <Routes>
            <Route path="*" element={<CampanaNotificaciones />} />
          </Routes>
          <RutaActual />
          <RolActual />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return fetchMock;
}

const pidio = (fetchMock: ReturnType<typeof vi.fn>, metodo: string, ruta: string) =>
  fetchMock.mock.calls.some(([arg, init]) => {
    const req = arg instanceof Request ? arg : new Request(String(arg), init as RequestInit);
    return req.method === metodo && new URL(req.url).pathname === ruta;
  });

async function esperarSesion(rol: Rol) {
  await waitFor(() => expect(screen.getByTestId('rol')).toHaveTextContent(rol));
  await new Promise((resolver) => setTimeout(resolver, 50));
}

describe('CampanaNotificaciones', () => {
  beforeEach(() => vi.restoreAllMocks());
  afterEach(() => vi.unstubAllGlobals());

  test('muestra el contador de notificaciones sin leer', async () => {
    renderCampana({
      ...sesion('Cliente', ['ControlAcceso']),
      'GET /api/control-acceso/notificaciones': respuesta(200, {
        items: [INCIDENCIA],
        contador: 1,
      }),
    });

    expect(await screen.findByText('1')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Notificaciones: 1 pendiente' }),
    ).toBeInTheDocument();
  });

  test('abre el menú y navega a incidencias al seleccionar una notificación', async () => {
    const usuario = userEvent.setup();
    const fetchMock = renderCampana({
      ...sesion('Cliente', ['ControlAcceso']),
      'GET /api/control-acceso/notificaciones': respuesta(200, {
        items: [INCIDENCIA],
        contador: 1,
      }),
      'POST /api/control-acceso/notificaciones/n1/marcar-leida': respuesta(204),
    });

    await usuario.click(
      await screen.findByRole('button', { name: 'Notificaciones: 1 pendiente' }),
    );
    expect(screen.getByText('Control de acceso')).toBeInTheDocument();
    await usuario.click(
      await screen.findByRole('menuitem', { name: /incidencia de marcación/ }),
    );

    await waitFor(() =>
      expect(screen.getByTestId('ruta-actual')).toHaveTextContent('/control-acceso/incidencias'),
    );
    expect(
      pidio(fetchMock, 'POST', '/api/control-acceso/notificaciones/n1/marcar-leida'),
    ).toBe(true);
  });

  test('sin fuentes visibles se ve sin insignia y dice que no hay notificaciones', async () => {
    const usuario = userEvent.setup();
    const fetchMock = renderCampana(sesion('Administrador', []));
    await esperarSesion('Administrador');

    const campana = screen.getByRole('button', { name: 'Notificaciones sin pendientes' });
    expect(within(campana).queryByText('0')).not.toBeInTheDocument();
    await usuario.click(campana);
    expect(await screen.findByText('No hay notificaciones')).toBeInTheDocument();
    expect(pidio(fetchMock, 'GET', '/api/control-acceso/notificaciones')).toBe(false);
    expect(pidio(fetchMock, 'GET', '/api/vacunacion/tareas')).toBe(false);
  });

  test('el trabajador con Vacunacion y ControlAcceso ve la suma y las dos secciones', async () => {
    const usuario = userEvent.setup();
    const fetchMock = renderCampana({
      ...sesion('Trabajador', ['GestionAvicola', 'ControlAcceso'], ['Vacunacion']),
      'GET /api/vacunacion/tareas': respuesta(200, {
        vencidasYHoy: [
          tareaVacunacion('t1', 'g1', isoDesplazado(-2)),
          tareaVacunacion('t2', 'g2', isoDesplazado(0)),
          tareaVacunacion('t3', 'g2', isoDesplazado(1)),
        ],
        proximas: [],
      }),
      'GET /api/control-acceso/notificaciones': respuesta(200, {
        items: [INCIDENCIA],
        contador: 1,
      }),
    });

    const campana = await screen.findByRole('button', { name: 'Notificaciones: 3 pendientes' });
    expect(within(campana).getByText('3')).toBeInTheDocument();
    await usuario.click(campana);

    const vacunacion = screen.getByText('Vacunación');
    const acceso = screen.getByText('Control de acceso');
    expect(
      vacunacion.compareDocumentPosition(acceso) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();

    await usuario.click(
      screen.getByRole('menuitem', { name: '2 tareas pendientes (1 vencida)' }),
    );
    await waitFor(() =>
      expect(screen.getByTestId('ruta-actual')).toHaveTextContent('/avicola/tareas'),
    );
    // La entrada de vacunación no se marca como leída.
    expect(
      pidio(fetchMock, 'POST', '/api/control-acceso/notificaciones/n1/marcar-leida'),
    ).toBe(false);
  });

  test('si falla una fuente, la otra sigue funcionando', async () => {
    const usuario = userEvent.setup();
    renderCampana({
      ...sesion('Trabajador', ['GestionAvicola', 'ControlAcceso'], ['Vacunacion']),
      'GET /api/vacunacion/tareas': respuesta(500, { title: 'Error' }),
      'GET /api/control-acceso/notificaciones': respuesta(200, {
        items: [INCIDENCIA],
        contador: 1,
      }),
    });

    await usuario.click(
      await screen.findByRole('button', { name: 'Notificaciones: 1 pendiente' }),
    );
    expect(
      await screen.findByText('No se pudieron cargar estas notificaciones.'),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('menuitem', { name: /incidencia de marcación/ }),
    ).toBeInTheDocument();
  });

  test('el cliente con Vacunacion no consulta ni ve la sección de vacunación', async () => {
    const usuario = userEvent.setup();
    const fetchMock = renderCampana(sesion('Cliente', ['GestionAvicola'], ['Vacunacion']));
    await esperarSesion('Cliente');

    expect(pidio(fetchMock, 'GET', '/api/vacunacion/tareas')).toBe(false);
    await usuario.click(screen.getByRole('button', { name: 'Notificaciones sin pendientes' }));
    expect(screen.queryByText('Vacunación')).not.toBeInTheDocument();
    expect(screen.getByText('No hay notificaciones')).toBeInTheDocument();
  });
});
```

Si `Modulo` no se exporta con ese nombre desde `lib/tipos`, usar el tipo que
emplea `AuthContext` para `modulos`. Ejecutar
`npm --prefix web test -- CampanaNotificaciones`: rojo porque el módulo no
existe en la ruta nueva.

- [ ] **2.4 Verde: hook de tareas.** Crear `useTareasPendientesVacunacion.ts`:

```ts
import { useQuery } from '@tanstack/react-query';
import { useFuncionalidad } from '../auth/useFuncionalidad';
import { obtenerNotificacionVacunacion } from './api';
import { CLAVE_NOTIFICACION_VACUNACION } from './constantes';
import { pendientesHastaHoy } from './tareasPendientes';

export const INTERVALO_REFRESCO_VACUNACION_MS = 60_000;

// Comparte la clave con VacunacionNotificacion y con los diálogos de completar,
// cancelar y asignar, que ya la invalidan. Así la insignia baja al instante.
// Sin red, React Query pausa la consulta y conserva el último dato.
export function useTareasPendientesVacunacion(habilitado = true) {
  const puede = useFuncionalidad('Vacunacion');
  const consulta = useQuery({
    queryKey: CLAVE_NOTIFICACION_VACUNACION,
    queryFn: obtenerNotificacionVacunacion,
    enabled: puede && habilitado,
    refetchInterval: INTERVALO_REFRESCO_VACUNACION_MS,
  });
  // Se calcula en cada render, sin memo: la fecha local puede cambiar aunque
  // los datos no cambien.
  const pendientes = pendientesHastaHoy(consulta.data?.vencidasYHoy ?? []);
  return {
    pendientes,
    cargando: consulta.isLoading,
    // Error solo si no hay datos previos: un refresco fallido conserva lo último.
    error: consulta.isError && consulta.data === undefined,
    reintentar: () => void consulta.refetch(),
  };
}
```

- [ ] **2.5 Verde: fuente «Vacunación».** Crear
  `web/src/features/avicola/useFuenteNotificacionesVacunacion.ts`:

```ts
import type { FuenteNotificaciones } from '../../app/notificaciones/fuentes';
import { useAuth } from '../auth/AuthContext';
import { useFuncionalidad } from '../auth/useFuncionalidad';
import { clasificarTarea } from './formatos';
import { textoResumenVacunacion } from './tareasPendientes';
import { useTareasPendientesVacunacion } from './useTareasPendientesVacunacion';

// Solo el Trabajador con Vacunacion. Una entrada-resumen sin estado «leído»:
// desaparece al completar las tareas.
export function useFuenteNotificacionesVacunacion(): FuenteNotificaciones {
  const { tieneRol } = useAuth();
  const esTrabajador = tieneRol('Trabajador');
  const puede = useFuncionalidad('Vacunacion');
  const { pendientes, error } = useTareasPendientesVacunacion(esTrabajador);
  const visible = esTrabajador && puede;
  const vencidas = pendientes.filter(
    (t) => clasificarTarea(t.fechaProgramada) === 'Vencida',
  ).length;
  return {
    id: 'vacunacion',
    seccion: 'Vacunación',
    visible,
    conteo: visible ? pendientes.length : 0,
    entradas:
      visible && pendientes.length > 0
        ? [
            {
              id: 'resumen',
              texto: textoResumenVacunacion(pendientes.length, vencidas),
              destino: '/avicola/tareas',
            },
          ]
        : [],
    error: visible && error,
  };
}
```

- [ ] **2.6 Verde: fuente «Control de acceso».** Crear
  `web/src/features/control-acceso/useFuenteNotificacionesAcceso.tsx`, con
  `textoNotificacion`, `horaResumida` y el icono movidos sin cambios desde el
  componente actual:

```tsx
import { useQuery, useQueryClient } from '@tanstack/react-query';
import MarkEmailReadRoundedIcon from '@mui/icons-material/MarkEmailReadRounded';
import type { FuenteNotificaciones } from '../../app/notificaciones/fuentes';
import { useAuth } from '../auth/AuthContext';
import {
  listarNotificacionesAcceso,
  marcarNotificacionAccesoLeida,
  type NotificacionAccesoResumen,
} from './api';

export const CLAVE_NOTIFICACIONES_ACCESO = ['control-acceso', 'notificaciones'] as const;
const INTERVALO_REFRESCO_MS = 60_000;

function textoNotificacion(notificacion: NotificacionAccesoResumen): string {
  if (notificacion.tipo === 'IncidenciaAcceso') return 'Hay una incidencia de marcación por revisar';
  return 'Nueva notificación';
}

function horaResumida(fechaUtc: string): string {
  return new Intl.DateTimeFormat('es-BO', {
    timeZone: 'America/La_Paz',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(fechaUtc));
}

// Mismo comportamiento que la campana anterior: solo con el módulo
// ControlAcceso, contador de sin leer y marcar leída al elegir.
export function useFuenteNotificacionesAcceso(): FuenteNotificaciones {
  const { modulos } = useAuth();
  const queryClient = useQueryClient();
  const visible = modulos.includes('ControlAcceso');
  const { data, isError } = useQuery({
    queryKey: CLAVE_NOTIFICACIONES_ACCESO,
    queryFn: listarNotificacionesAcceso,
    enabled: visible,
    refetchInterval: INTERVALO_REFRESCO_MS,
  });
  const items = visible ? (data?.items ?? []) : [];
  return {
    id: 'control-acceso',
    seccion: 'Control de acceso',
    visible,
    conteo: visible ? (data?.contador ?? 0) : 0,
    entradas: items.map((n) => ({
      id: n.id,
      texto: textoNotificacion(n),
      detalle: horaResumida(n.fechaUtc),
      icono: <MarkEmailReadRoundedIcon fontSize="small" />,
      destino: '/control-acceso/incidencias',
      alSeleccionar: async () => {
        await marcarNotificacionAccesoLeida(n.id);
        void queryClient.invalidateQueries({ queryKey: CLAVE_NOTIFICACIONES_ACCESO });
      },
    })),
    error: visible && isError && data === undefined,
  };
}
```

- [ ] **2.7 Verde: registro y campana.** Crear
  `web/src/app/notificaciones/useFuentesNotificaciones.ts`:

```ts
import { useFuenteNotificacionesVacunacion } from '../../features/avicola/useFuenteNotificacionesVacunacion';
import { useFuenteNotificacionesAcceso } from '../../features/control-acceso/useFuenteNotificacionesAcceso';
import type { FuenteNotificaciones } from './fuentes';

// Orden de las secciones del menú. Cada hook se llama siempre y en el mismo
// orden; la visibilidad va dentro de la fuente (visible + enabled), nunca en un
// montaje condicional. Fuente nueva: una línea más aquí.
export function useFuentesNotificaciones(): FuenteNotificaciones[] {
  return [useFuenteNotificacionesVacunacion(), useFuenteNotificacionesAcceso()];
}
```

Crear `web/src/app/notificaciones/CampanaNotificaciones.tsx`:

```tsx
import {
  Badge,
  IconButton,
  ListItemIcon,
  ListItemText,
  ListSubheader,
  Menu,
  MenuItem,
  Typography,
} from '@mui/material';
import NotificationsRoundedIcon from '@mui/icons-material/NotificationsRounded';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  etiquetaCampana,
  MENSAJE_ERROR_FUENTE,
  totalPendientes,
  type EntradaNotificacion,
} from './fuentes';
import { useFuentesNotificaciones } from './useFuentesNotificaciones';

// Campana general: no conoce ninguna fuente concreta.
export function CampanaNotificaciones() {
  const navigate = useNavigate();
  const [ancla, setAncla] = useState<HTMLElement | null>(null);
  const fuentes = useFuentesNotificaciones();
  const total = totalPendientes(fuentes);
  const secciones = fuentes.filter((f) => f.visible && (f.entradas.length > 0 || f.error));

  const cerrar = () => setAncla(null);

  const seleccionar = async (entrada: EntradaNotificacion) => {
    await entrada.alSeleccionar?.();
    cerrar();
    navigate(entrada.destino);
  };

  return (
    <>
      <IconButton
        color="inherit"
        aria-label={etiquetaCampana(total)}
        onClick={(evento) => setAncla(evento.currentTarget)}
      >
        {total > 0 ? (
          <Badge badgeContent={total} color="error">
            <NotificationsRoundedIcon />
          </Badge>
        ) : (
          <NotificationsRoundedIcon />
        )}
      </IconButton>
      <Menu
        anchorEl={ancla}
        open={Boolean(ancla)}
        onClose={cerrar}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { minWidth: 320 } } }}
      >
        {secciones.length === 0 && (
          <MenuItem disabled>
            <ListItemText primary="No hay notificaciones" />
          </MenuItem>
        )}
        {/* Arrays planos y no Fragment: Menu no admite Fragment como hijo. */}
        {secciones.flatMap((fuente) => [
          <ListSubheader key={`${fuente.id}-titulo`}>{fuente.seccion}</ListSubheader>,
          ...(fuente.error
            ? [
                <MenuItem key={`${fuente.id}-error`} disabled>
                  <ListItemText primary={MENSAJE_ERROR_FUENTE} />
                </MenuItem>,
              ]
            : []),
          ...fuente.entradas.map((entrada) => (
            <MenuItem
              key={`${fuente.id}-${entrada.id}`}
              onClick={() => void seleccionar(entrada)}
            >
              {entrada.icono && <ListItemIcon>{entrada.icono}</ListItemIcon>}
              <ListItemText
                primary={entrada.texto}
                secondary={
                  entrada.detalle && (
                    <Typography variant="caption" color="text.secondary">
                      {entrada.detalle}
                    </Typography>
                  )
                }
              />
            </MenuItem>
          )),
        ])}
      </Menu>
    </>
  );
}
```

Eliminar `web/src/features/control-acceso/CampanaNotificaciones.tsx`
(`git rm`). Comprobar con una búsqueda de `features/control-acceso/CampanaNotificaciones`
que no quedan importaciones. Ejecutar
`npm --prefix web test -- CampanaNotificaciones fuentes tareasPendientes`: verde.

- [ ] **2.8 Rojo: cabecera.** En `AppLayout.test.tsx`:

  1. Sustituir el `fetch` secuencial por uno por ruta y envolver en
     `QueryClientProvider`, porque la campana se monta siempre con sesión y
     hace consultas.
  2. Añadir la ruta de destino `/avicola/tareas`.
  3. Añadir las pruebas.

Imports nuevos (añadir `within` al import de `@testing-library/react`):

```tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { isoDesplazado, tareaVacunacion } from '../test/vacunacion';
```

Nueva `renderLayout`, que mantiene la firma actual y añade `tareas`:

```tsx
function renderLayout(
  rol: Rol,
  rutaInicial = '/',
  funcionalidades: Funcionalidad[] = [],
  tareas: unknown[] = [],
) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = input instanceof Request ? input : new Request(String(input), init);
    const clave = `${req.method} ${new URL(req.url).pathname}`;
    if (clave === 'POST /api/identidad/sesion/renovar') {
      return respuesta(200, { accessToken: 'nuevo' });
    }
    if (clave === 'GET /api/identidad/me') {
      return respuesta(200, {
        usuarioId: 'u1',
        correo: 'persona@icarus.test',
        rol,
        clienteId: null,
        trabajadorId: null,
        modulos: [],
        funcionalidades,
      });
    }
    if (clave === 'GET /api/vacunacion/tareas') {
      return respuesta(200, { vencidasYHoy: tareas, proximas: [] });
    }
    return new Response('', { status: 404 });
  });
  vi.stubGlobal('fetch', fetchMock);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const resultado = render(
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <MemoryRouter initialEntries={[rutaInicial]}>
          <AuthProvider>
            <Routes>
              <Route element={<AppLayout />}>
                <Route path="/" element={<div>contenido del layout</div>} />
                <Route path="/admin/clientes" element={<div>listado de clientes</div>} />
                <Route path="/avicola/tareas" element={<div>página de tareas</div>} />
              </Route>
              <Route path="/login" element={<div>pantalla de login</div>} />
            </Routes>
          </AuthProvider>
        </MemoryRouter>
      </ThemeProvider>
    </QueryClientProvider>,
  );
  return { ...resultado, fetchMock };
}
```

Pruebas nuevas dentro del `describe('AppLayout')`:

```tsx
test('la campana está siempre en la cabecera, aunque no haya módulos ni notificaciones', async () => {
  const usuario = userEvent.setup();
  renderLayout('Administrador');

  const campana = await screen.findByRole('button', { name: 'Notificaciones sin pendientes' });
  await usuario.click(campana);
  expect(await screen.findByText('No hay notificaciones')).toBeInTheDocument();
});

test('el trabajador con Vacunacion ve la insignia sin las futuras y llega a sus tareas', async () => {
  const usuario = userEvent.setup();
  renderLayout('Trabajador', '/', ['Vacunacion'], [
    tareaVacunacion('t1', 'g1', isoDesplazado(-2)),
    tareaVacunacion('t2', 'g1', isoDesplazado(0)),
    tareaVacunacion('t3', 'g2', isoDesplazado(1)),
  ]);

  const campana = await screen.findByRole('button', { name: 'Notificaciones: 2 pendientes' });
  expect(within(campana).getByText('2')).toBeInTheDocument();

  await usuario.click(campana);
  await usuario.click(
    screen.getByRole('menuitem', { name: '2 tareas pendientes (1 vencida)' }),
  );
  expect(await screen.findByText('página de tareas')).toBeInTheDocument();
});
```

Ejecutar `npm --prefix web test -- AppLayout`: las dos pruebas nuevas fallan y
las existentes siguen verdes con el `fetch` por ruta. Si alguna existente falla,
corregir el `fetch` simulado, no la prueba.

- [ ] **2.9 Verde: cabecera.** En `AppLayout.tsx`:

  1. Sustituir el import
     `import { CampanaNotificaciones } from '../features/control-acceso/CampanaNotificaciones';`
     por `import { CampanaNotificaciones } from './notificaciones/CampanaNotificaciones';`.
  2. Añadir `estaAutenticado` a la desestructuración de `useAuth()`.
  3. Sustituir `{modulos.includes('ControlAcceso') && <CampanaNotificaciones />}`
     por:

```tsx
{estaAutenticado && <CampanaNotificaciones />}
```

`modulos` se sigue usando en `obtenerEnlacesNavegacion`. Ejecutar
`npm --prefix web test -- AppLayout CampanaNotificaciones fuentes tareasPendientes`:
verde.

---

## Tarea 3: página `/avicola/tareas`

**Archivos:** `TextoRecortado.tsx`, `TareasVacunacionPage.tsx`,
`TareasVacunacionPage.test.tsx`, `paginasDiferidas.tsx`, `router.tsx`.

- [ ] **3.1 Rojo.** Crear `TareasVacunacionPage.test.tsx`:

```tsx
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { AuthProvider } from '../auth/AuthContext';
import { isoDesplazado, tareaVacunacion } from '../../test/vacunacion';
import { TareasVacunacionPage } from './TareasVacunacionPage';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

const galpon = (id: string, numero: string) => ({
  id,
  numero,
  capacidadMaxima: 1000,
  gallinasActuales: 900,
  fechaNacimientoLote: '2026-01-01',
  fechaDescarteLote: null,
  descripcion: null,
});

function prepararFetch(vencidasYHoy: unknown[] | 'error') {
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = input instanceof Request ? input : new Request(String(input), init);
    const clave = `${req.method} ${new URL(req.url).pathname}`;
    switch (clave) {
      case 'POST /api/identidad/sesion/renovar':
        return respuesta(200, { accessToken: 't', expiraEnSegundos: 900 });
      case 'GET /api/identidad/me':
        return respuesta(200, {
          usuarioId: 'u1',
          rol: 'Trabajador',
          clienteId: 'cli1',
          trabajadorId: 'tr1',
          modulos: ['GestionAvicola'],
          funcionalidades: ['Vacunacion'],
        });
      case 'GET /api/granjas':
        return respuesta(200, [{ id: 'gr1', nombre: 'Granja' }]);
      case 'GET /api/granjas/gr1/galpones':
        return respuesta(200, [galpon('g1', '1'), galpon('g2', '2')]);
      case 'GET /api/vacunacion/tareas':
        return vencidasYHoy === 'error'
          ? respuesta(500, { title: 'Error' })
          : respuesta(200, { vencidasYHoy, proximas: [] });
      default:
        return new Response('', { status: 404 });
    }
  });
  vi.stubGlobal('fetch', fn);
  return fn;
}

function renderPagina() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <AuthProvider>
          <TareasVacunacionPage />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('TareasVacunacionPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('agrupa por galpón, pone primero las vencidas y omite las futuras', async () => {
    prepararFetch([
      tareaVacunacion('t1', 'g2', isoDesplazado(0), { vacuna: 'NEWCASTLE' }),
      tareaVacunacion('t2', 'g1', isoDesplazado(0), { vacuna: 'GUMBORO' }),
      tareaVacunacion('t3', 'g1', isoDesplazado(-3), { vacuna: 'BRONQUITIS', edadDia: 14 }),
      tareaVacunacion('t4', 'g2', isoDesplazado(1), { vacuna: 'MAÑANA' }),
    ]);
    renderPagina();

    const grupo1 = await screen.findByRole('list', { name: 'Galpón 1 · 2 pendientes' });
    const grupo2 = screen.getByRole('list', { name: 'Galpón 2 · 1 pendiente' });
    expect(
      grupo1.compareDocumentPosition(grupo2) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();

    const [primera, segunda] = within(grupo1).getAllByRole('listitem');
    expect(within(primera).getByText('BRONQUITIS')).toBeInTheDocument();
    expect(within(primera).getByText('Vencida')).toBeInTheDocument();
    expect(
      within(primera).getByText(`Día 14 · Programada ${isoDesplazado(-3)}`),
    ).toBeInTheDocument();
    expect(within(segunda).getByText('Hoy')).toBeInTheDocument();
    expect(within(primera).getByRole('button', { name: 'Completar' })).toBeInTheDocument();

    expect(screen.queryByText('MAÑANA')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cancelar' })).not.toBeInTheDocument();
  });

  test('Completar abre el diálogo de la tarea', async () => {
    prepararFetch([tareaVacunacion('t1', 'g1', isoDesplazado(0), { vacuna: 'GUMBORO' })]);
    renderPagina();

    await userEvent.click(await screen.findByRole('button', { name: 'Completar' }));
    expect(
      await screen.findByRole('heading', { name: 'Marcar como aplicada — GUMBORO' }),
    ).toBeInTheDocument();
  });

  test('recorta el texto largo y lo despliega con Ver más', async () => {
    const largo = 'Aplicar en agua de bebida '.repeat(5).trim();
    prepararFetch([tareaVacunacion('t1', 'g1', isoDesplazado(0), { vacuna: largo })]);
    renderPagina();

    const boton = await screen.findByRole('button', { name: 'Ver más' });
    expect(boton).toHaveAttribute('aria-expanded', 'false');
    await userEvent.click(boton);
    expect(screen.getByRole('button', { name: 'Ver menos' })).toHaveAttribute(
      'aria-expanded',
      'true',
    );
  });

  test('un texto corto no ofrece Ver más', async () => {
    prepararFetch([tareaVacunacion('t1', 'g1', isoDesplazado(0), { vacuna: 'GUMBORO' })]);
    renderPagina();
    await screen.findByText('GUMBORO');
    expect(screen.queryByRole('button', { name: 'Ver más' })).not.toBeInTheDocument();
  });

  test('sin pendientes muestra el mensaje vacío', async () => {
    prepararFetch([tareaVacunacion('t1', 'g1', isoDesplazado(2))]);
    renderPagina();
    expect(
      await screen.findByText('No hay tareas de vacunación pendientes.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });

  test('un error de carga muestra un mensaje genérico con Reintentar', async () => {
    prepararFetch('error');
    renderPagina();
    expect(
      await screen.findByText('No se pudieron cargar las tareas de vacunación.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });
});
```

Ejecutar `npm --prefix web test -- TareasVacunacionPage`: rojo porque el
módulo no existe.

- [ ] **3.2 Verde: texto recortado.** Crear `TextoRecortado.tsx`:

```tsx
import { Box, Button, Typography } from '@mui/material';
import type { TypographyProps } from '@mui/material';
import { useState } from 'react';
import { LIMITE_TEXTO_LARGO } from './tareasPendientes';

const RECORTE_DOS_LINEAS = {
  display: '-webkit-box',
  WebkitLineClamp: 2,
  WebkitBoxOrient: 'vertical',
  overflow: 'hidden',
} as const;

export function TextoRecortado({
  texto,
  variante = 'body1',
}: {
  texto: string;
  variante?: TypographyProps['variant'];
}) {
  const [expandido, setExpandido] = useState(false);
  const largo = texto.length > LIMITE_TEXTO_LARGO;
  return (
    <Box>
      <Typography
        variant={variante}
        sx={{ overflowWrap: 'anywhere', ...(largo && !expandido ? RECORTE_DOS_LINEAS : {}) }}
      >
        {texto}
      </Typography>
      {largo && (
        <Button
          size="small"
          aria-expanded={expandido}
          onClick={() => setExpandido((v) => !v)}
          sx={{ px: 0, minWidth: 0 }}
        >
          {expandido ? 'Ver menos' : 'Ver más'}
        </Button>
      )}
    </Box>
  );
}
```

- [ ] **3.3 Verde: página.** Crear `TareasVacunacionPage.tsx`:

```tsx
import { Box, Button, Chip, Container, Paper, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import { PaginaCabecera } from '../../app/ui/PaginaCabecera';
import type { TareaVacunacionResumen } from '../../lib/tipos';
import { listarGalpones, listarGranjas } from './api';
import { CLAVE_GRANJAS } from './constantes';
import { CompletarTareaDialog } from './CompletarTareaDialog';
import { clasificarTarea } from './formatos';
import { agruparPorGalpon, textoTituloGrupo } from './tareasPendientes';
import { TextoRecortado } from './TextoRecortado';
import { useTareasPendientesVacunacion } from './useTareasPendientesVacunacion';

// Bandeja del trabajador: solo pendientes vencidas y de hoy, por galpón.
// Online: completar exige conexión, y CompletarTareaDialog ya lo controla.
export function TareasVacunacionPage() {
  const [completando, setCompletando] = useState<TareaVacunacionResumen | null>(null);
  const { pendientes, cargando, error, reintentar } = useTareasPendientesVacunacion();
  const granjas = useQuery({ queryKey: CLAVE_GRANJAS, queryFn: listarGranjas });
  const granja = granjas.data?.[0];
  const galpones = useQuery({
    queryKey: ['avicola', 'galpones', granja?.id],
    queryFn: () => listarGalpones(granja!.id),
    enabled: Boolean(granja),
  });
  const grupos = agruparPorGalpon(pendientes, galpones.data ?? []);

  return (
    <Container maxWidth="sm" sx={{ py: 3 }}>
      <PaginaCabecera titulo="Tareas de vacunación" variante="h5" />
      <EstadoCarga
        cargando={cargando}
        error={error}
        mensajeError="No se pudieron cargar las tareas de vacunación."
        onReintentar={reintentar}
      >
        {grupos.length === 0 ? (
          <Typography color="text.secondary">No hay tareas de vacunación pendientes.</Typography>
        ) : (
          grupos.map((grupo) => {
            const idTitulo = `grupo-vacunacion-${grupo.galponId}`;
            return (
              <Box key={grupo.galponId} component="section" sx={{ mb: 3 }}>
                <Typography
                  id={idTitulo}
                  component="h2"
                  variant="subtitle1"
                  sx={{ fontWeight: 700, mb: 1 }}
                >
                  {textoTituloGrupo(grupo)}
                </Typography>
                <Box
                  component="ul"
                  aria-labelledby={idTitulo}
                  sx={{
                    listStyle: 'none',
                    p: 0,
                    m: 0,
                    display: 'flex',
                    flexDirection: 'column',
                    gap: 1.5,
                  }}
                >
                  {grupo.tareas.map((tarea) => {
                    const clasificacion = clasificarTarea(tarea.fechaProgramada);
                    const aplicacion = tarea.modoAplicacion ?? tarea.observacionesProgramadas;
                    return (
                      <Paper
                        key={tarea.id}
                        component="li"
                        variant="outlined"
                        sx={{ p: 2, display: 'flex', flexDirection: 'column', gap: 1 }}
                      >
                        <Chip
                          size="small"
                          label={clasificacion}
                          color={clasificacion === 'Vencida' ? 'error' : 'warning'}
                          sx={{ alignSelf: 'flex-start' }}
                        />
                        <TextoRecortado texto={tarea.vacuna} />
                        <Typography variant="body2" color="text.secondary">
                          {`Día ${tarea.edadDia} · Programada ${tarea.fechaProgramada}`}
                        </Typography>
                        {aplicacion && <TextoRecortado texto={aplicacion} variante="body2" />}
                        <Button
                          variant="contained"
                          onClick={() => setCompletando(tarea)}
                          sx={{ alignSelf: { xs: 'stretch', sm: 'flex-start' } }}
                        >
                          Completar
                        </Button>
                      </Paper>
                    );
                  })}
                </Box>
              </Box>
            );
          })
        )}
      </EstadoCarga>
      <CompletarTareaDialog
        key={completando?.id ?? 'ninguna'}
        tarea={completando}
        abierto={completando !== null}
        alCerrar={() => setCompletando(null)}
      />
    </Container>
  );
}
```

La `key` reinicia el formulario del diálogo para cada tarea.

- [ ] **3.4 Ruta.** En `paginasDiferidas.tsx`, tras `GalponPage`:

```tsx
export const TareasVacunacionPage = lazy(() =>
  import('../features/avicola/TareasVacunacionPage').then((modulo) => ({
    default: modulo.TareasVacunacionPage,
  })),
);
```

En `router.tsx`, importar `TareasVacunacionPage` junto a las demás páginas
diferidas y añadir, tras la ruta `/avicola/galpones/:galponId/eficiencia`:

```tsx
{
  path: '/avicola/tareas',
  element: (
    <ProtectedRoute>
      <RequiereRol roles={['Trabajador']}>
        <RequiereFuncionalidad funcionalidades={['Vacunacion']}>
          <Suspense fallback={<CargandoRuta />}>
            <TareasVacunacionPage />
          </Suspense>
        </RequiereFuncionalidad>
      </RequiereRol>
    </ProtectedRoute>
  ),
},
```

Ejecutar `npm --prefix web test -- TareasVacunacionPage`: verde.

---

## Tarea 4: aviso en la página del galpón

**Archivos:** `GalponPage.tsx`, `GalponPage.test.tsx`.

- [ ] **4.1 Rojo.** En `GalponPage.test.tsx`, parametrizar `prepararFetch`
  conservando los valores por defecto de las pruebas existentes:

```tsx
function prepararFetch(
  opciones: {
    rol?: 'Trabajador' | 'Cliente';
    funcionalidades?: string[];
    reglas?: Record<string, Response | Response[]>;
  } = {},
) {
  const {
    rol = 'Trabajador',
    funcionalidades = ['ProduccionHuevos', 'Mortalidad'],
    reglas = {},
  } = opciones;
  return fetchSimulado({
    // ...reglas actuales sin cambios, salvo en GET /api/identidad/me:
    //    rol, y funcionalidades tomadas de las opciones...
    ...reglas,
  });
}
```

Añadir el import `import { isoDesplazado, tareaVacunacion } from '../../test/vacunacion';`
y un `describe` nuevo:

```tsx
describe('GalponPage y la vacunación', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  const pidio = (fetchMock: ReturnType<typeof vi.fn>, ruta: string) =>
    fetchMock.mock.calls.some(([arg, init]) => {
      const req = arg instanceof Request ? arg : new Request(String(arg), init as RequestInit);
      return new URL(req.url).pathname === ruta;
    });

  test('el Trabajador ve solo un aviso con enlace a sus tareas, sin el historial', async () => {
    const fetchMock = prepararFetch({
      funcionalidades: ['ProduccionHuevos', 'Mortalidad', 'Vacunacion'],
      reglas: {
        'GET /api/vacunacion/tareas': respuesta(200, {
          vencidasYHoy: [
            tareaVacunacion('t1', 'g1', isoDesplazado(-1)),
            tareaVacunacion('t2', 'g1', isoDesplazado(0)),
            tareaVacunacion('t3', 'g9', isoDesplazado(0)),
            tareaVacunacion('t4', 'g1', isoDesplazado(1)),
          ],
          proximas: [],
        }),
      },
    });
    renderPagina();

    const enlace = await screen.findByRole('link', {
      name: '2 tareas pendientes en este galpón',
    });
    expect(enlace).toHaveAttribute('href', '/avicola/tareas');
    expect(screen.queryByRole('heading', { name: 'Vacunación' })).not.toBeInTheDocument();
    expect(pidio(fetchMock, '/api/galpones/g1/vacunacion/tareas')).toBe(false);
  });

  test('el Trabajador sin pendientes en el galpón no ve aviso', async () => {
    prepararFetch({
      funcionalidades: ['ProduccionHuevos', 'Mortalidad', 'Vacunacion'],
      reglas: {
        'GET /api/vacunacion/tareas': respuesta(200, {
          vencidasYHoy: [tareaVacunacion('t3', 'g9', isoDesplazado(0))],
          proximas: [],
        }),
      },
    });
    renderPagina();
    await screen.findByText('Galpón 3');
    await new Promise((resolver) => setTimeout(resolver, 50));
    expect(screen.queryByRole('link', { name: /pendiente/ })).not.toBeInTheDocument();
  });

  test('el Cliente conserva la sección de vacunación y no ve el aviso', async () => {
    prepararFetch({
      rol: 'Cliente',
      funcionalidades: ['ProduccionHuevos', 'Mortalidad', 'Vacunacion'],
      reglas: {
        'GET /api/galpones/g1/vacunacion/tareas': respuesta(200, [
          tareaVacunacion('t1', 'g1', isoDesplazado(0)),
        ]),
      },
    });
    renderPagina();
    expect(await screen.findByRole('heading', { name: 'Vacunación' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /pendiente/ })).not.toBeInTheDocument();
  });
});
```

Ejecutar `npm --prefix web test -- GalponPage`: las dos primeras pruebas nuevas
fallan. Las pruebas offline existentes siguen verdes.

- [ ] **4.2 Verde.** En `GalponPage.tsx`:

  1. Ajustar los imports: `Link` de MUI (`Link as EnlaceMui`), y
     `useTareasPendientesVacunacion` y `textoPendientesGalpon`.
  2. Tras `const { tieneRol } = useAuth();` declarar
     `const esTrabajador = tieneRol('Trabajador');`.
  3. La consulta del historial no se hace para el Trabajador:

```tsx
const tareasVacunacion = useQuery({
  queryKey: [...CLAVE_TAREAS_VACUNACION, galponId],
  queryFn: () => listarTareasVacunacion(galponId),
  // El trabajador no ve el historial: usa la campana y /avicola/tareas.
  enabled: Boolean(galponId) && puedeVacunacion && !esTrabajador,
});
const { pendientes: pendientesVacunacion } = useTareasPendientesVacunacion(esTrabajador);
const pendientesDelGalpon = pendientesVacunacion.filter((t) => t.galponId === galponId).length;
```

  4. Tras el `Box` de la cabecera, antes del `TextField` de la fecha:

```tsx
{esTrabajador && pendientesDelGalpon > 0 && (
  <Alert severity="warning" sx={{ mt: 2, py: 0 }}>
    <EnlaceMui component={Link} to="/avicola/tareas" color="inherit">
      {textoPendientesGalpon(pendientesDelGalpon)}
    </EnlaceMui>
  </Alert>
)}
```

  5. Cambiar la condición de la sección `{puedeVacunacion && (` por
     `{puedeVacunacion && !esTrabajador && (`.

Ejecutar `npm --prefix web test -- GalponPage`: verde.

---

## Tarea 5: la lista de galpones del trabajador sin tablas de vacunación

**Archivos:** `VacunacionNotificacion.tsx`, `VacunacionNotificacion.test.tsx`.

- [ ] **5.1 Rojo.** En `VacunacionNotificacion.test.tsx`, la primera prueba pasa
  a ejecutarse como Cliente: `fetchConSesion(['Vacunacion'], {...}, 'Cliente')`.
  Su título queda «el Cliente con la funcionalidad ve vencidas/hoy y próximas
  con el número de galpón». Añadir:

```tsx
test('el Trabajador no consulta ni ve la notificación: usa la campana', async () => {
  const fetchMock = fetchConSesion(['Vacunacion'], {}, 'Trabajador');
  renderNotificacion();

  await new Promise((resolve) => setTimeout(resolve, 50));
  const pidioTareas = fetchMock.mock.calls.some(([arg]) => {
    const req = arg instanceof Request ? arg : new Request(String(arg));
    return new URL(req.url).pathname === '/api/vacunacion/tareas';
  });
  expect(pidioTareas).toBe(false);
  expect(screen.queryByText(/Vacunación/)).not.toBeInTheDocument();
});
```

Ejecutar `npm --prefix web test -- VacunacionNotificacion`: la prueba nueva
falla.

- [ ] **5.2 Verde.** En `VacunacionNotificacion.tsx`:

```tsx
const puede = useFuncionalidad('Vacunacion');
const { rol } = useAuth();
// El trabajador ve sus tareas desde la campana de la cabecera; aquí verían
// también las próximas, que no deben mostrársele.
const visible = puede && rol !== 'Trabajador';
```

Usar `enabled: visible` en la consulta y `if (!visible) return null;` en lugar
de `if (!puede) return null;`. Ejecutar la prueba dirigida: verde.

---

## Tarea 6: documentación

- [ ] **6.1** En `docs/proyecto/gestion-avicola.md`, §4, tras «No hay jobs ni
  notificaciones push; la consulta es la fuente de verdad.», añadir:

```markdown
En el PWA, el trabajador con `Vacunacion` ve en la campana general de la
cabecera la sección «Vacunación», con una sola entrada-resumen («3 tareas
pendientes (1 vencida)»). Cuenta las pendientes vencidas y de hoy según la fecha
local, no se marca como leída y baja solo al completarlas. La entrada abre
`/avicola/tareas`, con esas tareas agrupadas por galpón y la acción Completar.
Al trabajador nunca se le muestran las próximas, y en el galpón solo ve un aviso
con enlace. El Cliente conserva la tabla de hoy, vencidas y próximas, y el
historial del galpón.
```

- [ ] **6.2** En `docs/proyecto/frontend-pwa.md`, tabla de rutas, tras la fila
  de `/avicola/galpones/:galponId/eficiencia`:

```markdown
| `/avicola/tareas` | Trabajador + Vacunacion | Tareas de vacunación vencidas y de hoy, por galpón; la abre la campana de la cabecera. |
```

- [ ] **6.3** En `docs/proyecto/frontend-pwa.md`, §5, tras la tabla de
  features, añadir:

```markdown
La cabecera tiene una campana general de notificaciones
(`app/notificaciones/`), visible para todo usuario con sesión. Su insignia suma
los pendientes de todas las fuentes y su menú se agrupa por secciones. Cada
fuente es un hook junto a su feature, con su propia clave de React Query y su
propia regla de visibilidad, y se registra en `useFuentesNotificaciones`.
Fuentes actuales: «Vacunación» (Trabajador con `Vacunacion`) y «Control de
acceso» (tenant con el módulo `ControlAcceso`). Las notificaciones de pedidos de
alimento y de despachos siguen en sus páginas.
```

- [ ] **6.4** Comprobar los enlaces y el mojibake con la puerta (tarea 7).

---

## Tarea 7: verificación

- [ ] `npm --prefix web run lint`
- [ ] `npm --prefix web run typecheck`
- [ ] `npm --prefix web test` (suite completa; incluye `offline.test.ts` y las
  pruebas de cola de `GalponPage.test.tsx`).
- [ ] `./verify.ps1` completo, con Docker corriendo, aunque el backend no cambie:
  la puerta es obligatoria para cambios de código.
- [ ] Registrar cada comando y su resultado con `registrar ... comando`.
- [ ] Dejar los cambios sin commitear y avanzar a `VERIFICACION`:
  `node quality/tarea.mjs avanzar 2026-10-30-gestor-veterinario-vacunacion VERIFICACION --rol desarrollador --proveedor <p>`.

## Prueba manual (la ejecuta el usuario)

Criterios 13 a 23 de `docs/ai/tareas/2026-10-30-gestor-veterinario-vacunacion/tarea.md`,
en móvil o con el emulador de dispositivo del navegador.
