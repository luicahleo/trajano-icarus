import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Box, Button, CircularProgress, Container, TextField, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { Navigate, useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { ApiError } from '../../lib/http';
import { useFuncionalidad } from '../auth/useFuncionalidad';
import { crearGranja, listarGranjas } from './api';

export const CLAVE_GRANJAS = ['avicola', 'granjas'] as const;

const esquema = z.object({ nombre: z.string().trim().min(1, 'Ingresá el nombre de la granja.') });
type DatosFormulario = z.infer<typeof esquema>;

export function AvicolaInicioPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const puedeCrearGranja = useFuncionalidad('Granjas');
  const granjas = useQuery({ queryKey: CLAVE_GRANJAS, queryFn: listarGranjas });
  const { register, handleSubmit, formState: { errors } } = useForm<DatosFormulario>({ resolver: zodResolver(esquema) });

  const creacion = useMutation({
    mutationFn: (datos: DatosFormulario) => crearGranja(datos.nombre),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: CLAVE_GRANJAS });
      navigate('/avicola/galpones');
    },
  });

  if (granjas.isLoading) return <CircularProgress sx={{ display: 'block', mx: 'auto', mt: 4 }} />;
  if (granjas.isError) return <Alert severity="error">No se pudo cargar la granja. Reintentá más tarde.</Alert>;
  if ((granjas.data ?? []).length > 0) return <Navigate to="/avicola/galpones" replace />;
  if (!puedeCrearGranja) {
    return <Container sx={{ py: 4 }}><Alert severity="info">La cuenta no tiene una granja configurada. Pedile al titular que la cree.</Alert></Container>;
  }

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Typography variant="h5" component="h1" gutterBottom>Creá tu granja</Typography>
      <Typography variant="body2" sx={{ mb: 2 }}>Es el primer paso: después vas a cargar los galpones.</Typography>
      {creacion.isError && <Alert severity="error" sx={{ mb: 2 }}>{creacion.error instanceof ApiError ? creacion.error.message : 'No se pudo crear la granja.'}</Alert>}
      <Box component="form" onSubmit={handleSubmit((datos) => creacion.mutate(datos))} sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <TextField label="Nombre de la granja" {...register('nombre')} error={!!errors.nombre} helperText={errors.nombre?.message} autoFocus />
        <Button type="submit" variant="contained" disabled={creacion.isPending}>Crear granja</Button>
      </Box>
    </Container>
  );
}
