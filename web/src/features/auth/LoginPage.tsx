import { Box, Typography } from '@mui/material';
import { Proximamente } from '../../app/Proximamente';

export function LoginPage() {
  return (
    <Box sx={{ p: 4 }}>
      <Typography variant="h4">Iniciar sesión</Typography>
      <Proximamente />
    </Box>
  );
}
