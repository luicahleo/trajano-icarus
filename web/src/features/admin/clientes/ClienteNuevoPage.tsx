import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import VisibilityOffRoundedIcon from '@mui/icons-material/VisibilityOffRounded';
import VisibilityRoundedIcon from '@mui/icons-material/VisibilityRounded';
import { Alert, Box, Button, Card, IconButton, InputAdornment, TextField, Typography } from '@mui/material';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { ApiError } from '../../../lib/http';
import { crearCliente } from './api';

const CLAVE_CLIENTES = ['clientes'] as const;

const esquema = z.object({
  razonSocial: z
    .string()
    .min(1, 'La razón social es obligatoria.')
    .max(200, 'La razón social no puede superar los 200 caracteres.'),
  identificadorFiscal: z
    .string()
    .min(1, 'El NIT es obligatorio.')
    .regex(/^\d{1,15}$/, 'El NIT debe contener solo dígitos y tener como máximo 15 caracteres.'),
  email: z.string().min(1, 'El correo es obligatorio.').email('Correo inválido.'),
  contrasena: z.string().min(12, 'La contraseña debe tener al menos 12 caracteres.'),
  confirmacionContrasena: z.string().min(1, 'Confirma la contraseña.'),
}).refine((valores) => valores.contrasena === valores.confirmacionContrasena, {
  path: ['confirmacionContrasena'], message: 'Las contraseñas no coinciden.',
});

type Esquema = z.infer<typeof esquema>;

export function ClienteNuevoPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [errorApi, setErrorApi] = useState<string | null>(null);
  const [mostrarContrasena, setMostrarContrasena] = useState(false);
  const {
    register, setError,
    handleSubmit,
    formState: { errors },
  } = useForm<Esquema>({ resolver: zodResolver(esquema) });

  const crear = useMutation({
    mutationFn: crearCliente,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: CLAVE_CLIENTES });
      navigate('/admin/clientes');
    },
    onError: (error) => {
      if (error instanceof ApiError && error.erroresValidacion) {
        const nit = error.erroresValidacion.IdentificadorFiscal?.[0];
        if (nit) setError('identificadorFiscal', { type: 'server', message: nit });
      }
      setErrorApi(error instanceof ApiError ? error.code ?? 'No se pudo crear el cliente.' : 'No se pudo crear el cliente.');
    },
  });

  const onEnviar = handleSubmit((valores) => {
    setErrorApi(null);
    crear.mutate(valores);
  });

  return (
    <Box sx={{ p: 4 }}>
      <Typography variant="h4" sx={{ mb: 3 }}>
        Nuevo cliente
      </Typography>
      <Card sx={{ maxWidth: 520, p: 4 }}>
        <Box component="form" onSubmit={onEnviar} noValidate sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TextField
            label="Razón social"
            fullWidth
            {...register('razonSocial')}
            error={Boolean(errors.razonSocial)}
            helperText={errors.razonSocial?.message}
          />
          <TextField
            label="NIT"
            fullWidth
            {...register('identificadorFiscal')}
            error={Boolean(errors.identificadorFiscal)}
            helperText={errors.identificadorFiscal?.message}
          />
          <TextField
            label="Correo electrónico"
            type="email"
            autoComplete="off"
            fullWidth
            {...register('email')}
            error={Boolean(errors.email)}
            helperText={errors.email?.message}
          />
          <TextField
            label="Contraseña"
            type={mostrarContrasena ? 'text' : 'password'}
            autoComplete="new-password"
            fullWidth
            {...register('contrasena')}
            error={Boolean(errors.contrasena)}
            helperText={errors.contrasena?.message}
            slotProps={{ input: { endAdornment: <InputAdornment position="end"><IconButton aria-label={mostrarContrasena ? 'Ocultar contraseña' : 'Mostrar contraseña'} onClick={() => setMostrarContrasena(!mostrarContrasena)} edge="end">{mostrarContrasena ? <VisibilityOffRoundedIcon /> : <VisibilityRoundedIcon />}</IconButton></InputAdornment> } }}
          />
          <TextField label="Confirmar contraseña" type={mostrarContrasena ? 'text' : 'password'} autoComplete="new-password" fullWidth {...register('confirmacionContrasena')} error={Boolean(errors.confirmacionContrasena)} helperText={errors.confirmacionContrasena?.message} />
          {errorApi && <Alert severity="error">{errorApi}</Alert>}
          <Button type="submit" variant="contained" size="large" disabled={crear.isPending}>
            Crear cliente
          </Button>
        </Box>
      </Card>
    </Box>
  );
}
