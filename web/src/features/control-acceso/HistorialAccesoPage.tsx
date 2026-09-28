import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Button,
  Container,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import { ControlesPaginacion } from '../../app/ui/ControlesPaginacion';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import { PaginaCabecera } from '../../app/ui/PaginaCabecera';
import { TablaDatos } from '../../app/ui/TablaDatos';
import type { Columna } from '../../app/ui/TablaDatos';
import { ApiError } from '../../lib/http';
import type { JornadaAccesoResumen, ValorCorregido } from '../../lib/tipos';
import { useAuth } from '../auth/AuthContext';
import {
  corregirJornada,
  listarJornadas,
  listarTrabajadoresDeAcceso,
  obtenerJornada,
  registrarMarcacionManual,
} from './api';
import { CorreccionJornadaDialog } from './CorreccionJornadaDialog';
import { MarcacionManualDialog } from './MarcacionManualDialog';
import type { DatosMarcacionManual } from './MarcacionManualDialog';

const TAMANO_PAGINA = 20;

export function HistorialAccesoPage() {
  const { clienteId } = useAuth();
  const queryClient = useQueryClient();
  const [trabajadorId, setTrabajadorId] = useState('');
  const [desde, setDesde] = useState('');
  const [hasta, setHasta] = useState('');
  const [pagina, setPagina] = useState(1);
  const [seleccionada, setSeleccionada] = useState<JornadaAccesoResumen | null>(null);
  const [modo, setModo] = useState<'ver' | 'corregir' | null>(null);
  const [manualAbierto, setManualAbierto] = useState(false);
  const [aperturaManual, setAperturaManual] = useState(0);
  const [aperturaCorreccion, setAperturaCorreccion] = useState(0);
  const [errorCorreccion, setErrorCorreccion] = useState<string | null>(null);
  const [errorManual, setErrorManual] = useState<string | null>(null);

  const trabajadores = useQuery({
    queryKey: ['control-acceso', 'trabajadores', clienteId],
    queryFn: () => listarTrabajadoresDeAcceso(clienteId!),
    enabled: Boolean(clienteId),
  });
  const nombrePorId = new Map((trabajadores.data ?? []).map((t) => [t.id, t.nombre]));

  const filtros = {
    trabajadorId: trabajadorId || undefined,
    desde: desde || undefined,
    hasta: hasta || undefined,
  };
  const claveJornadas = ['control-acceso', 'jornadas', clienteId, filtros, pagina] as const;
  const jornadas = useQuery({
    queryKey: claveJornadas,
    queryFn: () => listarJornadas(filtros, { pagina, tamanoPagina: TAMANO_PAGINA }),
    enabled: Boolean(clienteId),
  });

  const detalle = useQuery({
    queryKey: ['control-acceso', 'jornada', seleccionada?.id],
    queryFn: () => obtenerJornada(seleccionada!.id),
    enabled: Boolean(seleccionada),
  });

  const refrescar = () => {
    void queryClient.invalidateQueries({ queryKey: ['control-acceso', 'jornadas'] });
    if (seleccionada) {
      void queryClient.invalidateQueries({ queryKey: ['control-acceso', 'jornada', seleccionada.id] });
    }
  };

  const correccion = useMutation({
    mutationFn: (datos: { motivo: string; valores: ValorCorregido[] }) =>
      corregirJornada(detalle.data!.id, {
        versionEsperada: detalle.data!.version,
        motivo: datos.motivo,
        valores: datos.valores,
      }),
    onSuccess: () => {
      setErrorCorreccion(null);
      setModo(null);
      refrescar();
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) {
        // No se sobrescribe: se avisa y se recarga la versión vigente.
        setErrorCorreccion('La jornada cambió mientras editabas; revisá y reintentá.');
        refrescar();
        return;
      }
      setErrorCorreccion(
        error instanceof ApiError ? (error.code ?? 'No se pudo corregir.') : 'No se pudo corregir.',
      );
    },
  });

  const manual = useMutation({
    mutationFn: (datos: DatosMarcacionManual) => registrarMarcacionManual(datos),
    onSuccess: () => {
      setErrorManual(null);
      setManualAbierto(false);
      refrescar();
    },
    onError: (error) =>
      setErrorManual(
        error instanceof ApiError
          ? (error.code ?? 'No se pudo registrar la marcación.')
          : 'No se pudo registrar la marcación.',
      ),
  });

  const columnas: Columna<JornadaAccesoResumen>[] = [
    { clave: 'fecha', encabezado: 'Fecha', render: (j) => j.fechaBoliviana },
    {
      clave: 'trabajador',
      encabezado: 'Trabajador',
      render: (j) => nombrePorId.get(j.trabajadorId) ?? '—',
    },
    { clave: 'estado', encabezado: 'Estado', render: (j) => j.estado },
    { clave: 'marcaciones', encabezado: 'Marcaciones', render: (j) => j.cantidadMarcaciones },
    {
      clave: 'acciones',
      encabezado: 'Acciones',
      alinear: 'right',
      render: (j) => (
        <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end' }}>
          <Button
            size="small"
            variant="outlined"
            onClick={() => {
              setSeleccionada(j);
              setModo('ver');
            }}
          >
            Ver
          </Button>
          <Button
            size="small"
            variant="outlined"
            onClick={() => {
              setErrorCorreccion(null);
              setSeleccionada(j);
              setModo('corregir');
              setAperturaCorreccion((n) => n + 1);
            }}
          >
            Corregir
          </Button>
        </Stack>
      ),
    },
  ];

  return (
    <Container maxWidth="lg" sx={{ py: 3 }}>
      <PaginaCabecera
        titulo="Historial de asistencia"
        acciones={
          <Button
            variant="contained"
            onClick={() => {
              setAperturaManual((n) => n + 1);
              setManualAbierto(true);
            }}
          >
            Registrar marcación manual
          </Button>
        }
      />

      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 2 }}>
        <TextField
          select
          label="Trabajador"
          size="small"
          value={trabajadorId}
          onChange={(evento) => {
            setTrabajadorId(evento.target.value);
            setPagina(1);
          }}
          sx={{ minWidth: 220 }}
        >
          <MenuItem value="">Todos</MenuItem>
          {(trabajadores.data ?? []).map((trabajador) => (
            <MenuItem key={trabajador.id} value={trabajador.id}>
              {trabajador.nombre}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          label="Desde"
          type="date"
          size="small"
          value={desde}
          onChange={(evento) => {
            setDesde(evento.target.value);
            setPagina(1);
          }}
          slotProps={{ inputLabel: { shrink: true } }}
        />
        <TextField
          label="Hasta"
          type="date"
          size="small"
          value={hasta}
          onChange={(evento) => {
            setHasta(evento.target.value);
            setPagina(1);
          }}
          slotProps={{ inputLabel: { shrink: true } }}
        />
      </Stack>

      <EstadoCarga
        cargando={jornadas.isLoading}
        error={jornadas.isError}
        mensajeError="No se pudo cargar el historial."
      >
        {jornadas.data && (
          <>
            <TablaDatos
              columnas={columnas}
              filas={jornadas.data.items}
              claveDeFila={(j) => j.id}
              mensajeVacio="No hay jornadas registradas todavía."
            />
            <div style={{ marginTop: 16 }}>
              <ControlesPaginacion
                numeroPagina={jornadas.data.numeroPagina}
                total={jornadas.data.total}
                tamanoPagina={jornadas.data.tamanoPagina}
                onCambiarPagina={setPagina}
              />
            </div>
          </>
        )}
      </EstadoCarga>

      <Dialog
        open={modo === 'ver' && Boolean(detalle.data)}
        onClose={() => setModo(null)}
        fullWidth
        maxWidth="sm"
      >
        <DialogTitle>Detalle de la jornada</DialogTitle>
        <DialogContent>
          {detalle.data && (
            <Stack spacing={1} sx={{ mt: 1 }}>
              <Typography variant="body2" color="text.secondary">
                {detalle.data.fechaBoliviana} · {detalle.data.estado}
              </Typography>
              {detalle.data.marcaciones.map((marcacion) => (
                <Typography key={marcacion.id}>
                  {marcacion.tipo} · {marcacion.instanteUtc} · {marcacion.origen}
                  {marcacion.motivo ? ` · ${marcacion.motivo}` : ''}
                </Typography>
              ))}
              {detalle.data.revisiones.length > 0 && (
                <Typography variant="body2" color="text.secondary">
                  {detalle.data.revisiones.length} corrección(es) registrada(s).
                </Typography>
              )}
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setModo(null)}>Cerrar</Button>
        </DialogActions>
      </Dialog>

      {modo === 'corregir' && detalle.data && (
        <CorreccionJornadaDialog
          key={`${detalle.data.id}-${detalle.data.version}-${aperturaCorreccion}`}
          abierto
          jornada={detalle.data}
          pendiente={correccion.isPending}
          error={errorCorreccion}
          onCancelar={() => setModo(null)}
          onGuardar={(motivo, valores) => correccion.mutate({ motivo, valores })}
        />
      )}

      <MarcacionManualDialog
        key={aperturaManual}
        abierto={manualAbierto}
        trabajadores={trabajadores.data ?? []}
        pendiente={manual.isPending}
        error={errorManual}
        onCancelar={() => setManualAbierto(false)}
        onGuardar={(datos) => manual.mutate(datos)}
      />
    </Container>
  );
}
