import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Container,
  TextField,
  Typography,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { useState } from 'react';
import { TablaDatos } from '../../app/ui/TablaDatos';
import type { Columna } from '../../app/ui/TablaDatos';
import type {
  EficienciaDia,
  MortalidadRegistro,
  RecogidaResumen,
  TareaVacunacionResumen,
} from '../../lib/tipos';
import type { OperacionPendiente } from '../../lib/offline/tipos';
import { ApiError } from '../../lib/http';
import { useFuncionalidad } from '../auth/useFuncionalidad';
import { descartarOperacion } from '../../app/offline/coordinador';
import { useOperacionesPendientes } from '../../app/offline/useOperacionesPendientes';
import {
  desactivarMortalidad,
  desactivarProduccion,
  listarMortalidad,
  listarProduccion,
  obtenerEficiencia,
  obtenerGalpon,
  listarTareasVacunacion,
  quitarPlanVacunacion,
} from './api';
import { CLAVE_NOTIFICACION_VACUNACION, CLAVE_TAREAS_VACUNACION, hoyIso } from './constantes';
import { formatearConteo, clasificarTarea, totalHuevos } from './formatos';
import { fusionarEventosDia, type Evento } from './eventosDia';
import { RegistrarBajasDialog } from './RegistrarBajasDialog';
import { RegistrarRecogidaDialog } from './RegistrarRecogidaDialog';
import { EditarBajasDialog } from './EditarBajasDialog';
import { EditarRecogidaDialog } from './EditarRecogidaDialog';
import { AsignarPlanDialog } from './AsignarPlanDialog';

function diaEficiencia(dias: EficienciaDia[] | undefined): EficienciaDia | undefined {
  return dias?.[0];
}

function etiquetaEstado(t: TareaVacunacionResumen): {
  etiqueta: string;
  color: 'default' | 'success' | 'error' | 'warning' | 'info';
} {
  if (t.estado === 'Completada') return { etiqueta: 'Completada', color: 'success' };
  if (t.estado === 'Cancelada') return { etiqueta: 'Cancelada', color: 'default' };
  const clasificacion = clasificarTarea(t.fechaProgramada);
  return {
    etiqueta: clasificacion,
    color: clasificacion === 'Vencida' ? 'error' : clasificacion === 'Hoy' ? 'warning' : 'info',
  };
}

