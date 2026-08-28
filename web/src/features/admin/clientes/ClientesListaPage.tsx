import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import AddRoundedIcon from '@mui/icons-material/AddRounded';
import {
  Button,
  Chip,
  Container,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
} from '@mui/material';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { DialogoConfirmacion } from '../../../app/ui/DialogoConfirmacion';
import { EstadoCarga } from '../../../app/ui/EstadoCarga';
import { PaginaCabecera } from '../../../app/ui/PaginaCabecera';
import type { ClienteResumen } from '../../../lib/tipos';
import { CLAVE_CLIENTES, listarClientes, reactivarCliente, suspenderCliente } from './api';

type AccionEstado = 'suspender' | 'reactivar';

interface Confirmacion {
  id: string;
  razonSocial: string;
  accion: AccionEstado;
}

export function ClientesListaPage() {
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
    <Container maxWidth="lg" sx={{ py: 3 }}>
      <PaginaCabecera
        titulo="Clientes"
        acciones={
          <Button
            variant="contained"
            startIcon={<AddRoundedIcon />}
            onClick={() => navigate('/admin/clientes/nuevo')}
          >
            Nuevo cliente
          </Button>
        }
      />

      <EstadoCarga
        cargando={isLoading}
        error={isError}
        mensajeError="No se pudo cargar la lista de clientes."
      >
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
                {clientes.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                      No hay clientes registrados todavía.
                    </TableCell>
                  </TableRow>
                ) : (
                  clientes.map((cliente) => (
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
                          <Chip
                            key={modulo}
                            label={modulo}
                            size="small"
                            variant="outlined"
                            sx={{ mr: 1 }}
                          />
                        ))}
                      </TableCell>
                      <TableCell align="right">
                        <Button
                          size="small"
                          variant="outlined"
                          onClick={() => navigate(`/admin/clientes/${cliente.id}`)}
                        >
                          Detalle
                        </Button>
                        {cliente.estaActivo ? (
                          <Button
                            size="small"
                            variant="outlined"
                            color="error"
                            onClick={() => cambiar(cliente, 'suspender')}
                          >
                            Suspender
                          </Button>
                        ) : (
                          <Button
                            size="small"
                            variant="outlined"
                            color="success"
                            onClick={() => cambiar(cliente, 'reactivar')}
                          >
                            Reactivar
                          </Button>
                        )}
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </EstadoCarga>

      <DialogoConfirmacion
        abierto={confirmacion !== null}
        titulo="Confirmar acción"
        mensaje={
          confirmacion &&
          (confirmacion.accion === 'suspender'
            ? `¿Suspender a ${confirmacion.razonSocial}?`
            : `¿Reactivar a ${confirmacion.razonSocial}?`)
        }
        color={confirmacion?.accion === 'suspender' ? 'error' : 'success'}
        pendiente={cambiarEstado.isPending}
        onCancelar={() => setConfirmacion(null)}
        onConfirmar={() => confirmacion && cambiarEstado.mutate(confirmacion)}
      />
    </Container>
  );
}
