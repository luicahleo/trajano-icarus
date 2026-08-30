import { Alert } from '@mui/material';
import { useConexion } from './useConexion';
import { usePendientesOffline } from './offline/usePendientesOffline';

export function BannerSinConexion() {
  const online = useConexion();
  const pendientes = usePendientesOffline();
  if (online) return null;
  const conteo =
    pendientes === 0
      ? ''
      : pendientes === 1
        ? ' 1 registro pendiente de sincronizar.'
        : ` ${pendientes} registros pendientes de sincronizar.`;
  return (
    <Alert severity="warning" sx={{ borderRadius: 0 }}>
      Sin conexión: los registros se guardan en este dispositivo y se sincronizarán al volver la
      red.{conteo}
    </Alert>
  );
}
