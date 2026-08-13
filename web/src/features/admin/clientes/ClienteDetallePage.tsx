import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import { Checkbox } from '@mui/material';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import type { Modulo } from '../../../lib/tipos';
import { definirModulos, listarClientes, reactivarCliente, suspenderCliente } from './api';

const CLAVE_CLIENTES = ['clientes'] as const;
const MODULOS: Modulo[] = ['GestionAvicola', 'ControlAcceso'];

export function ClienteDetallePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { data: clientes, isLoading, isError } = useQuery({
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

  if (isLoading) return <CircularProgress sx={{ m: 4 }} />;
  if (isError) return <Alert severity="error">No se pudo cargar el cliente.</Alert>;
  if (!cliente) return <Alert severity="info">No se encontró el cliente solicitado.</Alert>;

  const alternarModulo = (modulo: Modulo) => {
    const nuevos = cliente.modulos.includes(modulo)
      ? cliente.modulos.filter((m) => m !== modulo)
      : [...cliente.modulos, modulo];
    guardarModulos.mutate(nuevos);
  };

  return (
    <Box sx={{ p: 4 }}>
      <Button sx={{ mb: 2 }} onClick={() => navigate('/admin/clientes')}>
        Volver a clientes
      </Button>
      <Typography variant="h4" sx={{ mb: 3 }}>
        {cliente.razonSocial}
      </Typography>
      <Paper sx={{ p: 3, maxWidth: 520 }}>
        <Stack spacing={2}>
          <Typography>
            Identificador fiscal: <strong>{cliente.identificadorFiscal}</strong>
          </Typography>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <Typography>Estado:</Typography>
            <Chip label={cliente.estaActivo ? 'Activo' : 'Suspendido'} color={cliente.estaActivo ? 'success' : 'default'} size="small" />
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

      <Dialog open={confirmacion !== null} onClose={() => setConfirmacion(null)}>
        <DialogTitle>Confirmar acción</DialogTitle>
        <DialogContent>
          {confirmacion === 'suspender' ? `¿Suspender a ${cliente.razonSocial}?` : `¿Reactivar a ${cliente.razonSocial}?`}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmacion(null)}>Cancelar</Button>
          <Button
            variant="contained"
            color={confirmacion === 'suspender' ? 'error' : 'success'}
            onClick={() => confirmacion && cambiarEstado.mutate(confirmacion)}
          >
            Confirmar
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
