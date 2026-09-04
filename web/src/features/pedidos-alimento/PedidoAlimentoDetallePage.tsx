import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Divider,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import { DialogoConfirmacion } from '../../app/ui/DialogoConfirmacion';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import {
  borrarPedido,
  enviarPedido,
  obtenerPedido,
  type LineaPedidoDetalle,
} from './api';
import {
  COLOR_ESTADO,
  ETIQUETAS_ESTADO,
  ETIQUETAS_TIPO_ALIMENTO,
  formatoFecha,
  formatoMoneda,
} from './constantes';

// Detalle del pedido (spec SP8): precios congelados al enviar, historial
// completo de transiciones con motivos, y acciones solo en borrador. Abrir o
// leer el pedido nunca cambia su estado.
export function PedidoAlimentoDetallePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [confirmarEnvio, setConfirmarEnvio] = useState(false);
  const [confirmarBorrado, setConfirmarBorrado] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const { data: pedido, isLoading, isError } = useQuery({
    queryKey: ['pedidos-alimento', 'detalle', id],
    queryFn: () => obtenerPedido(id!),
    enabled: Boolean(id),
  });

  const refrescar = () => {
    queryClient.invalidateQueries({ queryKey: ['pedidos-alimento'] });
  };

  const enviar = useMutation({
    mutationFn: () => enviarPedido(id!),
    onSuccess: () => {
      setConfirmarEnvio(false);
      setError(null);
      refrescar();
    },
    onError: (e) => setError(e instanceof Error ? e.message : 'No se pudo enviar el pedido.'),
  });

  const borrar = useMutation({
    mutationFn: () => borrarPedido(id!),
    onSuccess: () => {
      refrescar();
      navigate('/pedidos');
    },
    onError: (e) => setError(e instanceof Error ? e.message : 'No se pudo borrar el borrador.'),
  });

  if (!pedido) {
    return (
      <Box sx={{ py: 3, px: 4 }}>
        <EstadoCarga cargando={isLoading} error={isError} mensajeError="No se pudo cargar el pedido." />
      </Box>
    );
  }

  const esBorrador = pedido.estado === 'Borrador';

  return (
    <Box sx={{ py: 3, px: { xs: 2, md: 4 } }}>
      <Stack direction="row" spacing={2} sx={{ mb: 2, alignItems: 'center' }}>
        <Chip
          label={ETIQUETAS_ESTADO[pedido.estado] ?? pedido.estado}
          color={COLOR_ESTADO[pedido.estado] ?? 'default'}
        />
        <Typography variant="h5" component="h1">
          Pedido {formatoFecha(pedido.fechaPedido ?? '')}
        </Typography>
      </Stack>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr 1fr', md: 'repeat(3, 1fr)' },
          gap: 2,
          mb: 2,
        }}
      >
        <Box>
          <Typography variant="body2" color="text.secondary">
            Fecha de pedido
          </Typography>
          <Typography>{pedido.fechaPedido ? formatoFecha(pedido.fechaPedido) : '—'}</Typography>
        </Box>
        <Box>
          <Typography variant="body2" color="text.secondary">
            Entrega estimada
          </Typography>
          <Typography>
            {pedido.fechaEntregaEstimada ? formatoFecha(pedido.fechaEntregaEstimada) : '—'}
          </Typography>
        </Box>
        <Box>
          <Typography variant="body2" color="text.secondary">
            Total solicitado
          </Typography>
          <Typography>
            {pedido.totalSolicitado === null ? '—' : formatoMoneda(pedido.totalSolicitado)}
          </Typography>
        </Box>
      </Box>

      {pedido.estado === 'Rechazado' && motivoDe(pedido.historial) && (
        <Alert severity="error" sx={{ mb: 2 }}>
          Rechazado por CAISY: {motivoDe(pedido.historial)}
        </Alert>
      )}
      {pedido.estado === 'Borrador' && motivoDe(pedido.historial) && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          CAISY devolvió este pedido: {motivoDe(pedido.historial)}. Corregilo y reenvialo.
        </Alert>
      )}

      <TableContainer component={Paper} sx={{ mb: 2 }}>
        <Table size="small" aria-label="Líneas del pedido">
          <TableHead>
            <TableRow>
              <TableCell>Tipo</TableCell>
              <TableCell>Presentación</TableCell>
              <TableCell align="right">Cantidad</TableCell>
              <TableCell align="right">Equivalentes de 40 kg</TableCell>
              <TableCell align="right">Precio por 40 kg</TableCell>
              <TableCell align="right">Subtotal</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {pedido.lineas.map((linea) => (
              <Linea key={linea.id} linea={linea} />
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {esBorrador && (
        <Stack direction="row" spacing={1} sx={{ mb: 3 }}>
          <Button
            variant="contained"
            component={RouterLink}
            to={`/pedidos/${pedido.id}/editar`}
          >
            Editar
          </Button>
          <Button variant="outlined" onClick={() => setConfirmarEnvio(true)}>
            Enviar a CAISY
          </Button>
          <Button variant="outlined" color="error" onClick={() => setConfirmarBorrado(true)}>
            Borrar borrador
          </Button>
        </Stack>
      )}

      <Typography variant="h6" component="h2" sx={{ mb: 1 }}>
        Historial
      </Typography>
      <Stack spacing={1} sx={{ mb: 3 }}>
        {pedido.historial.length === 0 && (
          <Typography variant="body2" color="text.secondary">
            Sin movimientos todavía.
          </Typography>
        )}
        {pedido.historial.map((t, i) => (
          <Paper key={i} variant="outlined" sx={{ p: 1.5 }}>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <Chip size="small" label={ETIQUETAS_ESTADO[t.estadoDestino] ?? t.estadoDestino} />
              <Typography variant="body2">
                {ETIQUETAS_ESTADO[t.estadoOrigen] ?? t.estadoOrigen} →{' '}
                {ETIQUETAS_ESTADO[t.estadoDestino] ?? t.estadoDestino}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {new Date(t.fechaUtc).toLocaleString('es-BO')}
              </Typography>
            </Stack>
            {t.motivo && (
              <Typography variant="body2" sx={{ mt: 0.5 }}>
                Motivo: {t.motivo}
              </Typography>
            )}
            {t.fechaEntregaEstimada && (
              <Typography variant="body2" sx={{ mt: 0.5 }}>
                Entrega estimada: {formatoFecha(t.fechaEntregaEstimada)}
              </Typography>
            )}
          </Paper>
        ))}
      </Stack>
      <Divider />

      <DialogoConfirmacion
        abierto={confirmarBorrado}
        titulo="Borrar borrador"
        mensaje="¿Borrar este borrador? Solo los borradores se pueden borrar."
        color="error"
        pendiente={borrar.isPending}
        onCancelar={() => setConfirmarBorrado(false)}
        onConfirmar={() => borrar.mutate()}
      />

      <Dialog open={confirmarEnvio} onClose={() => setConfirmarEnvio(false)}>
        <DialogTitle>Enviar pedido a CAISY</DialogTitle>
        <DialogContent>
          <DialogContentText component="div">
            <Typography variant="body2" sx={{ mb: 1 }}>
              Al enviar se fija la fecha de pedido (hoy) y se congelan los precios
              vigentes de todas las líneas. Esta acción consume el cupo semanal.
            </Typography>
            <Typography variant="body2">
              Total a enviar:{' '}
              <strong>
                {pedido.totalSolicitado === null
                  ? 'sin precios congelados'
                  : formatoMoneda(pedido.totalSolicitado)}
              </strong>
            </Typography>
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmarEnvio(false)}>Cancelar</Button>
          <Button
            variant="contained"
            onClick={() => enviar.mutate()}
            disabled={enviar.isPending || pedido.totalSolicitado === null}
          >
            Confirmar envío
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}

function Linea({ linea }: { linea: LineaPedidoDetalle }) {
  return (
    <TableRow>
      <TableCell>{ETIQUETAS_TIPO_ALIMENTO[linea.tipoAlimento] ?? linea.tipoAlimento}</TableCell>
      <TableCell>{linea.presentacion}</TableCell>
      <TableCell align="right">
        {linea.cantidadSolicitada} {linea.presentacion === 'Bolsa' ? 'bolsas' : 't'}
      </TableCell>
      <TableCell align="right">{linea.equivalentes40Kg}</TableCell>
      <TableCell align="right">
        {linea.precioFinalPor40Kg === null ? '—' : formatoMoneda(linea.precioFinalPor40Kg)}
      </TableCell>
      <TableCell align="right">
        {linea.subtotalSolicitado === null ? '—' : formatoMoneda(linea.subtotalSolicitado)}
      </TableCell>
    </TableRow>
  );
}

// El motivo de la última devolución o rechazo vive en el historial (spec SP8).
function motivoDe(historial: { estadoDestino: string; motivo: string | null }[]): string | null {
  const conMotivo = [...historial].reverse().find((t) => t.motivo !== null);
  return conMotivo?.motivo ?? null;
}
