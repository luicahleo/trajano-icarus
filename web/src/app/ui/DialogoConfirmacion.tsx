import { Button, Dialog, DialogActions, DialogContent, DialogTitle } from '@mui/material';
import type { ReactNode } from 'react';

interface DialogoConfirmacionProps {
  abierto: boolean;
  titulo: string;
  mensaje: ReactNode;
  etiquetaConfirmar?: string;
  color?: 'primary' | 'secondary' | 'error' | 'success' | 'info' | 'warning';
  pendiente?: boolean;
  onCancelar: () => void;
  onConfirmar: () => void;
}

export function DialogoConfirmacion({
  abierto,
  titulo,
  mensaje,
  etiquetaConfirmar = 'Confirmar',
  color = 'primary',
  pendiente = false,
  onCancelar,
  onConfirmar,
}: DialogoConfirmacionProps) {
  return (
    <Dialog open={abierto} onClose={onCancelar}>
      <DialogTitle>{titulo}</DialogTitle>
      <DialogContent>{mensaje}</DialogContent>
      <DialogActions>
        <Button onClick={onCancelar}>Cancelar</Button>
        <Button variant="contained" color={color} onClick={onConfirmar} disabled={pendiente}>
          {etiquetaConfirmar}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
