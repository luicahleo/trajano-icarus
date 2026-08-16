import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import { registrarEventoFlujo, sanitizarRuta } from '../lib/sesionDiagnostico';

export function CapturaFlujoNavegacion() {
  const location = useLocation();

  useEffect(() => {
    registrarEventoFlujo({
      eventName: 'flow.navigation',
      detail: sanitizarRuta(location.pathname),
    });
  }, [location.pathname]);

  return null;
}
