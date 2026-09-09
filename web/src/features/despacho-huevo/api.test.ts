import { beforeEach, describe, expect, test, vi } from 'vitest';
import {
  borrarDespacho,
  crearDespacho,
  despacharDespacho,
  editarDespacho,
  listarDespachos,
  obtenerDespacho,
  obtenerPrecioHuevoVigente,
} from './api';

const r = (s: number, c: unknown) =>
  new Response(c === undefined ? null : JSON.stringify(c), {
    status: s,
    headers: { 'content-type': 'application/json' },
  });
const sinCuerpo = () => new Response(null, { status: 204 });
const solicitud = (f: ReturnType<typeof vi.fn>) => f.mock.calls.at(0)?.[0] as unknown as Request;

describe('api despachos de huevo', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('listarDespachos consulta la bandeja del tenant', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => r(200, []));
    vi.stubGlobal('fetch', f);
    await listarDespachos();
    expect(solicitud(f).url).toContain('/api/despachos-huevo');
  });

  test('obtenerDespacho consulta el detalle', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => r(200, {}));
    vi.stubGlobal('fetch', f);
    await obtenerDespacho('h1');
    expect(solicitud(f).url).toContain('/api/despachos-huevo/h1');
  });

  test('crearDespacho hace POST con las líneas por tamaño', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => r(201, { id: 'h' }));
    vi.stubGlobal('fetch', f);
    await crearDespacho({
      lineas: [{ tamano: 'Extra', cantidadAmarras: 2, unidadesSueltas: 30 }],
    });
    const q = solicitud(f);
    expect(q.method).toBe('POST');
    const cuerpo = JSON.parse(await q.clone().text());
    expect(cuerpo.lineas[0]).toEqual({
      tamano: 'Extra',
      cantidadAmarras: 2,
      unidadesSueltas: 30,
    });
  });

  test('editarDespacho hace PUT y borrarDespacho hace DELETE', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => sinCuerpo());
    vi.stubGlobal('fetch', f);
    await editarDespacho('h1', { lineas: [{ tamano: 'Extra', cantidadAmarras: 3, unidadesSueltas: 0 }] });
    expect(solicitud(f).method).toBe('PUT');
    await borrarDespacho('h1');
    const q = f.mock.calls.at(1)?.[0] as unknown as Request;
    expect(q.method).toBe('DELETE');
    expect(q.url).toContain('/api/despachos-huevo/h1');
  });

  test('despacharDespacho envía multipart con la foto obligatoria de la nota', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => sinCuerpo());
    vi.stubGlobal('fetch', f);
    const archivo = new File(['imagen'], 'nota-entrega.jpg', { type: 'image/jpeg' });
    await despacharDespacho('h1', archivo);
    const q = solicitud(f);
    expect(q.method).toBe('POST');
    expect(q.url).toContain('/api/despachos-huevo/h1/despachar');
    // FormData: el navegador fija el boundary multipart, no se serializa JSON.
    expect(q.headers.get('content-type')).toContain('multipart/form-data');
    const cuerpo = await q.clone().text();
    expect(cuerpo).toContain('name="archivo"');
    expect(cuerpo).toContain('Content-Type: image/jpeg');
  });

  test('obtenerPrecioHuevoVigente consulta el endpoint del tenant', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => r(200, {}));
    vi.stubGlobal('fetch', f);
    await obtenerPrecioHuevoVigente();
    expect(solicitud(f).url).toContain('/api/despachos-huevo/precios-vigentes');
  });
});
