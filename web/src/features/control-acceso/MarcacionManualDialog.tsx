import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
} from '@mui/material';
import { useRef, useState } from 'react';
import type { TipoMarcacion, TrabajadorResumen } from '../../lib/tipos';
import { nuevoId } from './identificadores';

export interface DatosMarcacionManual {
  trabajadorId: string;
  tipo: TipoMarcacion;
  horaDeclaradaUtc: string;
  motivo: string;
  claveIdempotencia: string;
}

interface MarcacionManualDialogProps {
  abierto: boolean;
  trabajadores: TrabajadorResumen[];
  pendiente: boolean;
  error: string | null;
  onCancelar: () => void;
  onGuardar: (datos: DatosMarcacionManual) => void;
}

export function MarcacionManualDialog({
  abierto,
  trabajadores,
  pendiente,
  error,
  onCancelar,
  onGuardar,
}: MarcacionManualDialogProps) {
  const [trabajadorId, setTrabajadorId] = useState('');
  const [tipo, setTipo] = useState<TipoMarcacion>('Entrada');
  const [fechaHora, setFechaHora] = useState('');
  const [motivo, setMotivo] = useState('');
  const [errorLocal, setErrorLocal] = useState<string | null>(null);
  // Clave estable mientras el diálogo está abierto: un reintento no duplica.
  const clave = useRef(nuevoId());

  const guardar = () => {
    if (!trabajadorId) {
      setErrorLocal('Selecciona un trabajador.');
      return;
    }
    if (!fechaHora) {
      setErrorLocal('Indica la fecha y hora declarada.');
      return;
    }
    if (!motivo.trim()) {
      setErrorLocal('El motivo es obligatorio.');
      return;
    }
    setErrorLocal(null);
    onGuardar({
      trabajadorId,
      tipo,
      horaDeclaradaUtc: new Date(fechaHora).toISOString(),
      motivo: motivo.trim(),
      claveIdempotencia: clave.current,
    });
  };

  return (
    <Dialog open={abierto} onClose={onCancelar} fullWidth maxWidth="sm">
      <DialogTitle>Registrar marcación manual</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField
            select
            label="Trabajador"
            value={trabajadorId}
            onChange={(evento) => setTrabajadorId(evento.target.value)}
            fullWidth
          >
            {trabajadores.map((trabajador) => (
              <MenuItem key={trabajador.id} value={trabajador.id}>
                {trabajador.nombre}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            select
            label="Acción"
            value={tipo}
            onChange={(evento) => setTipo(evento.target.value as TipoMarcacion)}
            fullWidth
          >
            <MenuItem value="Entrada">Entrada</MenuItem>
            <MenuItem value="Salida">Salida</MenuItem>
          </TextField>
          <TextField
            label="Fecha y hora declarada"
            type="datetime-local"
            value={fechaHora}
            onChange={(evento) => setFechaHora(evento.target.value)}
            slotProps={{ inputLabel: { shrink: true } }}
            fullWidth
          />
          <TextField
            label="Motivo"
            value={motivo}
            onChange={(evento) => setMotivo(evento.target.value)}
            multiline
            minRows={2}
            fullWidth
          />
          {errorLocal && <Alert severity="error">{errorLocal}</Alert>}
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancelar}>Cancelar</Button>
        <Button variant="contained" onClick={guardar} disabled={pendiente}>
          Guardar
        </Button>
      </DialogActions>
    </Dialog>
  );
}
