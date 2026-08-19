import { zodResolver } from '@hookform/resolvers/zod';
import VisibilityOffRoundedIcon from '@mui/icons-material/VisibilityOffRounded';
import VisibilityRoundedIcon from '@mui/icons-material/VisibilityRounded';
import { Alert, Box, Button, Card, IconButton, InputAdornment, TextField, Typography } from '@mui/material';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { inicioSegunRol } from '../../app/inicioSegunRol';
import { ApiError } from '../../lib/http';
import { obtenerMe } from './api';
import { useAuth } from './AuthContext';

const esquema = z.object({
  email: z.string().min(1, 'El correo es obligatorio.').email('Correo inválido.'),
  contrasena: z.string().min(1, 'La contraseña es obligatoria.'),
});

type Esquema = z.infer<typeof esquema>;

export function LoginPage() {
  const { iniciarSesion } = useAuth();
  const navigate = useNavigate();
  const [errorApi, setErrorApi] = useState<{ code: string; correlationId?: string } | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [mostrarContrasena, setMostrarContrasena] = useState(false);
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<Esquema>({ resolver: zodResolver(esquema) });

  const onEnviar = handleSubmit(async (valores) => {
    setEnviando(true);
    setErrorApi(null);
    try {
      await iniciarSesion(valores);
      const me = await obtenerMe();
      navigate(inicioSegunRol(me.rol, me.funcionalidades));
    } catch (error) {
      if (error instanceof ApiError) {
        setErrorApi({ code: error.code ?? `Error de servidor (${error.status})`, correlationId: error.correlationId });
      } else {
        setErrorApi({ code: 'No se pudo iniciar sesión. Inténtalo de nuevo.' });
      }
    } finally {
      setEnviando(false);
    }
  });

  return (
    <Box sx={{ minHeight: '100vh', display: 'grid', placeItems: 'center', bgcolor: 'background.default', p: 2 }}>
      <Card sx={{ width: '100%', maxWidth: 420, p: 4 }}>
        <Typography variant="h4" sx={{ mb: 3 }}>
          Iniciar sesión
        </Typography>
        <Box component="form" onSubmit={onEnviar} noValidate sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField
            label="Correo electrónico"
            type="email"
            autoComplete="email"
            fullWidth
            {...register('email')}
            error={Boolean(errors.email)}
            helperText={errors.email?.message}
          />
          <TextField
            label="Contraseña"
            type={mostrarContrasena ? 'text' : 'password'}
            autoComplete="current-password"
            fullWidth
            {...register('contrasena')}
            error={Boolean(errors.contrasena)}
            helperText={errors.contrasena?.message}
            slotProps={{ input: { endAdornment: <InputAdornment position="end"><IconButton aria-label={mostrarContrasena ? 'Ocultar contraseña' : 'Mostrar contraseña'} onClick={() => setMostrarContrasena(!mostrarContrasena)} edge="end">{mostrarContrasena ? <VisibilityOffRoundedIcon /> : <VisibilityRoundedIcon />}</IconButton></InputAdornment> } }}
          />
          {errorApi && (
            <Alert severity="error">
              {errorApi.code}
              {errorApi.correlationId ? ` (ID: ${errorApi.correlationId})` : ''}
            </Alert>
          )}
          <Button type="submit" variant="contained" size="large" disabled={enviando}>
            Iniciar sesión
          </Button>
        </Box>
      </Card>
    </Box>
  );
}
