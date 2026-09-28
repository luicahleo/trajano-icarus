import { Box, CircularProgress } from '@mui/material';
import { useEffect, useState } from 'react';
import { ActivacionKioscoPage } from './ActivacionKioscoPage';
import { estadoSesionKiosco } from './apiKiosco';
import { MarcacionKioscoPage } from './MarcacionKioscoPage';

type Estado = 'cargando' | 'activacion' | 'marcacion';

export function AppKiosco() {
  const [estado, setEstado] = useState<Estado>('cargando');

  useEffect(() => {
    let activo = true;
    estadoSesionKiosco()
      .then((sesion) => {
        if (activo) setEstado(sesion ? 'marcacion' : 'activacion');
      })
      .catch(() => {
        if (activo) setEstado('activacion');
      });
    return () => {
      activo = false;
    };
  }, []);

  if (estado === 'cargando') {
    return (
      <Box
        sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '100dvh' }}
      >
        <CircularProgress aria-label="Cargando" />
      </Box>
    );
  }

  if (estado === 'activacion') {
    return <ActivacionKioscoPage onActivado={() => setEstado('marcacion')} />;
  }

  return <MarcacionKioscoPage />;
}
