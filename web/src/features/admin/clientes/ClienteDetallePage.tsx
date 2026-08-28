import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Checkbox,
  Chip,
  Container,
  FormControlLabel,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { DialogoConfirmacion } from '../../../app/ui/DialogoConfirmacion';
import { EstadoCarga } from '../../../app/ui/EstadoCarga';
import { PaginaCabecera } from '../../../app/ui/PaginaCabecera';
import type { Modulo } from '../../../lib/tipos';
import {
  CLAVE_CLIENTES,
  definirModulos,
  listarClientes,
  reactivarCliente,
  suspenderCliente,
} from './api';

const MODULOS: Modulo[] = ['GestionAvicola', 'ControlAcceso'];

export function ClienteDetallePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const {
    data: clientes,
    isLoading,
    isError,
  } = useQuery({
    queryKey: CLAVE_CLIENTES,
    queryFn: listarClientes,
  });
  const [confirmacion, setConfirmacion] = useState<'suspender' | 'reactivar' | null>(null);

  const cliente = clientes?.find((c) => c.id === id);

  const guardarModulos = useMutation({
    mutationFn: (modulos: Modulo[]) => definirModulos(cliente!.id, modulos),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: CLAVE_CLIENTES }),
    onError: () => {
      queryClient.invalidateQueries({ queryKey: CLAVE_CLIENTES });
    },
  });

  const cambiarEstado = useMutation({
    mutationFn: (accion: 'suspender' | 'reactivar') =>
      accion === 'suspender' ? suspenderCliente(cliente!.id) : reactivarCliente(cliente!.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: CLAVE_CLIENTES });
      setConfirmacion(null);
    },
  });

  if (isLoading) {
    return (
      <Container maxWidth="lg" sx={{ py: 3 }}>
        <EstadoCarga cargando error={false}>
          <></>
        </EstadoCarga>
      </Container>
    );
  }
  if (isError) {
    return (
      <Container maxWidth="lg" sx={{ py: 3 }}>
        <EstadoCarga cargando={false} error mensajeError="No se pudo cargar el cliente.">
          <></>
        </EstadoCarga>
      </Container>
    );
  }
  if (!cliente) {
    return (
      <Container maxWidth="lg" sx={{ py: 3 }}>
        <Alert severity="info">No se encontró el cliente solicitado.</Alert>
      </Container>
    );
  }

  const alternarModulo = (modulo: Modulo) => {
    const nuevos = cliente.modulos.includes(modulo)
      ? cliente.modulos.filter((m) => m !== modulo)
      : [...cliente.modulos, modulo];
    guardarModulos.mutate(nuevos);
  };

  return (
    <Container maxWidth="lg" sx={{ py: 3 }}>
      <Button sx={{ mb: 2 }} onClick={() => navigate('/admin/clientes')}>
        Volver a clientes
      </Button>
      <PaginaCabecera titulo={cliente.razonSocial} />
      <Paper sx={{ p: 3, maxWidth: 520 }}>
        <Stack spacing={2}>
          <Typography>
            Identificador fiscal: <strong>{cliente.identificadorFiscal}</strong>
          </Typography>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <Typography>Estado:</Typography>
            <Chip
              label={cliente.estaActivo ? 'Activo' : 'Suspendido'}
              color={cliente.estaActivo ? 'success' : 'default'}
              size="small"
            />
          </Stack>
          <Typography variant="h6" sx={{ mt: 2 }}>
            Módulos habilitados
          </Typography>
          {MODULOS.map((modulo) => (
            <FormControlLabel
              key={modulo}
              control={
                <Checkbox
                  checked={cliente.modulos.includes(modulo)}
                  onChange={() => alternarModulo(modulo)}
                />
              }
              label={modulo}
            />
          ))}
          {cliente.estaActivo ? (
            <Button variant="outlined" color="error" onClick={() => setConfirmacion('suspender')}>
              Suspender
            </Button>
          ) : (
            <Button variant="outlined" color="success" onClick={() => setConfirmacion('reactivar')}>
              Reactivar
            </Button>
          )}
        </Stack>
      </Paper>

      <DialogoConfirmacion
        abierto={confirmacion !== null}
        titulo="Confirmar acción"
        mensaje={
          confirmacion === 'suspender'
            ? `¿Suspender a ${cliente.razonSocial}?`
            : `¿Reactivar a ${cliente.razonSocial}?`
        }
        color={confirmacion === 'suspender' ? 'error' : 'success'}
        pendiente={cambiarEstado.isPending}
        onCancelar={() => setConfirmacion(null)}
        onConfirmar={() => confirmacion && cambiarEstado.mutate(confirmacion)}
      />
    </Container>
  );
}
