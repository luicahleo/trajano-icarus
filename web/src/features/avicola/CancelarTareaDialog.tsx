import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, TextField } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useConexion } from '../../app/useConexion';
import { ApiError } from '../../lib/http';
import type { TareaVacunacionResumen } from '../../lib/tipos';
import { cancelarTareaVacunacion } from './api';
import { CLAVE_NOTIFICACION_VACUNACION, CLAVE_TAREAS_VACUNACION } from './constantes';

export function CancelarTareaDialog({ tarea, abierto, alCerrar }: { tarea: TareaVacunacionResumen | null; abierto: boolean; alCerrar: () => void }) {
  const online = useConexion();
  const queryClient = useQueryClient();
  const [motivo, setMotivo] = useState('');
  const cancelar = useMutation({
    mutationFn: () => cancelarTareaVacunacion(tarea!.id, motivo.trim() || null),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: CLAVE_NOTIFICACION_VACUNACION });
      void queryClient.invalidateQueries({ queryKey: CLAVE_TAREAS_VACUNACION });
      setMotivo('');
      alCerrar();
    },
  });

  return <Dialog open={abierto} onClose={alCerrar}>
    <DialogTitle>Cancelar tarea — {tarea?.vacuna}</DialogTitle>
    <DialogContent>
      La tarea queda en el historial como cancelada y deja de aparecer en la notificación.
      <TextField label="Motivo (opcional)" value={motivo} onChange={(e) => setMotivo(e.target.value)} multiline fullWidth margin="dense" />
      {cancelar.isError && <Alert severity="error" sx={{ mt: 1 }}>{cancelar.error instanceof ApiError ? cancelar.error.message : 'No se pudo cancelar la tarea.'}</Alert>}
    </DialogContent>
    <DialogActions>
      <Button onClick={alCerrar}>Volver</Button>
      <Button color="error" onClick={() => cancelar.mutate()} disabled={!online || cancelar.isPending || !tarea}>Cancelar tarea</Button>
    </DialogActions>
  </Dialog>;
}
