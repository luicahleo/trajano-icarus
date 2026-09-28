import { Alert, Button, Card, CardContent, Stack, TextField, Typography } from '@mui/material';
import { useState } from 'react';
import { CampoContrasena } from '../app/ui/CampoContrasena';
import { ApiError } from '../lib/http';
import { activarKiosco } from './apiKiosco';

export function ActivacionKioscoPage({ onActivado }: { onActivado: () => void }) {
  const [email, setEmail] = useState('');
  const [contrasena, setContrasena] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pendiente, setPendiente] = useState(false);

  const activar = async () => {
    setPendiente(true);
    setError(null);
    try {
      await activarKiosco({ email, contrasena });
      onActivado();
    } catch (fallo) {
      setError(
        fallo instanceof ApiError
          ? (fallo.code ?? 'No se pudo activar el kiosco.')
          : 'No se pudo activar el kiosco.',
      );
    } finally {
      setPendiente(false);
    }
  };

  return (
    <Stack sx={{ alignItems: 'center', justifyContent: 'center', minHeight: '100dvh', p: 2 }}>
      <Card sx={{ maxWidth: 420, width: '100%' }}>
        <CardContent>
          <Typography variant="h5" component="h1" gutterBottom>
            Activar kiosco
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Un cliente del establecimiento confirma sus credenciales para habilitar este dispositivo.
          </Typography>
          <Stack spacing={2}>
            <TextField
              label="Correo"
              type="email"
              value={email}
              onChange={(evento) => setEmail(evento.target.value)}
              fullWidth
            />
            <CampoContrasena
              label="Contraseña"
              value={contrasena}
              onChange={(evento) => setContrasena(evento.target.value)}
              fullWidth
            />
            <Button variant="contained" size="large" disabled={pendiente} onClick={() => void activar()}>
              Activar
            </Button>
            {error && <Alert severity="error">{error}</Alert>}
          </Stack>
        </CardContent>
      </Card>
    </Stack>
  );
}
