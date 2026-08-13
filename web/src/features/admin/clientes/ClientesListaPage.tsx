import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import AddRoundedIcon from '@mui/icons-material/AddRounded';
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
import { useNavigate } from 'react-router-dom';
import type { ClienteResumen } from '../../../lib/tipos';
import { listarClientes, reactivarCliente, suspenderCliente } from './api';

const CLAVE_CLIENTES = ['clientes'] as const;

type AccionEstado = 'suspender' | 'reactivar';

interface Confirmacion {
  id: string;
  razonSocial: string;
  accion: AccionEstado;
}

export function ClientesListaPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { data: clientes, isLoading, isError } = useQuery({
    queryKey: CLAVE_CLIENTES,
    queryFn: listarClientes,
  });
  const [confirmacion, setConfirmacion] = useState<Confirmacion | null>(null);

  const cambiarEstado = useMutation({
    mutationFn: ({ id, accion }: { id: string; accion: AccionEstado }) =>
      accion === 'suspender' ? suspenderCliente(id) : reactivarCliente(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: CLAVE_CLIENTES });
      setConfirmacion(null);
    },
  });

  const cambiar = (cliente: ClienteResumen, accion: AccionEstado) => {
    setConfirmacion({ id: cliente.id, razonSocial: cliente.razonSocial, accion });
  };

  return (
    <Box sx={{ p: 4 }}>
      <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h4">Clientes</Typography>
        <Button variant="contained" startIcon={<AddRoundedIcon />} onClick={() => navigate('/admin/clientes/nuevo')}>
          Nuevo cliente
        </Button>
      </Stack>

      {isError && <Alert severity="error">No se pudo cargar la lista de clientes.</Alert>}
      {isLoading && <CircularProgress />}
      {clientes && (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Razón social</TableCell>
                <TableCell>Identificador fiscal</TableCell>
                <TableCell>Estado</TableCell>
                <TableCell>Módulos</TableCell>
                <TableCell align="right">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {clientes.map((cliente) => (
                <TableRow key={cliente.id}>
                  <TableCell>{cliente.razonSocial}</TableCell>
                  <TableCell>{cliente.identificadorFiscal}</TableCell>
                  <TableCell>
                    <Chip
                      label={cliente.estaActivo ? 'Activo' : 'Suspendido'}
                      color={cliente.estaActivo ? 'success' : 'default'}
                      size="small"
                    />
                  </TableCell>
                  <TableCell>
                    {cliente.modulos.map((modulo) => (
                      <Chip key={modulo} label={modulo} size="small" variant="outlined" sx={{ mr: 1 }} />
                    ))}
                  </TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end' }}>
                      <Button size="small" variant="outlined" onClick={() => navigate(`/admin/clientes/${cliente.id}`)}>
                        Detalle
                      </Button>
                      {cliente.estaActivo ? (
                        <Button size="small" variant="outlined" color="error" onClick={() => cambiar(cliente, 'suspender')}>
                          Suspender
                        </Button>
                      ) : (
                        <Button size="small" variant="outlined" color="success" onClick={() => cambiar(cliente, 'reactivar')}>
                          Reactivar
                        </Button>
                      )}
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog open={confirmacion !== null} onClose={() => setConfirmacion(null)}>
        <DialogTitle>Confirmar acción</DialogTitle>
        <DialogContent>
          {confirmacion &&
            (confirmacion.accion === 'suspender'
              ? `¿Suspender a ${confirmacion.razonSocial}?`
              : `¿Reactivar a ${confirmacion.razonSocial}?`)}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmacion(null)}>Cancelar</Button>
          <Button
            variant="contained"
            color={confirmacion?.accion === 'suspender' ? 'error' : 'success'}
            onClick={() => confirmacion && cambiarEstado.mutate(confirmacion)}
          >
            Confirmar
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
