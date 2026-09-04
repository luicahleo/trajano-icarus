import { beforeEach, describe, expect, test, vi } from 'vitest';
import {
  borrarPedido,
  crearPedido,
  editarPedido,
  enviarPedido,
  listarNotificaciones,
  listarPedidos,
  marcarNotificacionLeida,
  obtenerCupo,
  obtenerPedido,
  obtenerPrecioVigente,
} from './api';

const r = (s: number, c: unknown) =>
  new Response(c === undefined ? null : JSON.stringify(c), {
    status: s,
    headers: { 'content-type': 'application/json' },
  });
const sinCuerpo = () => new Response(null, { status: 204 });
const solicitud = (f: ReturnType<typeof vi.fn>) => f.mock.calls.at(0)?.[0] as unknown as Request;

describe('api pedidos de alimento', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('listarPedidos consulta la bandeja del tenant', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => r(200, []));
    vi.stubGlobal('fetch', f);
    await listarPedidos();
    expect(solicitud(f).url).toContain('/api/pedidos-alimento');
  });

  test('obtenerPedido consulta el detalle', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => r(200, {}));
    vi.stubGlobal('fetch', f);
    await obtenerPedido('p1');
    expect(solicitud(f).url).toContain('/api/pedidos-alimento/p1');
  });

  test('crearPedido hace POST con las líneas', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => r(201, { id: 'p' }));
    vi.stubGlobal('fetch', f);
    await crearPedido({ detalles: [{ tipoAlimento: 'PosturaUno', presentacion: 'Bolsa', cantidad: 100 }] });
    const q = solicitud(f);
    expect(q.method).toBe('POST');
    const cuerpo = JSON.parse(await q.clone().text());
    expect(cuerpo.detalles[0]).toEqual({ tipoAlimento: 'PosturaUno', presentacion: 'Bolsa', cantidad: 100 });
  });

  test('editarPedido hace PUT', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => sinCuerpo());
    vi.stubGlobal('fetch', f);
    await editarPedido('p1', { detalles: [{ tipoAlimento: 'PosturaUno', presentacion: 'Bolsa', cantidad: 150 }] });
    expect(solicitud(f).method).toBe('PUT');
  });

  test('borrarPedido hace DELETE', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => sinCuerpo());
    vi.stubGlobal('fetch', f);
    await borrarPedido('p1');
    expect(solicitud(f).method).toBe('DELETE');
  });

  test('enviarPedido hace POST al envío', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => sinCuerpo());
    vi.stubGlobal('fetch', f);
    await enviarPedido('p1');
    const q = solicitud(f);
    expect(q.method).toBe('POST');
    expect(q.url).toContain('/api/pedidos-alimento/p1/enviar');
  });

  test('obtenerPrecioVigente y obtenerCupo consultan los endpoints del tenant', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => r(200, {}));
    vi.stubGlobal('fetch', f);
    await obtenerPrecioVigente();
    expect((f.mock.calls.at(0)?.[0] as unknown as Request).url).toContain(
      '/api/pedidos-alimento/precios-vigentes',
    );
    await obtenerCupo();
    expect((f.mock.calls.at(1)?.[0] as unknown as Request).url).toContain(
      '/api/pedidos-alimento/cupo',
    );
  });

  test('notificaciones: listado y marcado de lectura', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => r(200, { items: [], contador: 0 }));
    vi.stubGlobal('fetch', f);
    await listarNotificaciones();
    expect((f.mock.calls.at(0)?.[0] as unknown as Request).url).toContain(
      '/api/pedidos-alimento/notificaciones',
    );
    await marcarNotificacionLeida('n1');
    const q = f.mock.calls.at(1)?.[0] as unknown as Request;
    expect(q.method).toBe('POST');
    expect(q.url).toContain('/api/pedidos-alimento/notificaciones/n1/marcar-leida');
  });
});
