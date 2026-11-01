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
