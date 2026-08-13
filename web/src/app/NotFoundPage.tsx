import { Box, Typography } from '@mui/material';

export function NotFoundPage() {
  return (
    <Box sx={{ p: 4 }}>
      <Typography variant="h4">Página no encontrada</Typography>
      <Typography sx={{ color: 'text.secondary' }}>La página que buscas no existe.</Typography>
    </Box>
  );
}
