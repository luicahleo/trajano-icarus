import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Box, Button, Card, Container, TextField } from '@mui/material';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { CampoContrasena } from '../../../app/ui/CampoContrasena';
import { PaginaCabecera } from '../../../app/ui/PaginaCabecera';
import { ApiError } from '../../../lib/http';
import { CLAVE_CLIENTES, crearCliente } from './api';

const esquema = z
  .object({
    razonSocial: z
      .string()
      .min(1, 'La razón social es obligatoria.')
      .max(200, 'La razón social no puede superar los 200 caracteres.'),
    identificadorFiscal: z
      .string()
      .min(1, 'El NIT es obligatorio.')
      .regex(/^\d{1,15}$/, 'El NIT debe contener solo dígitos y tener como máximo 15 caracteres.'),
    email: z.string().min(1, 'El correo es obligatorio.').email('Correo inválido.'),
    contrasena: z
      .string()
      .min(12, 'La contraseña debe tener al menos 12 caracteres.')
      .regex(/[A-Z]/, 'La contraseña debe incluir una mayúscula.')
      .regex(/[a-z]/, 'La contraseña debe incluir una minúscula.')
      .regex(/[0-9]/, 'La contraseña debe incluir un número.')
      .regex(/[^a-zA-Z0-9]/, 'La contraseña debe incluir un símbolo.'),
    confirmacionContrasena: z.string().min(1, 'Confirma la contraseña.'),
  })
  .refine((valores) => valores.contrasena === valores.confirmacionContrasena, {
    path: ['confirmacionContrasena'],
    message: 'Las contraseñas no coinciden.',
  });

type Esquema = z.infer<typeof esquema>;

export function ClienteNuevoPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [errorApi, setErrorApi] = useState<string | null>(null);
  const {
    register,
    setError,
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
      setErrorApi(
        error instanceof ApiError
          ? (error.code ?? 'No se pudo crear el cliente.')
          : 'No se pudo crear el cliente.',
      );
    },
  });

  const onEnviar = handleSubmit((valores) => {
    setErrorApi(null);
    crear.mutate(valores);
  });

  return (
    <Container maxWidth="lg" sx={{ py: 3 }}>
      <PaginaCabecera titulo="Nuevo cliente" />
      <Card sx={{ maxWidth: 520, p: 4 }}>
        <Box
          component="form"
          onSubmit={onEnviar}
          noValidate
          sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}
        >
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
          <CampoContrasena
            label="Contraseña"
            autoComplete="new-password"
            fullWidth
            {...register('contrasena')}
            error={Boolean(errors.contrasena)}
            helperText={errors.contrasena?.message}
          />
          <CampoContrasena
            label="Confirmar contraseña"
            autoComplete="new-password"
            fullWidth
            {...register('confirmacionContrasena')}
            error={Boolean(errors.confirmacionContrasena)}
            helperText={errors.confirmacionContrasena?.message}
          />
          {errorApi && <Alert severity="error">{errorApi}</Alert>}
          <Button type="submit" variant="contained" size="large" disabled={crear.isPending}>
            Crear cliente
          </Button>
        </Box>
      </Card>
    </Container>
  );
}
