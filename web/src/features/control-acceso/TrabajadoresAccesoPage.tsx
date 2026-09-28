import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Container, Stack, Typography } from '@mui/material';
import { useState } from 'react';
import { DialogoConfirmacion } from '../../app/ui/DialogoConfirmacion';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import { PaginaCabecera } from '../../app/ui/PaginaCabecera';
import { TablaDatos } from '../../app/ui/TablaDatos';
import type { Columna } from '../../app/ui/TablaDatos';
import { ApiError } from '../../lib/http';
import type { ResultadoEnrolamientoAcceso, TrabajadorResumen } from '../../lib/tipos';
import { useAuth } from '../auth/AuthContext';
import {
  definirHabilitacionAcceso,
  enrolarTrabajador,
  listarAccesoTrabajadores,
  listarTrabajadoresDeAcceso,
  revocarRostro,
} from './api';
import type { AccesoTrabajadorResumen } from './api';
import { EnrolamientoDialog } from './EnrolamientoDialog';
import { nuevoId } from './identificadores';
import { SesionKioscoPanel } from './SesionKioscoPanel';

function etiquetaAcceso(acceso: AccesoTrabajadorResumen | undefined): string {
  if (!acceso) return 'Sin configurar';
  if (acceso.enrolamiento === 'Revocado') return 'Rostro revocado';
  if (!acceso.habilitado) return 'Deshabilitado';
  if (acceso.enrolamiento === 'Vigente') return 'Habilitado';
  return 'Sin enrolar';
}

export function TrabajadoresAccesoPage() {
  const { clienteId } = useAuth();
  const queryClient = useQueryClient();
  const [enrolando, setEnrolando] = useState<TrabajadorResumen | null>(null);
  const [resultado, setResultado] = useState<ResultadoEnrolamientoAcceso | null>(null);
  const [errorEnrolamiento, setErrorEnrolamiento] = useState<string | null>(null);
  const [revocando, setRevocando] = useState<TrabajadorResumen | null>(null);

  const trabajadores = useQuery({
    queryKey: ['control-acceso', 'trabajadores', clienteId],
    queryFn: () => listarTrabajadoresDeAcceso(clienteId!),
    enabled: Boolean(clienteId),
  });
  const accesos = useQuery({
    queryKey: ['control-acceso', 'accesos'],
    queryFn: listarAccesoTrabajadores,
  });
  const accesoPorTrabajador = new Map(
    (accesos.data ?? []).map((acceso) => [acceso.trabajadorId, acceso]),
  );

  const refrescar = () => {
    void queryClient.invalidateQueries({ queryKey: ['control-acceso', 'accesos'] });
  };

  const habilitacion = useMutation({
    mutationFn: (datos: { id: string; habilitado: boolean }) =>
      definirHabilitacionAcceso(datos.id, datos.habilitado),
    onSuccess: refrescar,
  });

  const revocar = useMutation({
    mutationFn: (id: string) => revocarRostro(id),
    onSuccess: () => {
      setRevocando(null);
      refrescar();
    },
  });

  const enrolar = useMutation({
    mutationFn: (datos: { id: string; muestraBase64: string }) =>
      enrolarTrabajador(datos.id, {
        muestraBase64: datos.muestraBase64,
        formato: 'image/jpeg',
        claveIdempotencia: nuevoId(),
      }),
    onSuccess: (resultadoEnrolamiento) => {
      setResultado(resultadoEnrolamiento);
      refrescar();
    },
    onError: (error) =>
      setErrorEnrolamiento(
        error instanceof ApiError ? (error.code ?? 'No se pudo enrolar.') : 'No se pudo enrolar.',
      ),
  });

  const columnas: Columna<TrabajadorResumen>[] = [
    { clave: 'nombre', encabezado: 'Nombre', render: (t) => t.nombre },
    { clave: 'cargo', encabezado: 'Cargo', render: (t) => t.cargo },
    {
      clave: 'acceso',
      encabezado: 'Acceso',
      render: (t) => etiquetaAcceso(accesoPorTrabajador.get(t.id)),
    },
    {
      clave: 'acciones',
      encabezado: 'Acciones',
      alinear: 'right',
      render: (t) => {
        const acceso = accesoPorTrabajador.get(t.id);
        return (
          <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end' }}>
            <Button
              size="small"
              variant="outlined"
              onClick={() =>
                habilitacion.mutate({ id: t.id, habilitado: !(acceso?.habilitado ?? false) })
              }
            >
              {acceso?.habilitado ? 'Deshabilitar' : 'Habilitar'}
            </Button>
            <Button
              size="small"
              variant="outlined"
              onClick={() => {
                setResultado(null);
                setErrorEnrolamiento(null);
                setEnrolando(t);
              }}
            >
              Enrolar
            </Button>
            <Button
              size="small"
              variant="outlined"
              color="error"
              onClick={() => setRevocando(t)}
            >
              Revocar
            </Button>
          </Stack>
        );
      },
    },
  ];

  return (
    <Container maxWidth="lg" sx={{ py: 3 }}>
      <PaginaCabecera
        titulo="Control de acceso"
        subtitulo="Habilita trabajadores, enrola su rostro y administra el kiosco."
      />

      <EstadoCarga
        cargando={trabajadores.isLoading || accesos.isLoading}
        error={trabajadores.isError || accesos.isError}
        mensajeError="No se pudo cargar el control de acceso."
      >
        {trabajadores.data && (
          <TablaDatos
            columnas={columnas}
            filas={trabajadores.data}
            claveDeFila={(t) => t.id}
            mensajeVacio="No hay trabajadores todavía."
          />
        )}
      </EstadoCarga>

      <Stack spacing={2} sx={{ mt: 3 }}>
        <Typography variant="h6">Dispositivo dedicado</Typography>
        <SesionKioscoPanel />
      </Stack>

      <EnrolamientoDialog
        abierto={enrolando !== null}
        trabajador={enrolando}
        pendiente={enrolar.isPending}
        resultado={resultado}
        error={errorEnrolamiento}
        onCancelar={() => {
          setEnrolando(null);
          setResultado(null);
        }}
        onCapturar={(muestraBase64) =>
          enrolando && enrolar.mutate({ id: enrolando.id, muestraBase64 })
        }
      />

      <DialogoConfirmacion
        abierto={revocando !== null}
        titulo="Confirmar acción"
        mensaje={`¿Revocar el rostro de ${revocando?.nombre}? Se elimina su plantilla activa y deja de poder marcar.`}
        color="error"
        pendiente={revocar.isPending}
        onCancelar={() => setRevocando(null)}
        onConfirmar={() => revocando && revocar.mutate(revocando.id)}
      />
    </Container>
  );
}
