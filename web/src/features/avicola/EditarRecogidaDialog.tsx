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
import type { RecogidaResumen } from '../../lib/tipos';
import { ApiError } from '../../lib/http';
import { useConexion } from '../../app/useConexion';
import { editarProduccion } from './api';

const esquema = z.object({
  hora: z.string().min(1, 'La hora es obligatoria.'),
  cantidadMaples: z.number().int().min(0),
  unidadesIncompletas: z.number().int().min(0),
  maplesDescarte: z.number().int().min(0),
  unidadesDescarte: z.number().int().min(0),
});
type DatosFormulario = z.infer<typeof esquema>;

export function EditarRecogidaDialog({
  recogida,
  abierto,
  alCerrar,
}: {
  recogida: RecogidaResumen | null;
  abierto: boolean;
  alCerrar: () => void;
}) {
  const online = useConexion();
  const queryClient = useQueryClient();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<DatosFormulario>({
    resolver: zodResolver(esquema),
    values: recogida
      ? {
          hora: recogida.hora.slice(0, 5),
          cantidadMaples: recogida.cantidadMaples,
          unidadesIncompletas: recogida.unidadesIncompletas,
          maplesDescarte: recogida.maplesDescarte,
          unidadesDescarte: recogida.unidadesDescarte,
        }
      : undefined,
  });
  const guardar = useMutation({
    mutationFn: (datos: DatosFormulario) => editarProduccion(recogida!.id, datos),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['avicola', 'produccion'] });
      void queryClient.invalidateQueries({ queryKey: ['avicola', 'eficiencia'] });
      alCerrar();
    },
  });
  return (
    <Dialog open={abierto} onClose={alCerrar}>
      <DialogTitle>Editar recogida</DialogTitle>
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
          label="Maples"
          {...register('cantidadMaples', { valueAsNumber: true })}
          error={Boolean(errors.cantidadMaples)}
          helperText={errors.cantidadMaples?.message}
          inputMode="numeric"
          fullWidth
          margin="dense"
        />
        <TextField
          label="Unidades sueltas"
          {...register('unidadesIncompletas', { valueAsNumber: true })}
          error={Boolean(errors.unidadesIncompletas)}
          helperText={errors.unidadesIncompletas?.message}
          inputMode="numeric"
          fullWidth
          margin="dense"
        />
        <TextField
          label="Maples de descarte"
          {...register('maplesDescarte', { valueAsNumber: true })}
          error={Boolean(errors.maplesDescarte)}
          helperText={errors.maplesDescarte?.message}
          inputMode="numeric"
          fullWidth
          margin="dense"
        />
        <TextField
          label="Unidades de descarte"
          {...register('unidadesDescarte', { valueAsNumber: true })}
          error={Boolean(errors.unidadesDescarte)}
          helperText={errors.unidadesDescarte?.message}
          inputMode="numeric"
          fullWidth
          margin="dense"
        />
        {guardar.isError && (
          <Alert severity="error" sx={{ mt: 1 }}>
            {guardar.error instanceof ApiError
              ? guardar.error.message
              : 'No se pudo editar la recogida.'}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={alCerrar}>Cancelar</Button>
        <Button
          onClick={() => void handleSubmit((d) => guardar.mutate(d))()}
          disabled={!online || guardar.isPending}
        >
          Guardar
        </Button>
      </DialogActions>
    </Dialog>
  );
}
