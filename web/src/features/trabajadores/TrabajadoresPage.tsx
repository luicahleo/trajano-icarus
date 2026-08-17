import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  InputAdornment,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import VisibilityOffRoundedIcon from '@mui/icons-material/VisibilityOffRounded';
import VisibilityRoundedIcon from '@mui/icons-material/VisibilityRounded';
import { ApiError } from '../../lib/http';
import type { TrabajadorResumen } from '../../lib/tipos';
import { useAuth } from '../auth/AuthContext';
import { cesarTrabajador, crearTrabajador, desactivarTrabajador, listarTrabajadores } from './api';

const esquemaAlta = z.object({
  nombre: z.string().min(1, 'El nombre es obligatorio.'),
  documentoIdentidad: z.string().min(1, 'El documento de identidad es obligatorio.'),
  cargo: z.string().min(1, 'El cargo es obligatorio.'),
  fechaIngreso: z.string().min(1, 'La fecha de ingreso es obligatoria.'),
  email: z.string().min(1, 'El correo es obligatorio.').email('Correo inválido.'),
  contrasena: z.string().min(12, 'La contraseña debe tener al menos 12 caracteres.'),
  confirmacionContrasena: z.string().min(1, 'Confirma la contraseña.'),
}).refine((valores) => valores.contrasena === valores.confirmacionContrasena, {
  path: ['confirmacionContrasena'], message: 'Las contraseñas no coinciden.',
});

type DatosAlta = z.infer<typeof esquemaAlta>;
const camposAlta = new Set<keyof DatosAlta>([
  'nombre', 'documentoIdentidad', 'cargo', 'fechaIngreso', 'email', 'contrasena', 'confirmacionContrasena',
]);

