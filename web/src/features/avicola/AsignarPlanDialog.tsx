import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Radio, RadioGroup, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useConexion } from '../../app/useConexion';
import { ApiError } from '../../lib/http';
import { asignarPlanVacunacion, listarProgramasVacunacion } from './api';
import { CLAVE_NOTIFICACION_VACUNACION, CLAVE_PROGRAMAS_VACUNACION, CLAVE_TAREAS_VACUNACION } from './constantes';

export function AsignarPlanDialog({ galponId, abierto, alCerrar }: { galponId: string; abierto: boolean; alCerrar: () => void }) {
  const online = useConexion();
  const queryClient = useQueryClient();
  const [programaId, setProgramaId] = useState('');
  const programas = useQuery({ queryKey: CLAVE_PROGRAMAS_VACUNACION, queryFn: () => listarProgramasVacunacion(), enabled: abierto });
  const asignar = useMutation({
    mutationFn: () => asignarPlanVacunacion(galponId, programaId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: CLAVE_TAREAS_VACUNACION });
      void queryClient.invalidateQueries({ queryKey: CLAVE_NOTIFICACION_VACUNACION });
      setProgramaId('');
      alCerrar();
    },
  });

  return <Dialog open={abierto} onClose={alCerrar}>
    <DialogTitle>Asignar plan de vacunación</DialogTitle>
    <DialogContent>
      {programas.isLoading && <Typography>Cargando…</Typography>}
      {programas.isError && <Alert severity="error">No se pudo cargar el catálogo.</Alert>}
      <RadioGroup value={programaId} onChange={(e) => setProgramaId(e.target.value)}>
        {(programas.data ?? []).map((p) => (
          <FormControlLabel key={p.id} value={p.id} control={<Radio />} label={p.nombre} />
        ))}
      </RadioGroup>
      <Alert severity="warning" sx={{ mt: 2 }}>
        Si el galpón ya tiene un plan, las pendientes del plan anterior se desactivan. Las completadas y canceladas se conservan como historial.
      </Alert>
      {asignar.isError && <Alert severity="error" sx={{ mt: 1 }}>{asignar.error instanceof ApiError ? asignar.error.message : 'No se pudo asignar el plan.'}</Alert>}
    </DialogContent>
    <DialogActions>
      <Button onClick={alCerrar}>Volver</Button>
      <Button variant="contained" onClick={() => asignar.mutate()} disabled={!online || asignar.isPending || !programaId}>Asignar</Button>
    </DialogActions>
  </Dialog>;
}
