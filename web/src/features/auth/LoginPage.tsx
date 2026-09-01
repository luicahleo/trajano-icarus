import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Box, Button, Card, TextField, Typography } from '@mui/material';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { CampoContrasena } from '../../app/ui/CampoContrasena';
import { inicioSegunRol } from '../../app/inicioSegunRol';
import { useConexion } from '../../app/useConexion';
import { obtenerSesionOffline } from '../../app/offline/sesionOffline';
import { ApiError } from '../../lib/http';
import type { UsuarioActual } from '../../lib/tipos';
import { obtenerMe } from './api';
import { useAuth } from './AuthContext';

const esquema = z.object({
  email: z.string().min(1, 'El correo es obligatorio.').email('Correo inválido.'),
  contrasena: z.string().min(1, 'La contraseña es obligatoria.'),
});

type Esquema = z.infer<typeof esquema>;

export function LoginPage() {
  const { iniciarSesion, entrarSinConexion } = useAuth();
  const navigate = useNavigate();
  const online = useConexion();
  const [errorApi, setErrorApi] = useState<{ code: string; correlationId?: string } | null>(null);
  const [enviando, setEnviando] = useState(false);
  // Snapshot vigente para ofrecer la entrada offline (spec decisión 6): solo
  // existe si el último usuario del dispositivo fue un Trabajador.
  const [snapshotOffline, setSnapshotOffline] = useState<UsuarioActual | null>(null);
  useEffect(() => {
    if (online) return; // el render ya lo oculta con `!online`; no hace falta limpiar
    let activo = true;
    obtenerSesionOffline()
      .then((snapshot) => {
        if (activo) setSnapshotOffline(snapshot);
      })
      .catch(() => undefined);
    return () => {
      activo = false;
    };
  }, [online]);
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<Esquema>({ resolver: zodResolver(esquema) });

  const onEntrarOffline = async () => {
    const snapshot = await entrarSinConexion();
    if (snapshot) navigate(inicioSegunRol(snapshot.rol, snapshot.funcionalidades));
  };

  const onEnviar = handleSubmit(async (valores) => {
    setEnviando(true);
    setErrorApi(null);
    try {
      await iniciarSesion(valores);
      const me = await obtenerMe();
      navigate(inicioSegunRol(me.rol, me.funcionalidades));
    } catch (error) {
      if (error instanceof ApiError) {
        setErrorApi({
          code: error.code ?? `Error de servidor (${error.status})`,
          correlationId: error.correlationId,
        });
      } else {
        setErrorApi({ code: 'No se pudo iniciar sesión. Inténtalo de nuevo.' });
      }
    } finally {
      setEnviando(false);
    }
  });

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'grid',
        placeItems: 'center',
        bgcolor: 'background.default',
        p: 2,
      }}
    >
      <Card sx={{ width: '100%', maxWidth: 420, p: 4 }}>
        <Typography variant="h4" sx={{ mb: 3 }}>
          Iniciar sesión
        </Typography>
        <Box
          component="form"
          onSubmit={onEnviar}
          noValidate
          sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}
        >
          <TextField
            label="Correo electrónico"
            type="email"
            autoComplete="email"
            fullWidth
            {...register('email')}
            error={Boolean(errors.email)}
            helperText={errors.email?.message}
          />
          <CampoContrasena
            label="Contraseña"
            autoComplete="current-password"
            fullWidth
            {...register('contrasena')}
            error={Boolean(errors.contrasena)}
            helperText={errors.contrasena?.message}
          />
          {errorApi && (
            <Alert severity="error">
              {errorApi.code}
              {errorApi.correlationId ? ` (ID: ${errorApi.correlationId})` : ''}
            </Alert>
          )}
          {!online && snapshotOffline && (
            <Alert
              severity="info"
              action={
                <Button color="inherit" size="small" onClick={() => void onEntrarOffline()}>
                  Continuar sin conexión
                </Button>
              }
            >
              Sin conexión: puedes continuar con la sesión guardada en este dispositivo. Los
              registros se sincronizarán al volver la red.
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
