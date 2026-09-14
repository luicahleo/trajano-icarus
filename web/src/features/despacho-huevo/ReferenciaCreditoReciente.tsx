import { Typography } from '@mui/material';
import { formatoMonedaExacta } from '../../lib/formatos';

interface Props {
  recibidoReciente: number;
  diasReferencia: number;
}

// Ritmo de liquidación de CAISY (corrección 2026-09-14): el crédito ya cuenta
// desde la recepción, pero saber qué parte es reciente le sirve al Cliente
// para decidir cuánto pedir. Es informativo y no condiciona nada, así que se
// muestra en tono secundario y nunca en rojo. Reusado por
// PedidoFormularioPage.tsx y DespachoHuevoFormularioPage.tsx.
export function ReferenciaCreditoReciente({ recibidoReciente, diasReferencia }: Props) {
  if (!recibidoReciente) return null;
  return (
    <Typography variant="caption" color="text.secondary" component="p">
      De ese total, {formatoMonedaExacta(recibidoReciente)} se recibieron en los últimos{' '}
      {diasReferencia} días.
    </Typography>
  );
}
