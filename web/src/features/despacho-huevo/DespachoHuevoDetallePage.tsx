import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Box,
  Button,
  Chip,
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
import { useMemo, useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import { DialogoConfirmacion } from '../../app/ui/DialogoConfirmacion';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import {
  borrarDespacho,
  despacharDespacho,
  obtenerDespacho,
  obtenerPrecioHuevoVigente,
} from './api';
import {
  COLOR_ESTADO,
  ETIQUETAS_ESTADO,
  ETIQUETAS_TAMANO,
  formatoFecha,
  formatoMoneda,
  HUEVOS_POR_AMARRA,
} from './constantes';

// Compresión client-side (spec SP9B): pensada para conectividad rural, no
// reemplaza el reprocesamiento del servidor (que igual corrige orientación y
// quita metadatos). Si el navegador no soporta canvas, se sube el archivo
// original sin tocar y el servidor lo reprocesa igual.
async function comprimirImagen(archivo: File): Promise<File> {
  if (typeof document === 'undefined' || typeof createImageBitmap !== 'function') return archivo;
  try {
    const bitmap = await createImageBitmap(archivo);
    const ladoMaximo = 1600;
    const escala = Math.min(1, ladoMaximo / Math.max(bitmap.width, bitmap.height));
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(bitmap.width * escala);
    canvas.height = Math.round(bitmap.height * escala);
    const contexto = canvas.getContext('2d');
    if (!contexto) return archivo;
    contexto.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    const blob = await new Promise<Blob | null>((resolve) =>
      canvas.toBlob(resolve, 'image/jpeg', 0.7),
    );
    if (!blob) return archivo;
    return new File([blob], archivo.name, { type: 'image/jpeg' });
  } catch {
    return archivo;
  }
}

// Detalle del despacho (spec SP9B): cantidades por tamaño, precio y subtotal
// congelados solo tras despachar, y acciones solo en borrador. Despachar es
// irreversible: fija la fecha, congela los precios vigentes y adjunta la foto
// obligatoria de la nota de entrega.
export function DespachoHuevoDetallePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [confirmarDespacho, setConfirmarDespacho] = useState(false);
  const [confirmarBorrado, setConfirmarBorrado] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Foto de la nota de entrega (spec SP9B), obligatoria para despachar.
  const [fotoDespacho, setFotoDespacho] = useState<File | null>(null);

  const { data: despacho, isLoading, isError } = useQuery({
    queryKey: ['despachos-huevo', 'detalle', id],
    queryFn: () => obtenerDespacho(id!),
    enabled: Boolean(id),
  });

  // Publicación vigente: un borrador aún no tiene precios congelados (se
  // congelan al despachar), así que se estima el total con la vigente. Fuera
  // de borrador las líneas ya traen su snapshot y esta consulta no altera nada.
  const { data: preciosVigentes, isError: errorPrecios } = useQuery({
    queryKey: ['despachos-huevo', 'precios-vigentes'],
    queryFn: obtenerPrecioHuevoVigente,
  });

  const precioEstimadoDe = useMemo(() => {
    const indice = new Map(
      (preciosVigentes?.detalles ?? []).map((d) => [d.tamano, d.precioUnitario]),
    );
    return (tamano: string): number | null => indice.get(tamano) ?? null;
  }, [preciosVigentes]);

  const refrescar = () => {
    queryClient.invalidateQueries({ queryKey: ['despachos-huevo'] });
  };

  const despachar = useMutation({
    mutationFn: async () => {
      if (!fotoDespacho) throw new Error('Falta la foto de la nota de entrega.');
      return despacharDespacho(id!, fotoDespacho);
    },
    onSuccess: () => {
      setConfirmarDespacho(false);
      setFotoDespacho(null);
      setError(null);
      refrescar();
    },
    onError: (e) => setError(e instanceof Error ? e.message : 'No se pudo despachar.'),
  });

  const borrar = useMutation({
    mutationFn: () => borrarDespacho(id!),
    onSuccess: () => {
      refrescar();
      navigate('/despachos');
    },
    onError: (e) => setError(e instanceof Error ? e.message : 'No se pudo borrar el borrador.'),
  });

  if (!despacho) {
    return (
      <Box sx={{ py: 3, px: 4 }}>
        <EstadoCarga
          cargando={isLoading}
          error={isError}
          mensajeError="No se pudo cargar el despacho."
        />
      </Box>
    );
  }

  const esBorrador = despacho.estado === 'Borrador';

  // Total a despachar: el congelado si existe; si el borrador nunca se
  // despachó, la estimación con la publicación vigente (solo cuando cubre
  // todos los tamaños; si falta alguno, el despacho fallaría y se deja null).
  const totalEstimado =
    despacho.detalles.length > 0 &&
    despacho.detalles.every((d) => precioEstimadoDe(d.tamano) !== null)
      ? despacho.detalles.reduce(
          (acumulado, d) =>
            acumulado +
            (d.cantidadAmarras * HUEVOS_POR_AMARRA + d.unidadesSueltas) * precioEstimadoDe(d.tamano)!,
          0,
        )
      : null;
  const totalParaDespachar = despacho.totalBs ?? totalEstimado;
  const esEstimado = despacho.totalBs === null && totalEstimado !== null;

  return (
    <Box sx={{ py: 3, px: { xs: 2, md: 4 } }}>
      <Stack direction="row" spacing={2} sx={{ mb: 2, alignItems: 'center' }}>
        <Chip
          label={ETIQUETAS_ESTADO[despacho.estado] ?? despacho.estado}
          color={COLOR_ESTADO[despacho.estado] ?? 'default'}
        />
        <Typography variant="h5" component="h1">
          Despacho {despacho.fechaDespacho ? `del ${formatoFecha(despacho.fechaDespacho)}` : 'en borrador'}
        </Typography>
      </Stack>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}
      {esBorrador && errorPrecios && (
        <Alert severity="info" sx={{ mb: 2 }}>
          No hay publicación de precios de huevo vigente: no se puede despachar hasta que CAISY
          publique precios.
        </Alert>
      )}

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr 1fr', md: 'repeat(4, 1fr)' },
          gap: 2,
          mb: 2,
        }}
      >
        <Box>
          <Typography variant="body2" color="text.secondary">
            Fecha de despacho
          </Typography>
          <Typography>
            {despacho.fechaDespacho ? formatoFecha(despacho.fechaDespacho) : '—'}
          </Typography>
        </Box>
        <Box>
          <Typography variant="body2" color="text.secondary">
            Total amarras
          </Typography>
          <Typography>{despacho.totalAmarras}</Typography>
        </Box>
        <Box>
          <Typography variant="body2" color="text.secondary">
            Total huevos
          </Typography>
          <Typography>{despacho.totalHuevos}</Typography>
        </Box>
        <Box>
          <Typography variant="body2" color="text.secondary">
            Total
          </Typography>
          <Typography>
            {totalParaDespachar === null
              ? '—'
              : `${formatoMoneda(totalParaDespachar)}${esEstimado ? ' (estimado)' : ''}`}
          </Typography>
        </Box>
      </Box>

      <TableContainer component={Paper} sx={{ mb: 2 }}>
        <Table size="small" aria-label="Líneas del despacho">
          <TableHead>
            <TableRow>
              <TableCell>Tamaño</TableCell>
              <TableCell align="right">Cantidad Amarras</TableCell>
              <TableCell align="right">Unidades Sueltas</TableCell>
              <TableCell align="right">Total Huevos</TableCell>
              <TableCell align="right">Precio</TableCell>
              <TableCell align="right">Subtotal</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {despacho.detalles.map((detalle) => (
              <TableRow key={detalle.id}>
                <TableCell>{ETIQUETAS_TAMANO[detalle.tamano] ?? detalle.tamano}</TableCell>
                <TableCell align="right">{detalle.cantidadAmarras}</TableCell>
                <TableCell align="right">{detalle.unidadesSueltas}</TableCell>
                <TableCell align="right">{detalle.cantidadHuevos}</TableCell>
                {/* El precio unitario se congela al despachar: en borrador no se muestra. */}
                <TableCell align="right">
                  {detalle.precioUnitarioCongelado === null
                    ? '—'
                    : formatoMoneda(detalle.precioUnitarioCongelado)}
                </TableCell>
                <TableCell align="right">
                  {detalle.subtotal === null ? '—' : formatoMoneda(detalle.subtotal)}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {esBorrador && esEstimado && (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Precios estimados con la publicación vigente: se congelan al despachar.
        </Typography>
      )}

      {esBorrador && (
        <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
          <Typography variant="subtitle1" sx={{ mb: 1 }}>
            Despachar a CAISY
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            Adjuntá una foto de la nota de entrega: al confirmar se fija la fecha (hoy) y se
            congela el precio vigente por tamaño. Esta acción no se puede revertir.
          </Typography>
          <Button variant="outlined" component="label">
            {fotoDespacho
              ? `Foto elegida: ${fotoDespacho.name}`
              : 'Elegir foto de la nota de entrega'}
            <input
              type="file"
              accept="image/jpeg,image/png,image/webp"
              hidden
              onChange={async (e) => {
                const archivo = e.target.files?.[0];
                if (archivo) setFotoDespacho(await comprimirImagen(archivo));
              }}
            />
          </Button>
        </Paper>
      )}

      {esBorrador && (
        <Stack direction="row" spacing={1} sx={{ mb: 3 }}>
          <Button variant="contained" component={RouterLink} to={`/despachos/${despacho.id}/editar`}>
            Editar
          </Button>
          <Button
            variant="outlined"
            onClick={() => setConfirmarDespacho(true)}
            disabled={!fotoDespacho || errorPrecios}
          >
            Despachar a CAISY
          </Button>
          <Button variant="outlined" color="error" onClick={() => setConfirmarBorrado(true)}>
            Borrar borrador
          </Button>
        </Stack>
      )}

      <DialogoConfirmacion
        abierto={confirmarBorrado}
        titulo="Borrar borrador"
        mensaje="¿Borrar este borrador? Solo los borradores se pueden borrar."
        color="error"
        pendiente={borrar.isPending}
        onCancelar={() => setConfirmarBorrado(false)}
        onConfirmar={() => borrar.mutate()}
      />

      <DialogoConfirmacion
        abierto={confirmarDespacho}
        titulo="Despachar a CAISY"
        mensaje={
          <>
            <Typography variant="body2" sx={{ mb: 1 }}>
              Al despachar se fija la fecha (hoy), se congela el precio vigente de todos los
              tamaños y se adjunta la foto de la nota de entrega. Esta acción no se puede
              revertir.
            </Typography>
            <Typography variant="body2">
              Total a despachar:{' '}
              <strong>
                {totalParaDespachar === null
                  ? 'sin precios vigentes para todos los tamaños'
                  : `${formatoMoneda(totalParaDespachar)}${esEstimado ? ' (estimado, se congela al despachar)' : ''}`}
              </strong>
            </Typography>
          </>
        }
        pendiente={despachar.isPending}
        onCancelar={() => setConfirmarDespacho(false)}
        onConfirmar={() => despachar.mutate()}
      />
    </Box>
  );
}
