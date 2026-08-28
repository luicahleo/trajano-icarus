import { Container } from '@mui/material';
import { PaginaCabecera } from './ui/PaginaCabecera';

export function InicioPage() {
  return (
    <Container maxWidth="lg" sx={{ py: 3 }}>
      <PaginaCabecera
        titulo="Inicio"
        subtitulo="Tu rol todavía no tiene módulos habilitados en esta aplicación."
      />
    </Container>
  );
}
