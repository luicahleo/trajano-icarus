import CloudUploadRoundedIcon from '@mui/icons-material/CloudUploadRounded';
import {
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  List,
  ListItem,
  ListItemText,
  Snackbar,
} from '@mui/material';
import { useEffect, useState } from 'react';
import type { OperacionPendiente } from '../../lib/offline/tipos';
import { DialogoConfirmacion } from '../ui/DialogoConfirmacion';
import {
  descartarOperacion,
  listarOperaciones,
  reintentarOperacion,
  suscribirAvisos,
} from './coordinador';
import { usePendientesOffline } from './usePendientesOffline';

const tituloTipo = (op: OperacionPendiente) =>
  op.tipo === 'produccion.crear' ? 'Recogida' : 'Bajas';

export function PendientesOffline() {
  const pendientes = usePendientesOffline();
  const [abierto, setAbierto] = useState(false);
  const [operaciones, setOperaciones] = useState<OperacionPendiente[]>([]);
  const [aDescartar, setADescartar] = useState<OperacionPendiente | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);

  useEffect(() => suscribirAvisos(setAviso), []);

  const abrir = async () => {
    setOperaciones(await listarOperaciones());
    setAbierto(true);
  };
  const refrescar = async () => setOperaciones(await listarOperaciones());

  return (
    <>
      {pendientes > 0 && (
        <Chip
          icon={<CloudUploadRoundedIcon />}
          color="warning"
          size="small"
          component="button"
          onClick={() => void abrir()}
          aria-label={
            pendientes === 1
              ? '1 pendiente de sincronizar'
              : `${pendientes} pendientes de sincronizar`
          }
          label={pendientes === 1 ? '1 pendiente' : `${pendientes} pendientes`}
        />
      )}
      <Dialog open={abierto} onClose={() => setAbierto(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Registros pendientes de sincronizar</DialogTitle>
        <DialogContent>
          <List>
            {operaciones.map((op) => (
              <ListItem
                key={op.id}
                secondaryAction={
                  <>
                    {op.estado === 'error' && (
                      <Button
                        size="small"
                        onClick={() => void reintentarOperacion(op.id).then(refrescar)}
                      >
                        Reintentar
                      </Button>
                    )}
                    <Button size="small" color="error" onClick={() => setADescartar(op)}>
                      Descartar
                    </Button>
                  </>
                }
              >
                <ListItemText
                  primary={tituloTipo(op)}
                  secondary={`${new Date(op.creadoEn).toLocaleString()} · ${
                    op.estado === 'error' ? 'Error al sincronizar' : 'Pendiente'
                  }`}
                />
              </ListItem>
            ))}
          </List>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAbierto(false)}>Cerrar</Button>
        </DialogActions>
      </Dialog>
      <DialogoConfirmacion
        abierto={aDescartar !== null}
        titulo="Descartar registro"
        mensaje="El registro no se sincronizará y se perderá. ¿Continuar?"
        etiquetaConfirmar="Confirmar"
        color="error"
        pendiente={false}
        onCancelar={() => setADescartar(null)}
        onConfirmar={() => {
          if (aDescartar) void descartarOperacion(aDescartar.id).then(refrescar);
          setADescartar(null);
        }}
      />
      <Snackbar
        open={aviso !== null}
        autoHideDuration={4000}
        onClose={() => setAviso(null)}
        message={aviso ?? ''}
      />
    </>
  );
}
