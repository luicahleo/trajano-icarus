import {
  diagnosticoManualPermitido,
  exportarDiagnostico,
  modoDiagnosticoActivo,
  obtenerEventosRecientes,
  obtenerSesionId,
  registrarEventoFlujo,
  sanitizarRuta,
} from './sesionDiagnostico';

describe('sesionDiagnostico', () => {
  beforeEach(() => {
    sessionStorage.clear();
    window.history.replaceState(null, '', '/');
    vi.restoreAllMocks();
  });

  test('genera y conserva un session ID opaco por pestaña', () => {
    const primero = obtenerSesionId();
    expect(primero).toMatch(/^SES-[0-9A-F]{12}$/);
    expect(obtenerSesionId()).toBe(primero);
  });

  test('activa el modo diagnóstico con ?debug=1 y lo conserva', () => {
    window.history.replaceState(null, '', '/?debug=1');
    expect(modoDiagnosticoActivo()).toBe(true);
    window.history.replaceState(null, '', '/inicio');
    expect(modoDiagnosticoActivo()).toBe(true);
  });

  test('ignora ?debug=1 cuando el entorno no permite el diagnóstico manual', () => {
    window.history.replaceState(null, '', '/?debug=1');

    expect(modoDiagnosticoActivo(false)).toBe(false);
    expect(sessionStorage.getItem('icarus.debug')).toBeNull();
  });

  test('permite diagnóstico en desarrollo o mediante opt-in explícito', () => {
    expect(
      diagnosticoManualPermitido({
        DEV: true,
        VITE_HABILITAR_DIAGNOSTICO_MANUAL: undefined,
      }),
    ).toBe(true);
    expect(
      diagnosticoManualPermitido({
        DEV: false,
        VITE_HABILITAR_DIAGNOSTICO_MANUAL: 'true',
      }),
    ).toBe(true);
    expect(
      diagnosticoManualPermitido({
        DEV: false,
        VITE_HABILITAR_DIAGNOSTICO_MANUAL: undefined,
      }),
    ).toBe(false);
  });

  test('mantiene un buffer circular de 100 eventos', () => {
    for (let i = 0; i < 101; i += 1) {
      registrarEventoFlujo({ eventName: 'flow.navigation', detail: `/ruta/${i}` });
    }

    const eventos = obtenerEventosRecientes(100);
    expect(eventos).toHaveLength(100);
    expect(eventos[0].detail).toBe('/ruta/1');
    expect(eventos[99].seq).toBe(101);
  });

  test('sanitiza identificadores numéricos y UUID sin query string', () => {
    expect(sanitizarRuta('/clientes/42/trabajadores/550e8400-e29b-41d4-a716-446655440000')).toBe(
      '/clientes/:id/trabajadores/:id',
    );
  });

  test('exporta solo el diagnóstico local', () => {
    const createObjectURL = vi.fn(() => 'blob:test');
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL });
    const click = vi.fn();
    vi.spyOn(document, 'createElement').mockReturnValue({
      click,
      href: '',
      download: '',
    } as unknown as HTMLAnchorElement);

    exportarDiagnostico();

    expect(createObjectURL).toHaveBeenCalledOnce();
    expect(click).toHaveBeenCalledOnce();
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:test');
  });
});
