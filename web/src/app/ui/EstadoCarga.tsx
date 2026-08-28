import { Alert, Box, Button, CircularProgress } from '@mui/material';
import type { ReactNode } from 'react';

interface EstadoCargaProps {
  cargando: boolean;
  error: boolean;
  mensajeError?: string;
  onReintentar?: () => void;
  children: ReactNode;
}

export function EstadoCarga({
  cargando,
  error,
  mensajeError = 'No se pudo cargar la información.',
  onReintentar,
  children,
}: EstadoCargaProps) {
  if (cargando) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
        <CircularProgress aria-label="Cargando" />
      </Box>
    );
  }
  if (error) {
    return (
      <Alert
        severity="error"
        action={
          onReintentar ? (
            <Button color="inherit" size="small" onClick={onReintentar}>
              Reintentar
            </Button>
          ) : undefined
        }
      >
        {mensajeError}
      </Alert>
    );
  }
  return <>{children}</>;
}
