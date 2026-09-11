import { Stack, Typography } from '@mui/material';
import { formatoMonedaExacta } from '../../lib/formatos';
import type { AjusteCreditoHuevo } from './api';
import { formatoFecha } from './constantes';

interface Props {
  ajustes: AjusteCreditoHuevo[];
}

// Desglose visible del crédito (spec, ítem 2 del backlog): solo los ajustes
// de corrección con su motivo, el único componente del cálculo que
// representa una sorpresa real para el cliente. Reusado en
// PedidoFormularioPage.tsx y PedidoAlimentoDetallePage.tsx.
export function AjustesCreditoHuevo({ ajustes }: Props) {
  if (ajustes.length === 0) return null;
  return (
    <Stack spacing={0.5} sx={{ mt: 1 }}>
      <Typography variant="caption" color="text.secondary">
        Correcciones aplicadas a tu crédito:
      </Typography>
      {ajustes.map((a) => (
        <Typography key={a.id} variant="body2" color={a.monto < 0 ? 'error' : 'text.secondary'}>
          {formatoFecha(a.fecha)} — {a.motivo} ({formatoMonedaExacta(a.monto)})
        </Typography>
      ))}
    </Stack>
  );
}
