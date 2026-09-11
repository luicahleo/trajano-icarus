import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Box,
  Button,
  Chip,
  Divider,
  IconButton,
  List,
  ListItem,
  ListItemText,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import AddRoundedIcon from '@mui/icons-material/AddRounded';
import DoneAllRoundedIcon from '@mui/icons-material/DoneAllRounded';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import { PaginaCabecera } from '../../app/ui/PaginaCabecera';
import { TablaDatos } from '../../app/ui/TablaDatos';
import type { Columna } from '../../app/ui/TablaDatos';
import {
  listarDespachos,
  listarNotificacionesDespachoHuevo,
  marcarNotificacionDespachoHuevoLeida,
  type DespachoHuevoResumen,
} from './api';
import {
  COLOR_ESTADO,
  ETIQUETAS_ESTADO,
  formatoFecha,
  formatoMoneda,
  mensajeNotificacionDespachoHuevo,
} from './constantes';

// Bandeja compartida del tenant (spec SP9B): todos los usuarios del tenant con
// la funcionalidad ven los mismos despachos. Deliberadamente online.
export function DespachosHuevoPage() {
  const { data: despachos, isLoading, isError } = useQuery({
    queryKey: ['despachos-huevo'],
    queryFn: listarDespachos,
  });

  const navigate = useNavigate();
  const queryClient = useQueryClient();

  // La bandeja de novedades ya se llenaba desde SP9C y ninguna pantalla la
  // mostraba (spec SP9F). El backend filtra por rol qué tipos devuelve: el
  // Trabajador no recibe los financieros.
  //
  // Deliberadamente sin `isError`: si la consulta falla, `notificaciones`
  // queda undefined, `sinLeer` vacío y el bloque no se renderiza, así que la
  // tabla de despachos —lo principal de la pantalla— sigue viva. La
  // degradación sale del patrón, no de código defensivo.
  const { data: notificaciones } = useQuery({
    queryKey: ['despachos-huevo', 'notificaciones'],
    queryFn: listarNotificacionesDespachoHuevo,
  });

  const marcarLeida = useMutation({
    mutationFn: marcarNotificacionDespachoHuevoLeida,
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['despachos-huevo', 'notificaciones'] }),
  });

  const sinLeer = (notificaciones?.items ?? []).filter((n) => !n.leida);

  const columnas: Columna<DespachoHuevoResumen>[] = [
    {
      clave: 'estado',
      encabezado: 'Estado',
      render: (d) => (
        <Chip
          size="small"
          label={ETIQUETAS_ESTADO[d.estado] ?? d.estado}
          color={COLOR_ESTADO[d.estado] ?? 'default'}
        />
      ),
    },
    {
      clave: 'fechaDespacho',
      encabezado: 'Fecha de despacho',
      render: (d) => (d.fechaDespacho ? formatoFecha(d.fechaDespacho) : '—'),
    },
    { clave: 'amarras', encabezado: 'Amarras', alinear: 'right', render: (d) => d.totalAmarras },
    { clave: 'huevos', encabezado: 'Huevos', alinear: 'right', render: (d) => d.totalHuevos },
    {
      clave: 'total',
      encabezado: 'Total',
      alinear: 'right',
      render: (d) => (d.totalBs === null ? '—' : formatoMoneda(d.totalBs)),
    },
    {
      clave: 'acciones',
      encabezado: 'Acciones',
      alinear: 'right',
      render: (d) => (
        <Button size="small" component={RouterLink} to={`/despachos/${d.id}`}>
          Ver
        </Button>
      ),
    },
  ];

  return (
    <Box sx={{ py: 3, px: { xs: 2, md: 4 } }}>
      <PaginaCabecera
        titulo="Despachos de huevo"
        acciones={
          <Button
            variant="contained"
            startIcon={<AddRoundedIcon />}
            component={RouterLink}
            to="/despachos/nuevo"
          >
            Nuevo despacho
          </Button>
        }
      />

      <EstadoCarga
        cargando={isLoading}
        error={isError}
        mensajeError="No se pudo cargar la lista de despachos."
      >
        <Stack spacing={2}>
          {sinLeer.length > 0 && (
            <Paper variant="outlined" sx={{ mb: 1 }}>
              <Box sx={{ px: 2, pt: 1.5 }}>
                <Typography variant="subtitle2">
                  Novedades del despacho de huevo ({notificaciones?.contador})
                </Typography>
              </Box>
              <List dense>
                {sinLeer.slice(0, 5).map((n) => (
                  <ListItem
                    key={n.id}
                    secondaryAction={
                      <IconButton
                        edge="end"
                        aria-label="Marcar como leída"
                        onClick={() => marcarLeida.mutate(n.id)}
                        size="small"
                      >
                        <DoneAllRoundedIcon fontSize="small" />
                      </IconButton>
                    }
                    onClick={
                      n.despachoHuevoId
                        ? () => navigate(`/despachos/${n.despachoHuevoId}`)
                        : undefined
                    }
                    sx={{ cursor: n.despachoHuevoId ? 'pointer' : 'default' }}
                  >
                    <ListItemText
                      primary={mensajeNotificacionDespachoHuevo(n.tipo)}
                      secondary={
                        n.meta
                          ? `${n.meta} · ${new Date(n.fechaUtc).toLocaleString('es-BO')}`
                          : new Date(n.fechaUtc).toLocaleString('es-BO')
                      }
                    />
                  </ListItem>
                ))}
              </List>
            </Paper>
          )}

          <TablaDatos
            columnas={columnas}
            filas={despachos ?? []}
            claveDeFila={(d) => d.id}
            mensajeVacio="No hay despachos todavía. Creá el primero."
            etiqueta="Despachos de huevo"
          />

          <Divider />
          <Typography variant="body2" color="text.secondary">
            El despacho es compartido del tenant: cualquiera con la función puede verlo,
            editarlo o despacharlo mientras esté en borrador. El despacho fija la fecha,
            congela los precios vigentes por tamaño y exige la foto de la nota de entrega.
          </Typography>
        </Stack>
      </EstadoCarga>
    </Box>
  );
}
