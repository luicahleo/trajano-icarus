import { Container } from '@mui/material';
import { PaginaCabecera } from './ui/PaginaCabecera';

export function NotFoundPage() {
  return (
    <Container maxWidth="lg" sx={{ py: 3 }}>
      <PaginaCabecera titulo="Página no encontrada" subtitulo="La página que buscas no existe." />
    </Container>
  );
}