export function GalponPage() {
  const { galponId = '' } = useParams();
  const [fecha, setFecha] = useState(hoyIso());
  const [registrandoBajas, setRegistrandoBajas] = useState(false);
  const [registrandoRecogida, setRegistrandoRecogida] = useState(false);
  const [recogidaEditada, setRecogidaEditada] = useState<RecogidaResumen | null>(null);
  const [bajasEditadas, setBajasEditadas] = useState<MortalidadRegistro | null>(null);
  const [recogidaPendiente, setRecogidaPendiente] = useState<OperacionPendiente | null>(null);
  const [bajasPendiente, setBajasPendiente] = useState<OperacionPendiente | null>(null);
  const [registroAEliminar, setRegistroAEliminar] = useState<Evento | null>(null);
  const [asignandoPlan, setAsignandoPlan] = useState(false);
  const queryClient = useQueryClient();
  const esHoy = fecha === hoyIso();
  // Operaciones aún en cola de este galpón: se muestran como filas del día.
  const pendientes = useOperacionesPendientes(galponId);
  const galpon = useQuery({
    queryKey: ['avicola', 'galpon', galponId],
    queryFn: () => obtenerGalpon(galponId),
    enabled: Boolean(galponId),
  });
  const puedeProduccion = useFuncionalidad('ProduccionHuevos');
  const puedeMortalidad = useFuncionalidad('Mortalidad');
  const puedeVacunacion = useFuncionalidad('Vacunacion');
  const puedeEstructura = useFuncionalidad('Galpones');
  const produccion = useQuery({
    queryKey: ['avicola', 'produccion', galponId, fecha],
    queryFn: () => listarProduccion(galponId, fecha),
    enabled: Boolean(galponId) && puedeProduccion,
  });
  const mortalidad = useQuery({
    queryKey: ['avicola', 'mortalidad', galponId, fecha],
    queryFn: () => listarMortalidad(galponId, fecha),
    enabled: Boolean(galponId) && puedeMortalidad,
  });
  const eficiencia = useQuery({
    queryKey: ['avicola', 'eficiencia', galponId, fecha, fecha],
    queryFn: () => obtenerEficiencia(galponId, fecha, fecha),
    enabled: Boolean(galponId) && puedeProduccion,
  });
  const tareasVacunacion = useQuery({
    queryKey: [...CLAVE_TAREAS_VACUNACION, galponId],
    queryFn: () => listarTareasVacunacion(galponId),
    enabled: Boolean(galponId) && puedeVacunacion,
  });
  const quitarPlan = useMutation({
    mutationFn: () => quitarPlanVacunacion(galponId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: CLAVE_TAREAS_VACUNACION });
      void queryClient.invalidateQueries({ queryKey: CLAVE_NOTIFICACION_VACUNACION });
    },
  });
  const eliminar = useMutation({
    mutationFn: (evento: Evento) =>
      evento.pendiente
        ? descartarOperacion(evento.pendiente.id) // nunca llegó al servidor
        : evento.tipo === 'recogida'
          ? desactivarProduccion(evento.datos.id)
          : desactivarMortalidad(evento.datos.id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['avicola', 'produccion'] });
      void queryClient.invalidateQueries({ queryKey: ['avicola', 'mortalidad'] });
      void queryClient.invalidateQueries({ queryKey: ['avicola', 'galpon'] });
      void queryClient.invalidateQueries({ queryKey: ['avicola', 'eficiencia'] });
      setRegistroAEliminar(null);
    },
  });

  if (
    galpon.isLoading ||
    (puedeProduccion && (produccion.isLoading || eficiencia.isLoading)) ||
    (puedeMortalidad && mortalidad.isLoading)
  ) {
    return (
      <Container sx={{ py: 3 }}>
        <CircularProgress aria-label="Cargando" />
      </Container>
    );
  }
  if (galpon.isError && galpon.error instanceof ApiError && galpon.error.status === 404) {
    return (
      <Container sx={{ py: 3 }}>
        <Alert severity="error">No se encontró el galpón.</Alert>
      </Container>
    );
  }
  const error =
    galpon.error ??
    (puedeProduccion ? (produccion.error ?? eficiencia.error) : null) ??
    (puedeMortalidad ? mortalidad.error : null);
  if (error) {
    return (
      <Container sx={{ py: 3 }}>
        <Alert
          severity="error"
          action={
            <Button
              onClick={() => {
                void galpon.refetch();
                void produccion.refetch();
                void mortalidad.refetch();
                void eficiencia.refetch();
              }}
            >
              Reintentar
            </Button>
          }
        >
          No se pudo cargar el galpón.
        </Alert>
      </Container>
    );
  }
  if (
    !galpon.data ||
    (puedeProduccion && !produccion.data) ||
    (puedeMortalidad && !mortalidad.data)
  )
    return null;

  const eventos = fusionarEventosDia(
    produccion.data?.recogidas ?? [],
    mortalidad.data?.registros ?? [],
    // Los pendientes son siempre del día en curso: no se muestran al consultar
    // otra fecha.
    esHoy ? pendientes : [],
  );
  const dia = diaEficiencia(eficiencia.data?.dias);
  const planVigente =
    (tareasVacunacion.data ?? []).find((t) => t.estado === 'Pendiente')?.programaNombre ?? null;

  const columnasRegistros: Columna<Evento>[] = [
    { clave: 'hora', encabezado: 'Hora', render: (e) => e.hora.slice(0, 5) },
    {
      clave: 'registro',
      encabezado: 'Registro',
      render: (e) => (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
          {e.tipo === 'bajas'
            ? `${e.datos.cantidadMuertas} bajas`
            : formatearConteo(e.datos.cantidadMaples, e.datos.unidadesIncompletas)}
          {e.pendiente && <Chip size="small" color="warning" label="Pendiente" />}
        </Box>
      ),
    },
    {
      clave: 'descarte',
      encabezado: 'Descarte',
      render: (e) => {
        if (e.tipo !== 'recogida') return null;
        // El pendiente no tiene totales de servidor: se calculan del cuerpo.
        const totalDescarte = e.pendiente
          ? totalHuevos(e.datos.maplesDescarte, e.datos.unidadesDescarte)
          : e.datos.totalDescarte;
        return totalDescarte > 0
          ? formatearConteo(e.datos.maplesDescarte, e.datos.unidadesDescarte)
          : null;
      },
    },
    {
      clave: 'acciones',
      encabezado: '',
      alinear: 'right',
      render: (e) => {
        // Los pendientes son datos locales: se editan y eliminan siempre.
        if (e.pendiente) {
          return (
            <>
              <Button
                size="small"
                onClick={() =>
                  e.tipo === 'recogida'
                    ? setRecogidaPendiente(e.pendiente)
                    : setBajasPendiente(e.pendiente)
                }
              >
                Editar
              </Button>
              <Button size="small" onClick={() => setRegistroAEliminar(e)}>
                Eliminar
              </Button>
            </>
          );
        }
        return esHoy &&
          ((e.tipo === 'recogida' && puedeProduccion) ||
            (e.tipo === 'bajas' && puedeMortalidad)) ? (
          <>
            <Button
              size="small"
              onClick={() =>
                e.tipo === 'recogida' ? setRecogidaEditada(e.datos) : setBajasEditadas(e.datos)
              }
            >
              Editar
            </Button>
            <Button size="small" onClick={() => setRegistroAEliminar(e)}>
              Eliminar
            </Button>
          </>
        ) : null;
      },
    },
  ];

  const columnasVacunas: Columna<TareaVacunacionResumen>[] = [
    { clave: 'vacuna', encabezado: 'Vacuna', render: (t) => t.vacuna },
    { clave: 'dia', encabezado: 'Día', render: (t) => `Día ${t.edadDia}` },
    { clave: 'programada', encabezado: 'Programada', render: (t) => t.fechaProgramada },
    { clave: 'aplicada', encabezado: 'Aplicada', render: (t) => t.fechaAplicacion ?? '—' },
    { clave: 'aves', encabezado: 'Aves', render: (t) => t.avesVacunadas ?? '—' },
    {
      clave: 'estado',
      encabezado: 'Estado',
      render: (t) => (
        <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: 0.5 }}>
          <Chip size="small" label={etiquetaEstado(t).etiqueta} color={etiquetaEstado(t).color} />
          {t.motivoCancelacion && (
            <Typography variant="caption" color="text.secondary">
              {t.motivoCancelacion}
            </Typography>
          )}
        </Box>
      ),
    },
  ];

  return (
    <Container sx={{ py: 2 }}>
      <Box
        sx={{
          display: 'flex',
          flexDirection: { xs: 'column', sm: 'row' },
          justifyContent: 'space-between',
          alignItems: { sm: 'center' },
          gap: 1,
        }}
      >
        <Box>
          <Typography variant="h4">Galpón {galpon.data.numero}</Typography>
          <Typography>
            {galpon.data.gallinasActuales} / {galpon.data.capacidadMaxima} gallinas
          </Typography>
        </Box>
        <Box sx={{ display: 'flex', flexDirection: 'row', gap: 1, alignItems: 'center' }}>
          {puedeProduccion && dia && (
            <Typography>{dia.eficiencia.toLocaleString('es-ES')} %</Typography>
          )}
          {puedeProduccion && dia?.bajoUmbral && (
            <Chip size="small" color="error" label="Bajo umbral — considerar descarte" />
          )}
          {puedeProduccion && (
            <Button component={Link} to={`/avicola/galpones/${galponId}/eficiencia`}>
              Ver eficiencia
            </Button>
          )}
        </Box>
      </Box>
      <TextField
        label="Fecha"
        type="date"
        value={fecha}
        onChange={(e) => setFecha(e.target.value)}
        slotProps={{ inputLabel: { shrink: true }, htmlInput: { max: hoyIso() } }}
        sx={{ mt: 2 }}
      />
      {!esHoy && (
        <Alert severity="info" sx={{ mt: 2 }}>
          Día sellado: no se puede corregir
        </Alert>
      )}
      {esHoy && (
        <Box sx={{ display: 'flex', flexDirection: 'row', gap: 1, mt: 2 }}>
          {puedeProduccion && (
            <Button variant="contained" onClick={() => setRegistrandoRecogida(true)}>
              Registrar recogida
            </Button>
          )}
          {puedeMortalidad && (
            <Button variant="contained" onClick={() => setRegistrandoBajas(true)}>
              Registrar bajas
            </Button>
          )}
        </Box>
      )}
      <Typography variant="h6" sx={{ mt: 3 }}>
        Total del día:{' '}
        {puedeProduccion
          ? `${produccion.data?.totalVendible ?? 0} huevos vendibles · ${produccion.data?.totalDescarte ?? 0} de descarte`
          : ''}
        {puedeProduccion && puedeMortalidad ? ' · ' : ''}
        {puedeMortalidad ? `${mortalidad.data?.totalMuertas ?? 0} bajas` : ''}
      </Typography>
      <TablaDatos
        columnas={columnasRegistros}
        filas={eventos}
        claveDeFila={(e) => `${e.tipo}-${e.pendiente ? e.pendiente.id : e.datos.id}`}
        mensajeVacio="Sin registros para la fecha seleccionada."
      />
      <RegistrarBajasDialog
        key={bajasPendiente?.id ?? 'nueva'}
        galponId={galponId}
        abierto={registrandoBajas || bajasPendiente !== null}
        pendiente={bajasPendiente}
        alCerrar={() => {
          setRegistrandoBajas(false);
          setBajasPendiente(null);
        }}
      />
      <RegistrarRecogidaDialog
        key={recogidaPendiente?.id ?? 'nueva'}
        galponId={galponId}
        abierto={registrandoRecogida || recogidaPendiente !== null}
        pendiente={recogidaPendiente}
        alCerrar={() => {
          setRegistrandoRecogida(false);
          setRecogidaPendiente(null);
        }}
      />
      <EditarRecogidaDialog
        recogida={recogidaEditada}
        abierto={recogidaEditada !== null}
        alCerrar={() => setRecogidaEditada(null)}
      />
      <EditarBajasDialog
        registro={bajasEditadas}
        abierto={bajasEditadas !== null}
        alCerrar={() => setBajasEditadas(null)}
      />
      <Dialog open={registroAEliminar !== null} onClose={() => setRegistroAEliminar(null)}>
        <DialogTitle>Eliminar registro</DialogTitle>
        <DialogContent>
          {registroAEliminar?.pendiente
            ? 'El registro aún no se ha sincronizado: se eliminará de este dispositivo.'
            : 'El registro se desactiva; no se borra. Si era una baja, las gallinas vuelven al inventario.'}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRegistroAEliminar(null)}>Cancelar</Button>
          <Button
            onClick={() => registroAEliminar && eliminar.mutate(registroAEliminar)}
            disabled={eliminar.isPending}
          >
            Confirmar
          </Button>
        </DialogActions>
      </Dialog>
      {puedeVacunacion && (
        <Box component="section" sx={{ mt: 4 }}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <Typography variant="h6">Vacunación</Typography>
            {puedeEstructura && (
              <Box sx={{ display: 'flex', gap: 1 }}>
                <Button size="small" variant="outlined" onClick={() => setAsignandoPlan(true)}>
                  Asignar plan
                </Button>
                {(tareasVacunacion.data ?? []).some((t) => t.estado === 'Pendiente') && (
                  <Button
                    size="small"
                    color="error"
                    onClick={() => quitarPlan.mutate()}
                    disabled={quitarPlan.isPending}
                  >
                    Quitar plan
                  </Button>
                )}
              </Box>
            )}
          </Box>
          <Typography
            variant="body2"
            color={planVigente ? 'text.primary' : 'text.secondary'}
            sx={{ my: 1 }}
          >
            {planVigente ? `Plan asignado: ${planVigente}` : 'Sin plan asignado'}
          </Typography>
          {tareasVacunacion.isError && (
            <Alert severity="error">No se pudo cargar la vacunación.</Alert>
          )}
          <TablaDatos
            columnas={columnasVacunas}
            filas={tareasVacunacion.data ?? []}
            claveDeFila={(t) => t.id}
            mensajeVacio="No hay tareas de vacunación."
          />
          <AsignarPlanDialog
            galponId={galponId}
            abierto={asignandoPlan}
            alCerrar={() => setAsignandoPlan(false)}
          />
        </Box>
      )}
    </Container>
  );
}
