import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import AddRoundedIcon from '@mui/icons-material/AddRounded';
import { Button, Chip, Container } from '@mui/material';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { DialogoConfirmacion } from '../../../app/ui/DialogoConfirmacion';
import { EstadoCarga } from '../../../app/ui/EstadoCarga';
import { PaginaCabecera } from '../../../app/ui/PaginaCabecera';
import { TablaDatos } from '../../../app/ui/TablaDatos';
import type { Columna } from '../../../app/ui/TablaDatos';
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

  const columnas: Columna<ClienteResumen>[] = [
    { clave: 'razonSocial', encabezado: 'Razón social', render: (c) => c.razonSocial },
    {
      clave: 'identificadorFiscal',
      encabezado: 'Identificador fiscal',
      render: (c) => c.identificadorFiscal,
    },
    {
      clave: 'estado',
      encabezado: 'Estado',
      render: (c) => (
        <Chip
          label={c.estaActivo ? 'Activo' : 'Suspendido'}
          color={c.estaActivo ? 'success' : 'default'}
          size="small"
        />
      ),
    },
    {
      clave: 'modulos',
      encabezado: 'Módulos',
      render: (c) => (
        <>
          {c.modulos.map((modulo) => (
            <Chip key={modulo} label={modulo} size="small" variant="outlined" sx={{ mr: 1 }} />
          ))}
        </>
      ),
    },
    {
      clave: 'acciones',
      encabezado: 'Acciones',
      alinear: 'right',
      render: (c) => (
        <>
          <Button
            size="small"
            variant="outlined"
            onClick={() => navigate(`/admin/clientes/${c.id}`)}
          >
            Detalle
          </Button>
          {c.estaActivo ? (
            <Button
              size="small"
              variant="outlined"
              color="error"
              onClick={() => cambiar(c, 'suspender')}
            >
              Suspender
            </Button>
          ) : (
            <Button
              size="small"
              variant="outlined"
              color="success"
              onClick={() => cambiar(c, 'reactivar')}
            >
              Reactivar
            </Button>
          )}
        </>
      ),
    },
  ];

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
          <TablaDatos
            columnas={columnas}
            filas={clientes}
            claveDeFila={(c) => c.id}
            mensajeVacio="No hay clientes registrados todavía."
          />
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
