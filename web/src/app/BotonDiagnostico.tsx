import DownloadRoundedIcon from '@mui/icons-material/DownloadRounded';
import { Fab } from '@mui/material';
import {
  diagnosticoManualPermitido,
  exportarDiagnostico,
  modoDiagnosticoActivo,
} from '../lib/sesionDiagnostico';

export function BotonDiagnostico({
  permitido = diagnosticoManualPermitido(),
}: {
  permitido?: boolean;
}) {
  if (!modoDiagnosticoActivo(permitido)) return null;

  return (
    <Fab
      aria-label="Descargar diagnóstico"
      color="secondary"
      size="small"
      onClick={() => exportarDiagnostico(permitido)}
      sx={{ position: 'fixed', right: 16, bottom: 16, zIndex: (tema) => tema.zIndex.snackbar }}
    >
      <DownloadRoundedIcon />
    </Fab>
  );
}
