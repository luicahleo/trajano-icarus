import { Button, Card, CardContent, Stack, TextField, Typography, Alert } from '@mui/material';
import { useState } from 'react';
import { CampoContrasena } from '../../app/ui/CampoContrasena';
import { ApiError } from '../../lib/http';
import { useAuth } from '../auth/AuthContext';
import { activarSesionKiosco, cerrarSesionKiosco } from './api';

export function SesionKioscoPanel() {
  const { correo } = useAuth();
  const [email, setEmail] = useState(correo ?? '');
  const [contrasena, setContrasena] = useState('');
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pendiente, setPendiente] = useState(false);

  const ejecutar = async (accion: 'activar' | 'cerrar') => {
    setError(null);
    setMensaje(null);
    setPendiente(true);
    try {
      if (accion === 'activar') {
        await activarSesionKiosco({ email, contrasena });
        setMensaje('Sesión de kiosco activada en este navegador.');
      } else {
        await cerrarSesionKiosco({ email, contrasena });
        setMensaje('Modo kiosco finalizado.');
      }
      setContrasena('');
    } catch (fallo) {
      setError(
        fallo instanceof ApiError
          ? (fallo.code ?? 'No se pudo completar la acción.')
          : 'No se pudo completar la acción.',
      );
    } finally {
      setPendiente(false);
    }
  };

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6">Sesión del kiosco</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Activa el modo kiosco en el dispositivo dedicado confirmando tus credenciales. La salida
          también exige reautenticación.
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
          <Stack direction="row" spacing={1}>
            <Button
              variant="contained"
              disabled={pendiente}
              onClick={() => void ejecutar('activar')}
            >
              Activar kiosco
            </Button>
            <Button
              variant="outlined"
              disabled={pendiente}
              onClick={() => void ejecutar('cerrar')}
            >
              Salir del modo kiosco
            </Button>
          </Stack>
          {mensaje && <Alert severity="success">{mensaje}</Alert>}
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </CardContent>
    </Card>
  );
}
