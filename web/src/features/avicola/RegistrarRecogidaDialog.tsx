import {
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
import { guardarRecogida } from './offline';
import { type DatosRecogida } from './api';
import { totalHuevos } from './formatos';

export function RegistrarRecogidaDialog({
  galponId,
  abierto,
  alCerrar,
}: {
  galponId: string;
  abierto: boolean;
  alCerrar: () => void;
}) {
  const [hora, setHora] = useState(new Date().toTimeString().slice(0, 5));
  const [maples, setMaples] = useState('');
  const [sueltos, setSueltos] = useState('');
  const [descarteMaples, setDescarteMaples] = useState('');
  const [descarteSueltos, setDescarteSueltos] = useState('');
  const qc = useQueryClient();
  const guardar = useMutation({
    mutationFn: () => {
      const d: DatosRecogida = {
        hora,
        cantidadMaples: Number(maples) || 0,
        unidadesIncompletas: Number(sueltos) || 0,
        maplesDescarte: Number(descarteMaples) || 0,
        unidadesDescarte: Number(descarteSueltos) || 0,
        idempotencyKey: crypto.randomUUID(),
      };
      return guardarRecogida(galponId, d); // true si quedó encolada
    },
    onSuccess: (encolada) => {
      if (!encolada) void qc.invalidateQueries({ queryKey: ['avicola'] });
      alCerrar(); // si encoló, el coordinador muestra el aviso «Guardado sin conexión»
    },
  });
  return (
    <Dialog open={abierto} onClose={alCerrar}>
      <DialogTitle>Registrar recogida</DialogTitle>
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
