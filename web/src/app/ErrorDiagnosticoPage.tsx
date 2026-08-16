import { Box, Button, Typography } from '@mui/material';
import { useEffect, useRef, useState } from 'react';
import { useRouteError } from 'react-router-dom';
import { crearErrorId, reportarDiagnostico, type ReporteroDiagnostico } from '../lib/diagnosticos';

const PATRON_CARGA_CHUNK =
  /Failed to fetch dynamically imported module|failed to fetch dynamically|Loading chunk|ChunkLoadError/i;

interface ClasificacionError {
  eventName: 'router.unexpected' | 'chunk.load_failed';
  category: 'unexpected' | 'chunk';
  source: 'router';
}

function clasificarError(error: unknown): ClasificacionError {
  if (error instanceof Error && PATRON_CARGA_CHUNK.test(error.message)) {
    return { eventName: 'chunk.load_failed', category: 'chunk', source: 'router' };
  }
  return { eventName: 'router.unexpected', category: 'unexpected', source: 'router' };
}

export function ErrorDiagnosticoPage({
  reportero = reportarDiagnostico,
}: {
  reportero?: ReporteroDiagnostico;
} = {}) {
  const error = useRouteError();
  const [errorId] = useState(() => crearErrorId());
  const reportado = useRef(false);

  useEffect(() => {
    if (reportado.current) return;
    reportado.current = true;
    void reportero({ errorId, ...clasificarError(error) });
  }, [error, errorId, reportero]);

  return (
    <Box sx={{ p: 4 }}>
      <Typography variant="h4">Algo salió mal</Typography>
      <Typography sx={{ color: 'text.secondary' }}>
        El incidente quedó registrado. Si necesitas ayuda, menciona la referencia{' '}
        <strong>{errorId}</strong>.
      </Typography>
      <Button variant="contained" sx={{ mt: 2 }} onClick={() => window.location.reload()}>
        Recargar
      </Button>
    </Box>
  );
}
