import { Box, Typography } from '@mui/material';

export function InicioPage() {
  return (
    <Box sx={{ p: 4 }}>
      <Typography variant="h4">Inicio</Typography>
      <Typography sx={{ color: 'text.secondary' }}>
        Tu rol todavía no tiene módulos habilitados en esta aplicación.
      </Typography>
    </Box>
  );
}
