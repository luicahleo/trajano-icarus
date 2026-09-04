import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
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
import DoneAllRoundedIcon from '@mui/icons-material/DoneAllRounded';
import AddRoundedIcon from '@mui/icons-material/AddRounded';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import { PaginaCabecera } from '../../app/ui/PaginaCabecera';
import { TablaDatos } from '../../app/ui/TablaDatos';
import type { Columna } from '../../app/ui/TablaDatos';
import {
  listarNotificaciones,
  listarPedidos,
  marcarNotificacionLeida,
  obtenerCupo,
  type PedidoResumen,
} from './api';
import {
  COLOR_ESTADO,
  ETIQUETAS_ESTADO,
  formatoFecha,
  formatoMoneda,
  mensajeNotificacion,
} from './constantes';

// Bandeja compartida del tenant (spec SP8): todos los usuarios del tenant con
// la función ven los mismos pedidos. Deliberadamente online.
export function PedidosAlimentoPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: pedidos, isLoading, isError } = useQuery({
    queryKey: ['pedidos-alimento'],
    queryFn: listarPedidos,
  });

  const { data: cupo } = useQuery({ queryKey: ['pedidos-alimento', 'cupo'], queryFn: obtenerCupo });

  const { data: notificaciones } = useQuery({
    queryKey: ['pedidos-alimento', 'notificaciones'],
    queryFn: listarNotificaciones,
  });

  const marcarLeida = useMutation({
    mutationFn: marcarNotificacionLeida,
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['pedidos-alimento', 'notificaciones'] }),
  });

  const columnas: Columna<PedidoResumen>[] = [
    {
      clave: 'estado',
      encabezado: 'Estado',
      render: (p) => (
        <Chip
          size="small"
          label={ETIQUETAS_ESTADO[p.estado] ?? p.estado}
          color={COLOR_ESTADO[p.estado] ?? 'default'}
        />
      ),
    },
    {
      clave: 'fechaPedido',
      encabezado: 'Enviado',
      render: (p) => (p.fechaPedido ? formatoFecha(p.fechaPedido) : '—'),
    },
    { clave: 'lineas', encabezado: 'Líneas', render: (p) => p.cantidadLineas },
    { clave: 'presentacion', encabezado: 'Presentación', render: (p) => p.presentacion },
    {
      clave: 'total',
      encabezado: 'Total solicitado',
      alinear: 'right',
      render: (p) =>
        p.totalSolicitado === null ? '—' : formatoMoneda(p.totalSolicitado),
    },
    {
      clave: 'entrega',
      encabezado: 'Entrega estimada',
      render: (p) => (p.fechaEntregaEstimada ? formatoFecha(p.fechaEntregaEstimada) : '—'),
    },
    {
      clave: 'acciones',
      encabezado: 'Acciones',
      alinear: 'right',
      render: (p) => (
        <Button size="small" component={RouterLink} to={`/pedidos/${p.id}`}>
          Ver
        </Button>
      ),
    },
  ];

  const sinLeer = notificaciones?.items.filter((n) => !n.leida) ?? [];

  return (
    <Box sx={{ py: 3, px: { xs: 2, md: 4 } }}>
      <PaginaCabecera
        titulo="Pedidos de alimento"
        acciones={
          <Button
            variant="contained"
            startIcon={<AddRoundedIcon />}
            component={RouterLink}
            to="/pedidos/nuevo"
          >
            Nuevo pedido
          </Button>
        }
      />

      <EstadoCarga
        cargando={isLoading}
        error={isError}
        mensajeError="No se pudo cargar la lista de pedidos."
      >
        <Stack spacing={2}>
          {cupo && (
            <Alert
              severity={cupo.enviados >= cupo.maximo ? 'warning' : 'info'}
              sx={{ alignItems: 'center' }}
            >
              Cupo semanal: {cupo.enviados} de {cupo.maximo} pedidos enviados.
            </Alert>
          )}

          {sinLeer.length > 0 && (
            <Paper variant="outlined" sx={{ mb: 1 }}>
              <Box sx={{ px: 2, pt: 1.5 }}>
                <Typography variant="subtitle2">
                  Novedades de CAISY ({notificaciones?.contador})
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
                    onClick={() => navigate(`/pedidos/${n.pedidoId}`)}
                    sx={{ cursor: 'pointer' }}
                  >
                    <ListItemText
                      primary={mensajeNotificacion(n)}
                      secondary={new Date(n.fechaUtc).toLocaleString('es-BO')}
                    />
                  </ListItem>
                ))}
              </List>
            </Paper>
          )}

          <TablaDatos
            columnas={columnas}
            filas={pedidos ?? []}
            claveDeFila={(p) => p.id}
            mensajeVacio="No hay pedidos todavía. Creá el primero."
            etiqueta="Pedidos de alimento"
          />

          <Divider />
          <Typography variant="body2" color="text.secondary">
            El pedido es compartido del tenant: cualquiera con la función puede
            verlo, editarlo o enviarlo mientras esté en borrador. El envío congela
            los precios vigentes y consume el cupo semanal.
          </Typography>
        </Stack>
      </EstadoCarga>
    </Box>
  );
}
