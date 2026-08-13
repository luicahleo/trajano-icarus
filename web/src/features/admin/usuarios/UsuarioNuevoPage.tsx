import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Alert, Box, Button, Card, MenuItem, TextField, Typography } from '@mui/material';
import { useState } from 'react';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { z } from 'zod';
import { ApiError } from '../../../lib/http';
import type { Rol } from '../../../lib/tipos';
import { listarClientes } from '../clientes/api';
import { listarTrabajadores } from '../../trabajadores/api';
import { crearUsuario, type DatosNuevoUsuario } from './api';

const ROLES_VALORES = ['', 'Administrador', 'SoporteTecnico', 'Cliente', 'Trabajador'] as const;

const esquema = z
  .object({
    email: z.string().min(1, 'El correo es obligatorio.').email('Correo inválido.'),
    contrasena: z.string().min(12, 'La contraseña debe tener al menos 12 caracteres.'),
    rol: z.enum(ROLES_VALORES, { message: 'Selecciona un rol.' }),
    clienteId: z.string(),
    trabajadorId: z.string(),
  })
  .superRefine((valores, ctx) => {
    if (!valores.rol) {
      ctx.addIssue({ code: 'custom', path: ['rol'], message: 'Selecciona un rol.' });
    }
    const necesitaCliente = valores.rol === 'Cliente' || valores.rol === 'Trabajador';
    if (necesitaCliente && !valores.clienteId) {
      ctx.addIssue({ code: 'custom', path: ['clienteId'], message: 'Selecciona un cliente.' });
    }
    if (valores.rol === 'Trabajador' && !valores.trabajadorId) {
      ctx.addIssue({ code: 'custom', path: ['trabajadorId'], message: 'Selecciona un trabajador.' });
    }
  });

type Esquema = z.infer<typeof esquema>;

function aPayload(datos: Esquema): DatosNuevoUsuario {
  const necesitaCliente = datos.rol === 'Cliente' || datos.rol === 'Trabajador';
  const necesitaTrabajador = datos.rol === 'Trabajador';
  return {
    email: datos.email,
    contrasena: datos.contrasena,
    rol: datos.rol as Rol,
    clienteId: necesitaCliente ? datos.clienteId || null : null,
    trabajadorId: necesitaTrabajador ? datos.trabajadorId || null : null,
  };
}

export function UsuarioNuevoPage() {
  const [mensajeExito, setMensajeExito] = useState<string | null>(null);
  const [errorApi, setErrorApi] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    control,
    reset,
    formState: { errors },
  } = useForm<Esquema>({
    resolver: zodResolver(esquema),
    defaultValues: { email: '', contrasena: '', rol: '', clienteId: '', trabajadorId: '' },
  });

  const rol = useWatch({ control, name: 'rol' });
  const clienteId = useWatch({ control, name: 'clienteId' });
  const necesitaCliente = rol === 'Cliente' || rol === 'Trabajador';
  const necesitaTrabajador = rol === 'Trabajador';

  const { data: clientes } = useQuery({
    queryKey: ['clientes'],
    queryFn: listarClientes,
    enabled: necesitaCliente,
  });

  const { data: trabajadores } = useQuery({
    queryKey: ['trabajadores', clienteId],
    queryFn: () => listarTrabajadores(clienteId),
    enabled: necesitaTrabajador && Boolean(clienteId),
  });

  const crear = useMutation<{ id: string }, Error, DatosNuevoUsuario>({
    mutationFn: crearUsuario,
    onSuccess: () => {
      setMensajeExito('Cuenta creada correctamente.');
      setErrorApi(null);
      reset();
    },
    onError: (error) => {
      if (error instanceof ApiError) setErrorApi(error.code ?? 'No se pudo crear la cuenta.');
      else setErrorApi('No se pudo crear la cuenta.');
    },
  });

  const onEnviar = handleSubmit((datos) => {
    setMensajeExito(null);
    setErrorApi(null);
    crear.mutate(aPayload(datos));
  });

  return (
    <Box sx={{ p: 4 }}>
      <Typography variant="h4" sx={{ mb: 3 }}>
        Nueva cuenta de usuario
      </Typography>
      <Card sx={{ maxWidth: 560, p: 4 }}>
        <Box component="form" onSubmit={onEnviar} noValidate sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
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
            type="password"
            autoComplete="new-password"
            fullWidth
            {...register('contrasena')}
            error={Boolean(errors.contrasena)}
            helperText={errors.contrasena?.message}
          />
          <Controller
            name="rol"
            control={control}
            render={({ field }) => (
              <TextField select label="Rol" fullWidth {...field} error={Boolean(errors.rol)} helperText={errors.rol?.message}>
                <MenuItem value="" disabled>
                  Selecciona un rol
                </MenuItem>
                {ROLES_VALORES.slice(1).map((r) => (
                  <MenuItem key={r} value={r}>
                    {r}
                  </MenuItem>
                ))}
              </TextField>
            )}
          />
          {necesitaCliente && (
            <Controller
              name="clienteId"
              control={control}
              render={({ field }) => (
                <TextField
                  select
                  label="Cliente"
                  fullWidth
                  {...field}
                  error={Boolean(errors.clienteId)}
                  helperText={errors.clienteId?.message}
                >
                  <MenuItem value="" disabled>
                    Selecciona un cliente
                  </MenuItem>
                  {(clientes ?? []).map((c) => (
                    <MenuItem key={c.id} value={c.id}>
                      {c.razonSocial}
                    </MenuItem>
                  ))}
                </TextField>
              )}
            />
          )}
          {necesitaTrabajador && Boolean(clienteId) && (
            <Controller
              name="trabajadorId"
              control={control}
              render={({ field }) => (
                <TextField
                  select
                  label="Trabajador"
                  fullWidth
                  {...field}
                  error={Boolean(errors.trabajadorId)}
                  helperText={errors.trabajadorId?.message}
                >
                  <MenuItem value="" disabled>
                    Selecciona un trabajador
                  </MenuItem>
                  {(trabajadores ?? []).map((t) => (
                    <MenuItem key={t.id} value={t.id}>
                      {t.nombre}
                    </MenuItem>
                  ))}
                </TextField>
              )}
            />
          )}
          {errorApi && <Alert severity="error">{errorApi}</Alert>}
          {mensajeExito && <Alert severity="success">{mensajeExito}</Alert>}
          <Button type="submit" variant="contained" size="large" disabled={crear.isPending}>
            Crear cuenta
          </Button>
        </Box>
      </Card>
    </Box>
  );
}
