import {
  Badge,
  IconButton,
  ListItemIcon,
  ListItemText,
  ListSubheader,
  Menu,
  MenuItem,
  Typography,
} from '@mui/material';
import NotificationsRoundedIcon from '@mui/icons-material/NotificationsRounded';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  etiquetaCampana,
  MENSAJE_ERROR_FUENTE,
  totalPendientes,
  type EntradaNotificacion,
} from './fuentes';
import { useFuentesNotificaciones } from './useFuentesNotificaciones';

// Campana general: no conoce ninguna fuente concreta.
export function CampanaNotificaciones() {
  const navigate = useNavigate();
  const [ancla, setAncla] = useState<HTMLElement | null>(null);
  const fuentes = useFuentesNotificaciones();
  const total = totalPendientes(fuentes);
  const secciones = fuentes.filter((f) => f.visible && (f.entradas.length > 0 || f.error));

  const cerrar = () => setAncla(null);

  const seleccionar = async (entrada: EntradaNotificacion) => {
    await entrada.alSeleccionar?.();
    cerrar();
    navigate(entrada.destino);
  };

  return (
    <>
      <IconButton
        color="inherit"
        aria-label={etiquetaCampana(total)}
        onClick={(evento) => setAncla(evento.currentTarget)}
      >
        {total > 0 ? (
          <Badge badgeContent={total} color="error">
            <NotificationsRoundedIcon />
          </Badge>
        ) : (
          <NotificationsRoundedIcon />
        )}
      </IconButton>
      <Menu
        anchorEl={ancla}
        open={Boolean(ancla)}
        onClose={cerrar}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { minWidth: 320 } } }}
      >
        {secciones.length === 0 && (
          <MenuItem disabled>
            <ListItemText primary="No hay notificaciones" />
          </MenuItem>
        )}
        {/* Arrays planos y no Fragment: Menu no admite Fragment como hijo. */}
        {secciones.flatMap((fuente) => [
          <ListSubheader key={`${fuente.id}-titulo`}>{fuente.seccion}</ListSubheader>,
          ...(fuente.error
            ? [
                <MenuItem key={`${fuente.id}-error`} disabled>
                  <ListItemText primary={MENSAJE_ERROR_FUENTE} />
                </MenuItem>,
              ]
            : []),
          ...fuente.entradas.map((entrada) => (
            <MenuItem
              key={`${fuente.id}-${entrada.id}`}
              onClick={() => void seleccionar(entrada)}
            >
              {entrada.icono && <ListItemIcon>{entrada.icono}</ListItemIcon>}
              <ListItemText
                primary={entrada.texto}
                secondary={
                  entrada.detalle && (
                    <Typography variant="caption" color="text.secondary">
                      {entrada.detalle}
                    </Typography>
                  )
                }
              />
            </MenuItem>
          )),
        ])}
      </Menu>
    </>
  );
}
