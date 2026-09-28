import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import DeleteOutlineRoundedIcon from '@mui/icons-material/DeleteOutlineRounded';
import AddRoundedIcon from '@mui/icons-material/AddRounded';
import { useState } from 'react';
import type { JornadaAccesoDetalle, TipoMarcacion, ValorCorregido } from '../../lib/tipos';

interface FilaValor {
  tipo: TipoMarcacion;
  local: string;
}

function aInputLocal(iso: string): string {
  const fecha = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${fecha.getFullYear()}-${pad(fecha.getMonth() + 1)}-${pad(fecha.getDate())}T${pad(
    fecha.getHours(),
  )}:${pad(fecha.getMinutes())}`;
}

interface CorreccionJornadaDialogProps {
  abierto: boolean;
  jornada: JornadaAccesoDetalle | null;
  pendiente: boolean;
  error: string | null;
  onCancelar: () => void;
  onGuardar: (motivo: string, valores: ValorCorregido[]) => void;
}

export function CorreccionJornadaDialog({
  abierto,
  jornada,
  pendiente,
  error,
  onCancelar,
  onGuardar,
}: CorreccionJornadaDialogProps) {
  const [motivo, setMotivo] = useState('');
  const [filas, setFilas] = useState<FilaValor[]>(() =>
    (jornada?.valoresEfectivos ?? []).map((valor) => ({
      tipo: valor.tipo,
      local: aInputLocal(valor.instanteUtc),
    })),
  );
  const [errorLocal, setErrorLocal] = useState<string | null>(null);

  const agregar = () => {
    const siguiente: TipoMarcacion = filas.length % 2 === 0 ? 'Entrada' : 'Salida';
    setFilas((actuales) => [...actuales, { tipo: siguiente, local: '' }]);
  };

  const quitarUltimo = () => setFilas((actuales) => actuales.slice(0, -1));

  const actualizar = (indice: number, local: string) =>
    setFilas((actuales) => actuales.map((fila, i) => (i === indice ? { ...fila, local } : fila)));

  const guardar = () => {
    if (!motivo.trim()) {
      setErrorLocal('El motivo de la corrección es obligatorio.');
      return;
    }
    if (filas.some((fila) => !fila.local)) {
      setErrorLocal('Completa la hora de cada marcación.');
      return;
    }
    for (let i = 0; i + 1 < filas.length; i += 2) {
      if (new Date(filas[i + 1].local) <= new Date(filas[i].local)) {
        setErrorLocal('La salida debe ser posterior a la entrada.');
        return;
      }
    }
    setErrorLocal(null);
    onGuardar(
      motivo.trim(),
      filas.map((fila) => ({ tipo: fila.tipo, instanteUtc: new Date(fila.local).toISOString() })),
    );
  };

  return (
    <Dialog open={abierto} onClose={onCancelar} fullWidth maxWidth="sm">
      <DialogTitle>Corregir jornada</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2" color="text.secondary">
            Los valores originales no se borran: la corrección queda auditada.
          </Typography>
          {filas.map((fila, indice) => (
            <Stack
              key={`${fila.tipo}-${indice}`}
              direction="row"
              spacing={1}
              sx={{ alignItems: 'center' }}
            >
              <Typography sx={{ minWidth: 72 }}>{fila.tipo}</Typography>
              <TextField
                label={`Hora de ${fila.tipo.toLowerCase()}`}
                type="datetime-local"
                value={fila.local}
                onChange={(evento) => actualizar(indice, evento.target.value)}
                slotProps={{ inputLabel: { shrink: true } }}
                size="small"
                fullWidth
              />
            </Stack>
          ))}
          <Stack direction="row" spacing={1}>
            <Button size="small" startIcon={<AddRoundedIcon />} onClick={agregar}>
              Agregar marcación
            </Button>
            {filas.length > 0 && (
              <Button
                size="small"
                color="error"
                startIcon={<DeleteOutlineRoundedIcon />}
                onClick={quitarUltimo}
              >
                Quitar última
              </Button>
            )}
          </Stack>
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
