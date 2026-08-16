import { render } from '@testing-library/react';
import type { DiagnosticoFrontend } from '../lib/diagnosticos';
import { CapturaErroresGlobales } from './CapturaErroresGlobales';

describe('CapturaErroresGlobales', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  test('instala la captura global al montar y la desinstala al desmontar', () => {
    const reportero = vi.fn().mockResolvedValue(undefined);
    const { unmount } = render(<CapturaErroresGlobales reportero={reportero} />);

    window.dispatchEvent(new Event('error'));
    window.dispatchEvent(new Event('unhandledrejection'));

    expect(reportero).toHaveBeenCalledTimes(2);
    const nombres = reportero.mock.calls.map(([d]) => (d as DiagnosticoFrontend).eventName);
    expect(nombres).toContain('window.unexpected');
    expect(nombres).toContain('promise.unhandled');

    unmount();

    window.dispatchEvent(new Event('error'));
    window.dispatchEvent(new Event('unhandledrejection'));

    expect(reportero).toHaveBeenCalledTimes(2);
  });
});
