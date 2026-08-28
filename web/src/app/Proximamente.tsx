import { Box, Chip, Container } from '@mui/material';

export function Proximamente() {
  return (
    <Container maxWidth="lg" sx={{ py: 3 }}>
      <Box sx={{ py: 6, textAlign: 'center' }}>
        <Chip label="Próximamente" color="secondary" />
      </Box>
    </Container>
  );
}
