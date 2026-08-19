import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, TextField } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { ApiError } from '../../lib/http';
import { useConexion } from '../../app/useConexion';
import { registrarMortalidad } from './api';

const esquema = z.object({
  hora: z.string().min(1, 'La hora es obligatoria.'),
  cantidadMuertas: z.number().int().positive('La cantidad debe ser mayor que cero.'),
});
type DatosFormulario = z.infer<typeof esquema>;

export function RegistrarBajasDialog({ galponId, abierto, alCerrar }: { galponId: string; abierto: boolean; alCerrar: () => void }) {
  const online = useConexion();
  const queryClient = useQueryClient();
  const { register, handleSubmit, formState: { errors } } = useForm<DatosFormulario>({
    resolver: zodResolver(esquema),
    defaultValues: { hora: new Date().toTimeString().slice(0, 5) },
  });
  const guardar = useMutation({
    mutationFn: (datos: DatosFormulario) => registrarMortalidad(galponId, { ...datos, idempotencyKey: crypto.randomUUID() }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['avicola', 'mortalidad'] });
      void queryClient.invalidateQueries({ queryKey: ['avicola', 'galpon'] });
      void queryClient.invalidateQueries({ queryKey: ['avicola', 'eficiencia'] });
      alCerrar();
    },
  });
  const enviar = (datos: DatosFormulario) => guardar.mutate(datos);

  return <Dialog open={abierto} onClose={alCerrar}>
    <DialogTitle>Registrar bajas</DialogTitle>
    <DialogContent>
      <TextField label="Hora" type="time" {...register('hora')} error={Boolean(errors.hora)} helperText={errors.hora?.message} fullWidth margin="dense" />
      <TextField label="Gallinas muertas" {...register('cantidadMuertas', { valueAsNumber: true })} error={Boolean(errors.cantidadMuertas)} helperText={errors.cantidadMuertas?.message} inputMode="numeric" fullWidth margin="dense" />
      {guardar.isError && <Alert severity="error" sx={{ mt: 1 }}>{guardar.error instanceof ApiError ? guardar.error.message : 'No se pudieron registrar las bajas.'}</Alert>}
    </DialogContent>
    <DialogActions>
      <Button onClick={alCerrar}>Cancelar</Button>
      <Button onClick={() => void handleSubmit(enviar)()} disabled={!online || guardar.isPending}>Guardar</Button>
    </DialogActions>
  </Dialog>;
}
