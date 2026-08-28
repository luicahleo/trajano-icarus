import { Box, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';

interface PaginaCabeceraProps {
  titulo: ReactNode;
  subtitulo?: ReactNode;
  acciones?: ReactNode;
  variante?: 'h1' | 'h2' | 'h3' | 'h4' | 'h5' | 'h6';
}

export function PaginaCabecera({
  titulo,
  subtitulo,
  acciones,
  variante = 'h4',
}: PaginaCabeceraProps) {
  return (
    <Stack
      direction="row"
      spacing={2}
      sx={{ justifyContent: 'space-between', alignItems: 'flex-start', mb: 3, flexWrap: 'wrap' }}
    >
      <Box>
        <Typography variant={variante}>{titulo}</Typography>
        {subtitulo && (
          <Typography variant="body2" color="text.secondary">
            {subtitulo}
          </Typography>
        )}
      </Box>
      {acciones && (
        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexShrink: 0 }}>{acciones}</Box>
      )}
    </Stack>
  );
}
