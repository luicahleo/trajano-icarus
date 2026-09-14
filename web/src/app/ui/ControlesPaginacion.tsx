import { Button, Stack, Typography } from '@mui/material';
import ChevronLeftRoundedIcon from '@mui/icons-material/ChevronLeftRounded';
import ChevronRightRoundedIcon from '@mui/icons-material/ChevronRightRounded';
import { contarPaginas } from '../../lib/paginacion';

// Controles de paginación reusables (spec 2026-09-14): el estado de página se
// anuncia como texto («Página 2 de 7»), no solo con iconos, y los botones
// llevan aria-label propio. No conoce pedidos ni despachos.
interface ControlesPaginacionProps {
  numeroPagina: number;
  total: number;
  tamanoPagina: number;
  onCambiarPagina: (pagina: number) => void;
}

export function ControlesPaginacion({
  numeroPagina,
  total,
  tamanoPagina,
  onCambiarPagina,
}: ControlesPaginacionProps) {
  const totalPaginas = contarPaginas(total, tamanoPagina);

  return (
    <Stack direction="row" spacing={2} alignItems="center" justifyContent="flex-end">
      <Button
        size="small"
        startIcon={<ChevronLeftRoundedIcon />}
        disabled={numeroPagina <= 1}
        aria-label="Página anterior"
        onClick={() => onCambiarPagina(numeroPagina - 1)}
      >
        Anterior
      </Button>
      <Typography variant="body2" color="text.secondary">
        Página {numeroPagina} de {totalPaginas}
      </Typography>
      <Button
        size="small"
        endIcon={<ChevronRightRoundedIcon />}
        disabled={numeroPagina >= totalPaginas}
        aria-label="Página siguiente"
        onClick={() => onCambiarPagina(numeroPagina + 1)}
      >
        Siguiente
      </Button>
    </Stack>
  );
}
