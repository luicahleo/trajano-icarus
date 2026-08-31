import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  TextField,
  Typography,
} from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { ApiError } from '../../lib/http';
import type { OperacionPendiente } from '../../lib/offline/tipos';
import { actualizarContenidoOperacion } from '../../app/offline/coordinador';
import { guardarRecogida } from './offline';
import { type DatosRecogida } from './api';
import { totalHuevos } from './formatos';

export function RegistrarRecogidaDialog({
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
  // Edición de una operación aún en cola: precarga su cuerpo y conserva su
  // idempotencyKey. El estado se inicializa al montar; la página remonta el
  // diálogo con `key` al cambiar de registro.
  const inicial = pendiente ? (pendiente.cuerpo as DatosRecogida) : null;
  const [hora, setHora] = useState(inicial?.hora ?? new Date().toTimeString().slice(0, 5));
  const [maples, setMaples] = useState(inicial ? String(inicial.cantidadMaples) : '');
  const [sueltos, setSueltos] = useState(inicial ? String(inicial.unidadesIncompletas) : '');
  const [descarteMaples, setDescarteMaples] = useState(
    inicial ? String(inicial.maplesDescarte) : '',
  );
  const [descarteSueltos, setDescarteSueltos] = useState(
    inicial ? String(inicial.unidadesDescarte) : '',
  );
  const qc = useQueryClient();
  const guardar = useMutation({
    // 'always': la propia mutationFn decide (conCola encola si no hay red). Con
    // el 'online' por defecto, TanStack pausa la mutación al quedarse sin red y
    // el guardado offline jamás se ejecuta ni cierra el diálogo.
    networkMode: 'always',
    mutationFn: async () => {
      const d: DatosRecogida = {
        hora,
        cantidadMaples: Number(maples) || 0,
        unidadesIncompletas: Number(sueltos) || 0,
        maplesDescarte: Number(descarteMaples) || 0,
        unidadesDescarte: Number(descarteSueltos) || 0,
        idempotencyKey: inicial?.idempotencyKey ?? crypto.randomUUID(),
      };
      if (pendiente) {
        await actualizarContenidoOperacion(pendiente.id, d);
        return true; // sigue en cola: la fila se refresca vía suscripción
      }
      return guardarRecogida(galponId, d); // true si quedó encolada
    },
    onSuccess: (encolada) => {
      if (!encolada) void qc.invalidateQueries({ queryKey: ['avicola'] });
      alCerrar(); // si encoló, el coordinador muestra el aviso «Guardado sin conexión»
    },
  });
  return (
    <Dialog open={abierto} onClose={alCerrar}>
      <DialogTitle>{pendiente ? 'Editar recogida' : 'Registrar recogida'}</DialogTitle>
      <DialogContent>
        <TextField
          label="Hora"
          type="time"
          value={hora}
          onChange={(e) => setHora(e.target.value)}
          fullWidth
          margin="dense"
        />
        <TextField
          label="Maples"
          value={maples}
          onChange={(e) => setMaples(e.target.value)}
          inputMode="numeric"
          fullWidth
          margin="dense"
        />
        <TextField
          label="Unidades sueltas"
          value={sueltos}
          onChange={(e) => setSueltos(e.target.value)}
          inputMode="numeric"
          fullWidth
          margin="dense"
        />
        <Typography>= {totalHuevos(Number(maples) || 0, Number(sueltos) || 0)} huevos</Typography>
        <TextField
          label="Maples de descarte"
          value={descarteMaples}
          onChange={(e) => setDescarteMaples(e.target.value)}
          fullWidth
          margin="dense"
        />
        <TextField
          label="Unidades de descarte"
          value={descarteSueltos}
          onChange={(e) => setDescarteSueltos(e.target.value)}
          fullWidth
          margin="dense"
        />
        {guardar.isError && (
          <Alert severity="error" sx={{ mt: 1 }}>
            {guardar.error instanceof ApiError
              ? guardar.error.message
              : 'No se pudo registrar la recogida.'}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={alCerrar}>Cancelar</Button>
        <Button onClick={() => guardar.mutate()} disabled={guardar.isPending}>
          Guardar
        </Button>
      </DialogActions>
    </Dialog>
  );
}
