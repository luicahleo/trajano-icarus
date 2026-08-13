import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Box, Button, Card, TextField, Typography } from '@mui/material';
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
    .min(1, 'El identificador fiscal es obligatorio.')
    .max(32, 'El identificador fiscal no puede superar los 32 caracteres.'),
});

type Esquema = z.infer<typeof esquema>;

export function ClienteNuevoPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [errorApi, setErrorApi] = useState<string | null>(null);
  const {
    register,
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
      if (error instanceof ApiError) setErrorApi(error.code ?? 'No se pudo crear el cliente.');
      else setErrorApi('No se pudo crear el cliente.');
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
            label="Identificador fiscal"
            fullWidth
            {...register('identificadorFiscal')}
            error={Boolean(errors.identificadorFiscal)}
            helperText={errors.identificadorFiscal?.message}
          />
          {errorApi && <Alert severity="error">{errorApi}</Alert>}
          <Button type="submit" variant="contained" size="large" disabled={crear.isPending}>
            Crear cliente
          </Button>
        </Box>
      </Card>
    </Box>
  );
}
