import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
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
import { useAuth } from '../auth/AuthContext';
import {
  descartarIncidenciaAcceso,
  listarIncidenciasAcceso,
  listarTrabajadoresDeAcceso,
  resolverIncidenciaAcceso,
  type EstadoIncidenciaAcceso,
  type IncidenciaAccesoResumen,
} from './api';
import { MarcacionManualDialog } from './MarcacionManualDialog';
import type { DatosMarcacionManual } from './MarcacionManualDialog';
import { nuevoId } from './identificadores';

const TAMANO_PAGINA = 25;

function formatearBolivia(instanteUtc: string): string {
  return new Intl.DateTimeFormat('es-BO', {
    timeZone: 'America/La_Paz',
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(instanteUtc));
}

export function IncidenciasAccesoPage() {
  const { clienteId } = useAuth();
  const queryClient = useQueryClient();
  const [estado, setEstado] = useState<EstadoIncidenciaAcceso | ''>('');
  const [pagina, setPagina] = useState(1);
  const [resolviendo, setResolviendo] = useState<IncidenciaAccesoResumen | null>(null);
  const [descartando, setDescartando] = useState<IncidenciaAccesoResumen | null>(null);
  const [errorResolucion, setErrorResolucion] = useState<string | null>(null);
  const [errorDescarte, setErrorDescarte] = useState<string | null>(null);
  const [motivoDescarte, setMotivoDescarte] = useState('');
  const [errorMotivoDescarte, setErrorMotivoDescarte] = useState<string | null>(null);
  const [aperturaResolucion, setAperturaResolucion] = useState(0);

  const trabajadores = useQuery({
    queryKey: ['control-acceso', 'trabajadores', clienteId],
    queryFn: () => listarTrabajadoresDeAcceso(clienteId!),
    enabled: Boolean(clienteId),
  });

  const claveIncidencias = ['control-acceso', 'incidencias', clienteId, estado, pagina] as const;
  const incidencias = useQuery({
    queryKey: claveIncidencias,
    queryFn: () => listarIncidenciasAcceso(estado || undefined, { pagina, tamanoPagina: TAMANO_PAGINA }),
    enabled: Boolean(clienteId),
  });

  const refrescar = () => {
    void queryClient.invalidateQueries({ queryKey: ['control-acceso', 'incidencias'] });
  };

  const resolver = useMutation({
    mutationFn: (datos: DatosMarcacionManual) =>
      resolverIncidenciaAcceso(resolviendo!.id, {
        trabajadorId: datos.trabajadorId,
        tipo: datos.tipo,
        horaDeclaradaUtc: datos.horaDeclaradaUtc,
        motivo: datos.motivo,
        claveIdempotencia: datos.claveIdempotencia,
        versionEsperada: resolviendo!.version,
      }),
    onSuccess: () => {
      setErrorResolucion(null);
      setResolviendo(null);
      refrescar();
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) {
        setErrorResolucion('La incidencia cambió mientras editabas; revisá y reintentá.');
        refrescar();
        return;
      }
      setErrorResolucion(
        error instanceof ApiError ? (error.code ?? 'No se pudo resolver la incidencia.') : 'No se pudo resolver la incidencia.',
      );
    },
  });

  const descartar = useMutation({
    mutationFn: () =>
      descartarIncidenciaAcceso(descartando!.id, {
        motivo: motivoDescarte.trim(),
        claveIdempotencia: nuevoId(),
        versionEsperada: descartando!.version,
      }),
    onSuccess: () => {
      setErrorDescarte(null);
      setDescartando(null);
      setMotivoDescarte('');
      refrescar();
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) {
        setErrorDescarte('La incidencia cambió mientras editabas; revisá y reintentá.');
        refrescar();
        return;
      }
      setErrorDescarte(
        error instanceof ApiError ? (error.code ?? 'No se pudo descartar la incidencia.') : 'No se pudo descartar la incidencia.',
      );
    },
  });

  const confirmarDescarte = () => {
    if (!motivoDescarte.trim()) {
      setErrorMotivoDescarte('El motivo es obligatorio.');
      return;
    }
    setErrorMotivoDescarte(null);
    descartar.mutate();
  };

  const abrirResolucion = (incidencia: IncidenciaAccesoResumen) => {
    setErrorResolucion(null);
    setResolviendo(incidencia);
    setAperturaResolucion((n) => n + 1);
  };

  const columnas: Columna<IncidenciaAccesoResumen>[] = [
    {
      clave: 'instante',
      encabezado: 'Fecha y hora',
      render: (i) => formatearBolivia(i.tercerRechazoUtc),
    },
    { clave: 'accion', encabezado: 'Acción', render: (i) => i.accion },
    { clave: 'estado', encabezado: 'Estado', render: (i) => i.estado },
    {
      clave: 'acciones',
      encabezado: 'Acciones',
      alinear: 'right',
      render: (i) => (
        <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end' }}>
          {i.estado === 'Pendiente' && (
            <>
              <Button size="small" variant="outlined" onClick={() => abrirResolucion(i)}>
                Resolver
              </Button>
              <Button
                size="small"
                variant="outlined"
                color="error"
                onClick={() => {
                  setErrorDescarte(null);
                  setDescartando(i);
                }}
              >
                Descartar
              </Button>
            </>
          )}
        </Stack>
      ),
    },
  ];

  return (
    <Container maxWidth="lg" sx={{ py: 3 }}>
      <PaginaCabecera
        titulo="Incidencias de marcación"
        subtitulo="Revisá los intentos fallidos del kiosco y registrá la marcación manual o descartá el caso."
      />

      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 2 }}>
        <TextField
          select
          label="Estado"
          size="small"
          value={estado}
          onChange={(evento) => {
            setEstado(evento.target.value as EstadoIncidenciaAcceso | '');
            setPagina(1);
          }}
          sx={{ minWidth: 180 }}
        >
          <MenuItem value="">Todos</MenuItem>
          <MenuItem value="Pendiente">Pendiente</MenuItem>
          <MenuItem value="Resuelta">Resuelta</MenuItem>
          <MenuItem value="Descartada">Descartada</MenuItem>
        </TextField>
      </Stack>

      <EstadoCarga
        cargando={incidencias.isLoading}
        error={incidencias.isError}
        mensajeError="No se pudo cargar las incidencias."
      >
        {incidencias.data && (
          <>
            <TablaDatos
              columnas={columnas}
              filas={incidencias.data.items}
              claveDeFila={(i) => i.id}
              mensajeVacio="No hay incidencias registradas."
            />
            <div style={{ marginTop: 16 }}>
              <ControlesPaginacion
                numeroPagina={incidencias.data.numeroPagina}
                total={incidencias.data.total}
                tamanoPagina={incidencias.data.tamanoPagina}
                onCambiarPagina={setPagina}
              />
            </div>
          </>
        )}
      </EstadoCarga>

      {resolviendo && (
        <MarcacionManualDialog
          key={aperturaResolucion}
          abierto
          titulo="Resolver incidencia"
          tipoInicial={resolviendo.accion}
          trabajadores={trabajadores.data ?? []}
          pendiente={resolver.isPending}
          error={errorResolucion}
          onCancelar={() => setResolviendo(null)}
          onGuardar={(datos) => resolver.mutate(datos)}
        >
          <Alert severity="info" sx={{ mb: 2 }}>
            Incidencia del {formatearBolivia(resolviendo.primerRechazoUtc)}. La hora declarada será la
            que registres a continuación.
          </Alert>
        </MarcacionManualDialog>
      )}

      <Dialog open={descartando !== null} onClose={() => setDescartando(null)} fullWidth maxWidth="sm">
        <DialogTitle>Descartar incidencia</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Typography variant="body2" color="text.secondary">
              Indicá el motivo por el cual se descarta esta incidencia. No se creará ninguna marcación.
            </Typography>
            <TextField
              label="Motivo"
              value={motivoDescarte}
              onChange={(evento) => setMotivoDescarte(evento.target.value)}
              multiline
              minRows={2}
              fullWidth
            />
            {errorMotivoDescarte && <Alert severity="error">{errorMotivoDescarte}</Alert>}
            {errorDescarte && <Alert severity="error">{errorDescarte}</Alert>}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDescartando(null)}>Cancelar</Button>
          <Button variant="contained" onClick={confirmarDescarte} disabled={descartar.isPending}>
            Descartar
          </Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
}
