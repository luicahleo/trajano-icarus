import { CircularProgress, Container } from '@mui/material';

export function CargandoRuta() {
  return (
    <Container
      component="section"
      role="status"
      aria-label="Cargando página"
      sx={{ py: 4, display: 'grid', placeItems: 'center' }}
    >
      <CircularProgress />
    </Container>
  );
}
