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
  TextField,
  Typography,
} from '@mui/material';
import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import { DialogoConfirmacion } from '../../app/ui/DialogoConfirmacion';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../../lib/http';
import { AjustesCreditoHuevo } from '../despacho-huevo/AjustesCreditoHuevo';
import { obtenerBalanceCreditoHuevo } from '../despacho-huevo/api';
import {
  borrarPedido,
  enviarPedido,
  obtenerOriginalDocumentoNota,
  obtenerPedido,
  obtenerPrecioVigente,
  obtenerVistaDocumentoNota,
  recibirPedido,
  type DocumentoNota,
  type LineaPedidoDetalle,
} from './api';
import {
  COLOR_ESTADO,
  ETIQUETAS_ESTADO,
  ETIQUETAS_TIPO_ALIMENTO,
  formatoFecha,
  formatoMoneda,
  formatoMonedaExacta,
} from './constantes';

// Compresión client-side (spec SP8D): pensada para conectividad rural, no
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

// Detalle del pedido (spec SP8): precios congelados al enviar, historial
// completo de transiciones con motivos, y acciones solo en borrador. Abrir o
// leer el pedido nunca cambia su estado.
// Debe coincidir exactamente con el `title` que arma el backend para
// CreditoInsuficienteRequiereConfirmacionException (spec SP9E) — es el único
// mecanismo disponible hoy para distinguir este 409 de otros sin extender
// ApiError con campos específicos de un único caso de uso.
const TITULO_CREDITO_INSUFICIENTE = 'Crédito insuficiente';

export function PedidoAlimentoDetallePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { tieneRol } = useAuth();
  const esCliente = tieneRol('Cliente');
  const [confirmarEnvio, setConfirmarEnvio] = useState(false);
  const [requiereConfirmacionCredito, setRequiereConfirmacionCredito] = useState(false);
  const [confirmarBorrado, setConfirmarBorrado] = useState(false);
  const [confirmarRecepcion, setConfirmarRecepcion] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Cantidades realmente recibidas por tipo: precargadas con lo entregado.
  const [recibidas, setRecibidas] = useState<Record<string, string>>({});
  // Foto de la copia de la nota del receptor (spec SP8D), obligatoria.
  const [fotoRecepcion, setFotoRecepcion] = useState<File | null>(null);

  const { data: pedido, isLoading, isError } = useQuery({
    queryKey: ['pedidos-alimento', 'detalle', id],
    queryFn: () => obtenerPedido(id!),
    enabled: Boolean(id),
  });

  // Publicación vigente: un borrador nuevo aún no tiene precios congelados
  // (se congelan al enviar), así que se estiman con la vigente para mostrar
  // el total y habilitar el primer envío. Fuera de borrador las líneas ya
  // traen su snapshot y esta consulta no altera nada.
  const { data: preciosVigentes } = useQuery({
    queryKey: ['pedidos-alimento', 'precios-vigentes'],
    queryFn: obtenerPrecioVigente,
  });

  // Saldo actual del cliente, consultado solo cuando el primer intento de
  // envío ya avisó que hace falta confirmar (spec SP9E): evita duplicar en
  // el frontend el mismo cálculo que ya hace el backend.
  const { data: creditoParaConfirmar } = useQuery({
    queryKey: ['despachos-huevo', 'credito'],
    queryFn: obtenerBalanceCreditoHuevo,
    enabled: requiereConfirmacionCredito && esCliente,
  });

  const precioEstimadoDe = useMemo(() => {
    const indice = new Map(
      (preciosVigentes?.detalles ?? []).map((d) => [
        `${d.tipoAlimento}|${d.presentacion}`,
        d.precioFinalPor40Kg,
      ]),
    );
    return (linea: LineaPedidoDetalle): number | null =>
      linea.precioFinalPor40Kg ??
      indice.get(`${linea.tipoAlimento}|${linea.presentacion}`) ??
      null;
  }, [preciosVigentes]);

  const refrescar = () => {
    queryClient.invalidateQueries({ queryKey: ['pedidos-alimento'] });
  };

  const cerrarDialogoEnvio = () => {
    setConfirmarEnvio(false);
    setRequiereConfirmacionCredito(false);
  };

  const enviar = useMutation({
    mutationFn: (confirmarCreditoInsuficiente: boolean) =>
      enviarPedido(id!, confirmarCreditoInsuficiente),
    onSuccess: () => {
      setConfirmarEnvio(false);
      setRequiereConfirmacionCredito(false);
      setError(null);
      refrescar();
    },
    onError: (e) => {
      if (e instanceof ApiError && e.code === TITULO_CREDITO_INSUFICIENTE) {
        setRequiereConfirmacionCredito(true);
        return;
      }
      setError(e instanceof Error ? e.message : 'No se pudo enviar el pedido.');
    },
  });

  const borrar = useMutation({
    mutationFn: () => borrarPedido(id!),
    onSuccess: () => {
      refrescar();
      navigate('/pedidos');
    },
    onError: (e) => setError(e instanceof Error ? e.message : 'No se pudo borrar el borrador.'),
  });

  const recibir = useMutation({
    mutationFn: () => {
      if (!fotoRecepcion) throw new Error('Falta la foto de la nota recibida.');
      return recibirPedido(
        id!,
        pedido!.entrega!.lineas.map((l) => ({
          tipoAlimento: l.tipoAlimento,
          cantidadRecibida: Number(recibidas[l.tipoAlimento] ?? l.cantidadEntregada),
        })),
        fotoRecepcion,
      );
    },
    onSuccess: () => {
      setConfirmarRecepcion(false);
      setFotoRecepcion(null);
      setError(null);
      refrescar();
    },
    onError: (e) => setError(e instanceof Error ? e.message : 'No se pudo confirmar la recepción.'),
  });

  if (!pedido) {
    return (
      <Box sx={{ py: 3, px: 4 }}>
        <EstadoCarga cargando={isLoading} error={isError} mensajeError="No se pudo cargar el pedido." />
      </Box>
    );
  }

  const esBorrador = pedido.estado === 'Borrador';

  // Total a enviar: el congelado si existe; si el borrador nunca se envió, la
  // estimación con la publicación vigente (solo cuando cubre todas las
  // líneas; si falta alguna, el envío fallaría y se deja en null).
  const totalEstimado =
    pedido.lineas.length > 0 && pedido.lineas.every((l) => precioEstimadoDe(l) !== null)
      ? pedido.lineas.reduce(
          (acumulado, l) => acumulado + precioEstimadoDe(l)! * l.equivalentes40Kg,
          0,
        )
      : null;
  const totalParaEnviar = pedido.totalSolicitado ?? totalEstimado;
  const esEstimado = pedido.totalSolicitado === null && totalEstimado !== null;

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
            {totalParaEnviar === null
              ? '—'
              : `${formatoMoneda(totalParaEnviar)}${esEstimado ? ' (estimado)' : ''}`}
          </Typography>
        </Box>
      </Box>

      {pedido.entrega && (
        <>
          <Typography variant="h6" component="h2" sx={{ mb: 1 }}>
            Entrega y nota
          </Typography>
          <TableContainer component={Paper} sx={{ mb: 1 }}>
            <Table size="small" aria-label="Comparación de cantidades">
              <TableHead>
                <TableRow>
                  <TableCell>Tipo</TableCell>
                  <TableCell align="right">Solicitado</TableCell>
                  <TableCell align="right">Despachado</TableCell>
                  <TableCell align="right">Recibido</TableCell>
                  <TableCell align="right">Diferencia</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {pedido.lineas.map((linea) => {
                  const entregada =
                    pedido.entrega!.lineas.find((l) => l.tipoAlimento === linea.tipoAlimento)
                      ?.cantidadEntregada ?? null;
                  const recibida =
                    pedido.recepcion?.lineas.find((l) => l.tipoAlimento === linea.tipoAlimento)
                      ?.cantidadRecibida ?? null;
                  const diferencia =
                    entregada !== null && recibida !== null ? recibida - entregada : null;
                  return (
                    <TableRow key={linea.id}>
                      <TableCell>
                        {ETIQUETAS_TIPO_ALIMENTO[linea.tipoAlimento] ?? linea.tipoAlimento}
                      </TableCell>
                      <TableCell align="right">{linea.cantidadSolicitada}</TableCell>
                      <TableCell align="right">{entregada ?? '—'}</TableCell>
                      <TableCell align="right">{recibida ?? '—'}</TableCell>
                      <TableCell align="right">
                        {diferencia === null
                          ? '—'
                          : diferencia === 0
                            ? '0'
                            : diferencia > 0
                              ? `+${diferencia}`
                              : diferencia}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 2 }}>
            <Typography variant="body2">
              Nota <strong>{pedido.entrega.numeroNota}</strong> del{' '}
              {formatoFecha(pedido.entrega.fechaNota)}
            </Typography>
            <Typography variant="body2">
              Total despachado (canónico):{' '}
              <strong>{formatoMoneda(pedido.entrega.totalDespachado)}</strong>
            </Typography>
            <Typography variant="body2">
              Total informado (nota):{' '}
              {pedido.entrega.totalNetoInformado === null
                ? '—'
                : formatoMoneda(pedido.entrega.totalNetoInformado)}
            </Typography>
          </Stack>
          {pedido.entrega.documentos.length > 0 && (
            <Stack direction="row" spacing={2} sx={{ mb: 2, flexWrap: 'wrap' }}>
              {pedido.entrega.documentos.map((documento) => (
                <RespaldoNota key={documento.id} pedidoId={pedido.id} documento={documento} />
              ))}
            </Stack>
          )}
        </>
      )}

      {pedido.estado === 'Despachado' && pedido.entrega && (
        <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
          <Typography variant="subtitle1" sx={{ mb: 1 }}>
            Confirmar recepción
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            Registrá la cantidad realmente recibida por línea y adjuntá una foto de tu copia de
            la nota: si coincide todo, el pedido termina recibido conforme; con diferencias,
            queda el detalle para contraste.
          </Typography>
          <Stack spacing={1}>
            {pedido.entrega.lineas.map((linea) => (
              <Stack key={linea.tipoAlimento} direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                <Typography sx={{ minWidth: 160 }}>
                  {ETIQUETAS_TIPO_ALIMENTO[linea.tipoAlimento] ?? linea.tipoAlimento}
                </Typography>
                <Typography variant="body2" color="text.secondary" sx={{ minWidth: 110 }}>
                  Despachado: {linea.cantidadEntregada}
                </Typography>
                <TextField
                  size="small"
                  type="number"
                  label="Recibido"
                  value={recibidas[linea.tipoAlimento] ?? String(linea.cantidadEntregada)}
                  onChange={(e) =>
                    setRecibidas((previo) => ({
                      ...previo,
                      [linea.tipoAlimento]: e.target.value,
                    }))
                  }
                  slotProps={{ htmlInput: { min: 0, step: 1 } }}
                  sx={{ width: 130 }}
                />
              </Stack>
            ))}
            <Stack spacing={1} sx={{ mt: 2 }}>
              <Button variant="outlined" component="label">
                {fotoRecepcion
                  ? `Foto elegida: ${fotoRecepcion.name}`
                  : 'Elegir foto de la nota recibida'}
                <input
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  hidden
                  onChange={async (e) => {
                    const archivo = e.target.files?.[0];
                    if (archivo) setFotoRecepcion(await comprimirImagen(archivo));
                  }}
                />
              </Button>
            </Stack>
          </Stack>
          <Button
            variant="contained"
            onClick={() => setConfirmarRecepcion(true)}
            sx={{ mt: 2 }}
            disabled={!fotoRecepcion}
          >
            Confirmar recepción
          </Button>
        </Paper>
      )}

      {pedido.recepcion && (
        <Alert severity="success" sx={{ mb: 2 }}>
          Recepción confirmada el {formatoFecha(pedido.recepcion.fechaRecepcion)}: total recibido{' '}
          {formatoMoneda(pedido.recepcion.totalRecibido)}
          {pedido.recepcion.diferencias.length > 0
            ? ` con ${pedido.recepcion.diferencias.length} diferencia(s) contra lo despachado.`
            : ' sin diferencias contra lo despachado.'}
        </Alert>
      )}

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
              <Linea key={linea.id} linea={linea} precioEstimado={precioEstimadoDe(linea)} />
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {esBorrador && esEstimado && (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Precios estimados con la publicación vigente: se congelan al enviar.
        </Typography>
      )}

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

      <DialogoConfirmacion
        abierto={confirmarRecepcion}
        titulo="Confirmar recepción"
        mensaje="¿Confirmar la recepción con las cantidades indicadas? El pedido termina en Recibido conforme o Recibido con diferencias, y CAISY será notificada. No hay reapertura."
        pendiente={recibir.isPending}
        onCancelar={() => setConfirmarRecepcion(false)}
        onConfirmar={() => recibir.mutate()}
      />

      <Dialog open={confirmarEnvio} onClose={cerrarDialogoEnvio}>
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
                {totalParaEnviar === null
                  ? 'sin precios vigentes para todas las líneas'
                  : `${formatoMoneda(totalParaEnviar)}${esEstimado ? ' (estimado, se congela al enviar)' : ''}`}
              </strong>
            </Typography>
            {esCliente && requiereConfirmacionCredito && (
              <Alert severity="warning" sx={{ mt: 2 }}>
                {/* El crédito de huevo va con cuatro decimales (sale de precios
                    por huevo de cuatro decimales) y el total del pedido con dos,
                    que es lo exacto para los precios de alimento. */}
                Este pedido va a dejar tu crédito por despachos de huevo en{' '}
                {formatoMonedaExacta(
                  (creditoParaConfirmar?.saldoDisponible ?? 0) - (totalParaEnviar ?? 0),
                )}{' '}
                negativo (saldo actual{' '}
                {formatoMonedaExacta(creditoParaConfirmar?.saldoDisponible ?? 0)}, este pedido{' '}
                {formatoMoneda(totalParaEnviar ?? 0)}). ¿Confirmás el envío igual?
                <AjustesCreditoHuevo ajustes={creditoParaConfirmar?.ajustes ?? []} />
              </Alert>
            )}
            {!esCliente && requiereConfirmacionCredito && (
              <Alert severity="warning" sx={{ mt: 2 }}>
                Este pedido necesita confirmación del Cliente para continuar.
              </Alert>
            )}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={cerrarDialogoEnvio}>Cancelar</Button>
          {(esCliente || !requiereConfirmacionCredito) && (
            <Button
              variant="contained"
              onClick={() => enviar.mutate(requiereConfirmacionCredito)}
              disabled={enviar.isPending || totalParaEnviar === null}
            >
              {requiereConfirmacionCredito ? 'Enviar de todas formas' : 'Confirmar envío'}
            </Button>
          )}
        </DialogActions>
      </Dialog>
    </Box>
  );
}