function fechaDeHoy(): string {
  const d = new Date();
  const mes = String(d.getMonth() + 1).padStart(2, '0');
  const dia = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${mes}-${dia}`;
}

export function TrabajadoresPage() {
  const { clienteId } = useAuth();
  const queryClient = useQueryClient();
  const [abiertaAlta, setAbiertaAlta] = useState(false);
  const [cesando, setCesando] = useState<TrabajadorResumen | null>(null);
  const [fechaCese, setFechaCese] = useState('');
  const [errorCese, setErrorCese] = useState<string | null>(null);
  const [desactivando, setDesactivando] = useState<TrabajadorResumen | null>(null);
  const [errorAlta, setErrorAlta] = useState<string | null>(null);
  const [mostrarContrasena, setMostrarContrasena] = useState(false);

  const claveTrabajadores = ['trabajadores', clienteId] as const;

  const { data: trabajadores, isLoading, isError } = useQuery({
    queryKey: claveTrabajadores,
    queryFn: () => listarTrabajadores(clienteId!),
    enabled: Boolean(clienteId),
  });

  const refrescar = () => queryClient.invalidateQueries({ queryKey: ['trabajadores', clienteId] });

  const {
    register, setError,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<DatosAlta>({ resolver: zodResolver(esquemaAlta) });

  const crear = useMutation({
    mutationFn: (datos: DatosAlta) => crearTrabajador(clienteId!, datos),
    onSuccess: () => {
      refrescar();
      setAbiertaAlta(false);
      reset();
    },
    onError: (error) => {
      if (error instanceof ApiError && error.erroresValidacion) {
        for (const [campo, mensajes] of Object.entries(error.erroresValidacion)) {
          const nombreCampo = campo.charAt(0).toLowerCase() + campo.slice(1);
          if (camposAlta.has(nombreCampo as keyof DatosAlta)) {
            setError(nombreCampo as keyof DatosAlta, { type: 'server', message: mensajes[0] });
          }
        }
      }
      setErrorAlta(error instanceof ApiError ? error.code ?? 'No se pudo crear el trabajador.' : 'No se pudo crear el trabajador.');
    },
  });

  const cesar = useMutation({
    mutationFn: (fecha: string) => cesarTrabajador(cesando!.id, fecha),
    onSuccess: () => {
      refrescar();
      setCesando(null);
    },
  });

  const desactivar = useMutation({
    mutationFn: () => desactivarTrabajador(desactivando!.id),
    onSuccess: () => {
      refrescar();
      setDesactivando(null);
    },
  });

  const onEnviarCese = () => {
    const hoy = fechaDeHoy();
    if (!fechaCese) {
      setErrorCese('La fecha de cese es obligatoria.');
      return;
    }
    if (fechaCese > hoy) {
      setErrorCese('La fecha de cese no puede ser futura.');
      return;
    }
    setErrorCese(null);
    cesar.mutate(fechaCese);
  };

  return (
    <Box sx={{ p: 4 }}>
      <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h4">Trabajadores</Typography>
        {clienteId && (
          <Button variant="contained" onClick={() => setAbiertaAlta(true)}>
            Nuevo trabajador
          </Button>
        )}
      </Stack>

      {isError && <Alert severity="error">No se pudo cargar la lista de trabajadores.</Alert>}
      {isLoading && <CircularProgress />}
      {trabajadores && (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Nombre</TableCell>
                <TableCell>Documento</TableCell>
                <TableCell>Cargo</TableCell>
                <TableCell>Fecha de ingreso</TableCell>
                <TableCell>Fecha de cese</TableCell>
                <TableCell align="right">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {trabajadores.map((t) => (
                <TableRow key={t.id}>
                  <TableCell>{t.nombre}</TableCell>
                  <TableCell>{t.documentoIdentidad}</TableCell>
                  <TableCell>{t.cargo}</TableCell>
                  <TableCell>{t.fechaIngreso}</TableCell>
                  <TableCell>{t.fechaCese ?? '—'}</TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end' }}>
                      <Button size="small" variant="outlined" onClick={() => setCesando(t)}>
                        Cesar
                      </Button>
                      <Button size="small" variant="outlined" color="error" onClick={() => setDesactivando(t)}>
                        Desactivar
                      </Button>
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog open={abiertaAlta} onClose={() => setAbiertaAlta(false)}>
        <DialogTitle>Nuevo trabajador</DialogTitle>
        <DialogContent>
          <Box
            component="form"
            id="form-alta-trabajador"
            onSubmit={handleSubmit((datos) => {
              setErrorAlta(null);
              crear.mutate(datos);
            })}
            noValidate
            sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1, minWidth: 420 }}
          >
            <TextField
              label="Nombre completo"
              fullWidth
              {...register('nombre')}
              error={Boolean(errors.nombre)}
              helperText={errors.nombre?.message}
            />
            <TextField
              label="Documento de identidad"
              fullWidth
              {...register('documentoIdentidad')}
              error={Boolean(errors.documentoIdentidad)}
              helperText={errors.documentoIdentidad?.message}
            />
            <TextField
              label="Cargo"
              fullWidth
              {...register('cargo')}
              error={Boolean(errors.cargo)}
              helperText={errors.cargo?.message}
            />
            <TextField
              label="Fecha de ingreso"
              type="date"
              fullWidth
              slotProps={{ inputLabel: { shrink: true } }}
              {...register('fechaIngreso')}
              error={Boolean(errors.fechaIngreso)}
              helperText={errors.fechaIngreso?.message}
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
            {errorAlta && <Alert severity="error">{errorAlta}</Alert>}
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAbiertaAlta(false)}>Cancelar</Button>
          <Button type="submit" form="form-alta-trabajador" variant="contained" disabled={crear.isPending}>
            Guardar
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={cesando !== null} onClose={() => setCesando(null)}>
        <DialogTitle>Cesar a {cesando?.nombre}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Fecha de cese"
              type="date"
              fullWidth
              slotProps={{ inputLabel: { shrink: true } }}
              value={fechaCese}
              onChange={(e) => setFechaCese(e.target.value)}
              error={Boolean(errorCese)}
              helperText={errorCese}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCesando(null)}>Cancelar</Button>
          <Button variant="contained" onClick={onEnviarCese} disabled={cesar.isPending}>
            Confirmar
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={desactivando !== null} onClose={() => setDesactivando(null)}>
        <DialogTitle>Confirmar acción</DialogTitle>
        <DialogContent>¿Desactivar a {desactivando?.nombre}? Esta acción elimina su acceso.</DialogContent>
        <DialogActions>
          <Button onClick={() => setDesactivando(null)}>Cancelar</Button>
          <Button variant="contained" color="error" onClick={() => desactivando && desactivar.mutate()} disabled={desactivar.isPending}>
            Confirmar
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
