import { Box, Button, Typography } from '@mui/material';
import type { TypographyProps } from '@mui/material';
import { useState } from 'react';
import { LIMITE_TEXTO_LARGO } from './tareasPendientes';

const RECORTE_DOS_LINEAS = {
  display: '-webkit-box',
  WebkitLineClamp: 2,
  WebkitBoxOrient: 'vertical',
  overflow: 'hidden',
} as const;

export function TextoRecortado({
  texto,
  variante = 'body1',
}: {
  texto: string;
  variante?: TypographyProps['variant'];
}) {
  const [expandido, setExpandido] = useState(false);
  const largo = texto.length > LIMITE_TEXTO_LARGO;
  return (
    <Box>
      <Typography
        variant={variante}
        sx={{ overflowWrap: 'anywhere', ...(largo && !expandido ? RECORTE_DOS_LINEAS : {}) }}
      >
        {texto}
      </Typography>
      {largo && (
        <Button
          size="small"
          aria-expanded={expandido}
          onClick={() => setExpandido((v) => !v)}
          sx={{ px: 0, minWidth: 0 }}
        >
          {expandido ? 'Ver menos' : 'Ver más'}
        </Button>
      )}
    </Box>
  );
}
