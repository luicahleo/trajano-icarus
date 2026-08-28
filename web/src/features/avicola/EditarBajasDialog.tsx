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
import type { MortalidadRegistro } from '../../lib/tipos';
import { ApiError } from '../../lib/http';
import { useConexion } from '../../app/useConexion';
import { editarMortalidad } from './api';

const esquema = z.object({
  hora: z.string().min(1, 'La hora es obligatoria.'),
  cantidadMuertas: z.number().int().positive('La cantidad debe ser mayor que cero.'),
});
type DatosFormulario = z.infer<typeof esquema>;

export function EditarBajasDialog({
  registro,
  abierto,
  alCerrar,
}: {
  registro: MortalidadRegistro | null;
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
    values: registro
      ? { hora: registro.hora.slice(0, 5), cantidadMuertas: registro.cantidadMuertas }
      : undefined,
  });
  const guardar = useMutation({
    mutationFn: (datos: DatosFormulario) => editarMortalidad(registro!.id, datos),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['avicola', 'mortalidad'] });
      void queryClient.invalidateQueries({ queryKey: ['avicola', 'galpon'] });
      void queryClient.invalidateQueries({ queryKey: ['avicola', 'eficiencia'] });
      alCerrar();
    },
  });
  return (
    <Dialog open={abierto} onClose={alCerrar}>
      <DialogTitle>Editar bajas</DialogTitle>
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
              : 'No se pudieron editar las bajas.'}
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