function Linea({
  linea,
  precioEstimado,
}: {
  linea: LineaPedidoDetalle;
  precioEstimado: number | null;
}) {
  // Sin congelado (borrador nuevo) se muestra la estimación con la vigente.
  const precio = linea.precioFinalPor40Kg ?? precioEstimado;
  const subtotal =
    linea.subtotalSolicitado ??
    (precioEstimado === null ? null : precioEstimado * linea.equivalentes40Kg);
  return (
    <TableRow>
      <TableCell>{ETIQUETAS_TIPO_ALIMENTO[linea.tipoAlimento] ?? linea.tipoAlimento}</TableCell>
      <TableCell>{linea.presentacion}</TableCell>
      <TableCell align="right">
        {linea.cantidadSolicitada} {linea.presentacion === 'Bolsa' ? 'bolsas' : 't'}
      </TableCell>
      <TableCell align="right">{linea.equivalentes40Kg}</TableCell>
      <TableCell align="right">
        {precio === null ? '—' : formatoMoneda(precio)}
      </TableCell>
      <TableCell align="right">
        {subtotal === null ? '—' : formatoMoneda(subtotal)}
      </TableCell>
    </TableRow>
  );
}

// Respaldo del receptor (spec SP8C/SP8D): muestra la copia segura de
// visualización y ofrece el original como descarga autorizada (adjunto). La
// vista derivada se trae como blob autenticado y se libera al desmontar. El
// documento no admite sustitución: se crea una sola vez con la recepción.
function RespaldoNota({
  pedidoId,
  documento,
}: {
  pedidoId: string;
  documento: DocumentoNota;
}) {
  const [url, setUrl] = useState<string | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    let urlCreada: string | null = null;
    let cancelado = false;
    obtenerVistaDocumentoNota(pedidoId, documento.id)
      .then(({ blob }) => {
        if (cancelado) return;
        urlCreada = URL.createObjectURL(blob);
        setUrl(urlCreada);
      })
      .catch(() => setError(true));
    return () => {
      cancelado = true;
      if (urlCreada) URL.revokeObjectURL(urlCreada);
    };
  }, [pedidoId, documento.id]);

  const descargarOriginal = async () => {
    const { blob } = await obtenerOriginalDocumentoNota(pedidoId, documento.id);
    const urlOriginal = URL.createObjectURL(blob);
    const enlace = document.createElement('a');
    enlace.href = urlOriginal;
    enlace.download = documento.nombreSeguro;
    enlace.click();
    URL.revokeObjectURL(urlOriginal);
  };

  return (
    <Paper variant="outlined" sx={{ p: 1, width: 170 }}>
      {error ? (
        <Typography variant="caption" color="text.secondary">
          Sin vista previa
        </Typography>
      ) : url ? (
        <Box
          component="img"
          src={url}
          alt={`Respaldo de la nota: ${documento.nombreSeguro}`}
          sx={{ width: '100%', height: 120, objectFit: 'cover', borderRadius: 1 }}
        />
      ) : (
        <Box sx={{ width: '100%', height: 120, bgcolor: 'grey.100', borderRadius: 1 }} />
      )}
      <Typography variant="caption" sx={{ wordBreak: 'break-all', display: 'block' }}>
        {documento.nombreSeguro}
      </Typography>
      <Button size="small" onClick={() => void descargarOriginal()}>
        Descargar original
      </Button>
    </Paper>
  );
}

// El motivo de la última devolución o rechazo vive en el historial (spec SP8).
function motivoDe(historial: { estadoDestino: string; motivo: string | null }[]): string | null {
  const conMotivo = [...historial].reverse().find((t) => t.motivo !== null);
  return conMotivo?.motivo ?? null;
}
