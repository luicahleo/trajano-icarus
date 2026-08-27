import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, TextField } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { useConexion } from '../../app/useConexion';
import { ApiError } from '../../lib/http';
import type { TareaVacunacionResumen } from '../../lib/tipos';
import { completarTareaVacunacion } from './api';
import { CLAVE_NOTIFICACION_VACUNACION, CLAVE_TAREAS_VACUNACION, hoyIso } from './constantes';

const esquema = z.object({
  fechaAplicacion: z.string().min(1, 'La fecha es obligatoria.').refine((f) => f <= hoyIso(), 'La fecha de aplicación no puede ser futura.'),
  avesVacunadas: z.string().refine((v) => v === '' || Number(v) > 0, 'La cantidad debe ser mayor que cero.'),
  observaciones: z.string(),
});
type DatosFormulario = z.infer<typeof esquema>;

export function CompletarTareaDialog({ tarea, abierto, alCerrar }: { tarea: TareaVacunacionResumen | null; abierto: boolean; alCerrar: () => void }) {
  const online = useConexion();
  const queryClient = useQueryClient();
  const { register, handleSubmit, formState: { errors } } = useForm<DatosFormulario>({
    resolver: zodResolver(esquema),
    defaultValues: { fechaAplicacion: hoyIso(), avesVacunadas: '', observaciones: '' },
  });
  const guardar = useMutation({
    mutationFn: (datos: DatosFormulario) => completarTareaVacunacion(tarea!.id, {
      fechaAplicacion: datos.fechaAplicacion,
      avesVacunadas: datos.avesVacunadas === '' ? null : Number(datos.avesVacunadas),
      observaciones: datos.observaciones.trim() || null,
    }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: CLAVE_NOTIFICACION_VACUNACION });
      void queryClient.invalidateQueries({ queryKey: CLAVE_TAREAS_VACUNACION });
      alCerrar();
    },
  });

  return <Dialog open={abierto} onClose={alCerrar}>
    <DialogTitle>Marcar como aplicada — {tarea?.vacuna}</DialogTitle>
    <DialogContent>
      <TextField label="Fecha de aplicación" type="date" {...register('fechaAplicacion')} error={Boolean(errors.fechaAplicacion)} helperText={errors.fechaAplicacion?.message} slotProps={{ inputLabel: { shrink: true }, htmlInput: { max: hoyIso() } }} fullWidth margin="dense" />
      <TextField label="Aves vacunadas" {...register('avesVacunadas')} error={Boolean(errors.avesVacunadas)} helperText={errors.avesVacunadas?.message ?? 'Dejalo vacío si se vacunó todo el galpón.'} inputMode="numeric" fullWidth margin="dense" />
      <TextField label="Observaciones" {...register('observaciones')} multiline fullWidth margin="dense" />
      {guardar.isError && <Alert severity="error" sx={{ mt: 1 }}>{guardar.error instanceof ApiError ? guardar.error.message : 'No se pudo completar la tarea.'}</Alert>}
    </DialogContent>
    <DialogActions>
      <Button onClick={alCerrar}>Volver</Button>
      <Button onClick={() => void handleSubmit((datos) => guardar.mutate(datos))()} disabled={!online || guardar.isPending || !tarea}>Guardar</Button>
    </DialogActions>
  </Dialog>;
}
