import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Badge,
  IconButton,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
  Typography,
} from '@mui/material';
import NotificationsRoundedIcon from '@mui/icons-material/NotificationsRounded';
import MarkEmailReadRoundedIcon from '@mui/icons-material/MarkEmailReadRounded';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  listarNotificacionesAcceso,
  marcarNotificacionAccesoLeida,
  type NotificacionAccesoResumen,
} from './api';

const INTERVALO_REFRESCO_MS = 60_000;

function textoNotificacion(notificacion: NotificacionAccesoResumen): string {
  if (notificacion.tipo === 'IncidenciaAcceso') return 'Hay una incidencia de marcación por revisar';
  return 'Nueva notificación';
}

function horaResumida(fechaUtc: string): string {
  return new Intl.DateTimeFormat('es-BO', {
    timeZone: 'America/La_Paz',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(fechaUtc));
}

export function CampanaNotificaciones() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [ancla, setAncla] = useState<HTMLElement | null>(null);

  const { data } = useQuery({
    queryKey: ['control-acceso', 'notificaciones'],
    queryFn: listarNotificacionesAcceso,
    refetchInterval: INTERVALO_REFRESCO_MS,
  });

  const contador = data?.contador ?? 0;
  const items = data?.items ?? [];

  const abrir = (evento: React.MouseEvent<HTMLElement>) => {
    setAncla(evento.currentTarget);
  };

  const cerrar = () => {
    setAncla(null);
  };

  const seleccionar = async (notificacion: NotificacionAccesoResumen) => {
    await marcarNotificacionAccesoLeida(notificacion.id);
    void queryClient.invalidateQueries({ queryKey: ['control-acceso', 'notificaciones'] });
    cerrar();
    navigate('/control-acceso/incidencias');
  };

  return (
    <>
      <IconButton color="inherit" aria-label="Notificaciones" onClick={abrir}>
        <Badge badgeContent={contador} color="error">
          <NotificationsRoundedIcon />
        </Badge>
      </IconButton>
      <Menu
        anchorEl={ancla}
        open={Boolean(ancla)}
        onClose={cerrar}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { minWidth: 320 } } }}
      >
        {items.length === 0 && (
          <MenuItem disabled>
            <ListItemText primary="No hay notificaciones" />
          </MenuItem>
        )}
        {items.map((n) => (
          <MenuItem key={n.id} onClick={() => void seleccionar(n)}>
            <ListItemIcon>
              <MarkEmailReadRoundedIcon fontSize="small" />
            </ListItemIcon>
            <ListItemText
              primary={textoNotificacion(n)}
              secondary={
                <Typography variant="caption" color="text.secondary">
                  {horaResumida(n.fechaUtc)}
                </Typography>
              }
            />
          </MenuItem>
        ))}
      </Menu>
    </>
  );
}
