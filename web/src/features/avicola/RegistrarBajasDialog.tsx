import { zodResolver } from '@hookform/resolvers/zod';
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  TextField,
} from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { ApiError } from '../../lib/http';
import type { OperacionPendiente } from '../../lib/offline/tipos';
import { actualizarContenidoOperacion } from '../../app/offline/coordinador';
import { guardarBajas } from './offline';
import type { DatosBajas } from './api';

const esquema = z.object({
  hora: z.string().min(1, 'La hora es obligatoria.'),
  cantidadMuertas: z.number().int().positive('La cantidad debe ser mayor que cero.'),
});
type DatosFormulario = z.infer<typeof esquema>;

export function RegistrarBajasDialog({
  galponId,
  abierto,
  alCerrar,
  pendiente,
}: {
  galponId: string;
  abierto: boolean;
  alCerrar: () => void;
  pendiente?: OperacionPendiente | null;
}) {
  const queryClient = useQueryClient();
  // Edición de una operación aún en cola: precarga su cuerpo y conserva su
  // idempotencyKey.
  const inicial = pendiente ? (pendiente.cuerpo as DatosBajas) : null;
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<DatosFormulario>({
    resolver: zodResolver(esquema),
    defaultValues: { hora: new Date().toTimeString().slice(0, 5) },
    values: inicial
      ? {
          hora: inicial.hora ?? new Date().toTimeString().slice(0, 5),
          cantidadMuertas: inicial.cantidadMuertas,
        }
      : undefined,
  });
  const guardar = useMutation({
    // 'always': la propia mutationFn decide (conCola encola si no hay red). Con
    // el 'online' por defecto, TanStack pausa la mutación al quedarse sin red y
    // el guardado offline jamás se ejecuta ni cierra el diálogo.
    networkMode: 'always',
    mutationFn: async (datos: DatosFormulario) => {
      if (pendiente) {
        await actualizarContenidoOperacion(pendiente.id, {
          ...datos,
          idempotencyKey: inicial!.idempotencyKey,
        });
        return true; // sigue en cola: la fila se refresca vía suscripción
      }
      return guardarBajas(galponId, { ...datos, idempotencyKey: crypto.randomUUID() });
    },
    onSuccess: (encolada) => {
      if (!encolada) {
        void queryClient.invalidateQueries({ queryKey: ['avicola', 'mortalidad'] });
        void queryClient.invalidateQueries({ queryKey: ['avicola', 'galpon'] });
        void queryClient.invalidateQueries({ queryKey: ['avicola', 'eficiencia'] });
      }
      alCerrar();
    },
  });
  const enviar = (datos: DatosFormulario) => guardar.mutate(datos);

  return (
    <Dialog open={abierto} onClose={alCerrar}>
      <DialogTitle>{pendiente ? 'Editar bajas' : 'Registrar bajas'}</DialogTitle>
      <DialogContent>
        <TextField
          label="Hora"
          type="time"
          {...register('hora')}
          error={Boolean(errors.hora)}
          helperText={errors.hora?.message}
          fullWidth
          margin="dense"
        />
        <TextField
          label="Gallinas muertas"
          {...register('cantidadMuertas', { valueAsNumber: true })}
          error={Boolean(errors.cantidadMuertas)}
          helperText={errors.cantidadMuertas?.message}
          inputMode="numeric"
          fullWidth
          margin="dense"
        />
        {guardar.isError && (
          <Alert severity="error" sx={{ mt: 1 }}>
            {guardar.error instanceof ApiError
              ? guardar.error.message
              : 'No se pudieron registrar las bajas.'}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={alCerrar}>Cancelar</Button>
        <Button onClick={() => void handleSubmit(enviar)()} disabled={guardar.isPending}>
          Guardar
        </Button>
      </DialogActions>
    </Dialog>
  );
}
