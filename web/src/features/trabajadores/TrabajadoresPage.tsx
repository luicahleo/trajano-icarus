import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Container,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Stack,
  TextField,
} from '@mui/material';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { CampoContrasena } from '../../app/ui/CampoContrasena';
import { DialogoConfirmacion } from '../../app/ui/DialogoConfirmacion';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import { PaginaCabecera } from '../../app/ui/PaginaCabecera';
import { TablaDatos } from '../../app/ui/TablaDatos';
import type { Columna } from '../../app/ui/TablaDatos';
import { ApiError } from '../../lib/http';
import type {
  Funcionalidad,
  FuncionalidadOperativaTrabajador,
  TrabajadorResumen,
} from '../../lib/tipos';
import { useAuth } from '../auth/AuthContext';
import { hoyIso } from '../avicola/constantes';
import {
  cesarTrabajador,
  crearTrabajador,
  definirFuncionalidades,
  desactivarTrabajador,
  listarTrabajadores,
} from './api';

const esquemaAlta = z
  .object({
    nombre: z.string().min(1, 'El nombre es obligatorio.'),
    documentoIdentidad: z.string().min(1, 'El documento de identidad es obligatorio.'),
    cargo: z.string().min(1, 'El cargo es obligatorio.'),
    fechaIngreso: z.string().min(1, 'La fecha de ingreso es obligatoria.'),
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

type DatosAlta = z.infer<typeof esquemaAlta>;
const camposAlta = new Set<keyof DatosAlta>([
  'nombre',
  'documentoIdentidad',
  'cargo',
  'fechaIngreso',
  'email',
  'contrasena',
  'confirmacionContrasena',
]);

const FUNCIONALIDADES_OPERATIVAS: FuncionalidadOperativaTrabajador[] = [
  'ProduccionHuevos',
  'Mortalidad',
  'Vacunacion',
  'PedidoAlimento',
];

function esOperativa(f: Funcionalidad): f is FuncionalidadOperativaTrabajador {
  return (
    f === 'ProduccionHuevos' ||
    f === 'Mortalidad' ||
    f === 'Vacunacion' ||
    f === 'PedidoAlimento'
  );
}

function etiquetaFuncionalidad(funcionalidad: FuncionalidadOperativaTrabajador): string {
  if (funcionalidad === 'ProduccionHuevos') return 'Producción de huevos';
  if (funcionalidad === 'Mortalidad') return 'Mortalidad';
  if (funcionalidad === 'PedidoAlimento') return 'Pedidos de alimento';
  return 'Vacunación';
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
  const [configurando, setConfigurando] = useState<TrabajadorResumen | null>(null);
  const [funcionalidades, setFuncionalidades] = useState<FuncionalidadOperativaTrabajador[]>([]);

  const claveTrabajadores = ['trabajadores', clienteId] as const;

  const {
    data: trabajadores,
    isLoading,
    isError,
  } = useQuery({
    queryKey: claveTrabajadores,
    queryFn: () => listarTrabajadores(clienteId!),
    enabled: Boolean(clienteId),
  });

  const refrescar = () => queryClient.invalidateQueries({ queryKey: ['trabajadores', clienteId] });

  const {
    register,
    setError,
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
      setErrorAlta(
        error instanceof ApiError
          ? (error.code ?? 'No se pudo crear el trabajador.')
          : 'No se pudo crear el trabajador.',
      );
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

  const guardarFuncionalidades = useMutation({
    mutationFn: () => definirFuncionalidades(clienteId!, configurando!.id, funcionalidades),
    onSuccess: () => {
      refrescar();
      setConfigurando(null);
    },
  });

  const onEnviarCese = () => {
    const hoy = hoyIso();
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

  const columnas: Columna<TrabajadorResumen>[] = [
    { clave: 'nombre', encabezado: 'Nombre', render: (t) => t.nombre },
    { clave: 'documento', encabezado: 'Documento', render: (t) => t.documentoIdentidad },
    { clave: 'cargo', encabezado: 'Cargo', render: (t) => t.cargo },
    { clave: 'fechaIngreso', encabezado: 'Fecha de ingreso', render: (t) => t.fechaIngreso },
    { clave: 'fechaCese', encabezado: 'Fecha de cese', render: (t) => t.fechaCese ?? '—' },
    {
      clave: 'funcionalidades',
      encabezado: 'Funcionalidades',
      render: (t) =>
        t.funcionalidades.filter(esOperativa).map(etiquetaFuncionalidad)
          .join(', ') || 'Ninguna',
    },
    {
      clave: 'acciones',
      encabezado: 'Acciones',
      alinear: 'right',
      render: (t) => (
        <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end' }}>
          <Button size="small" variant="outlined" onClick={() => setCesando(t)}>
            Cesar
          </Button>
          <Button size="small" variant="outlined" color="error" onClick={() => setDesactivando(t)}>
            Desactivar
          </Button>
          {!t.fechaCese && (
            <Button
              size="small"
              variant="outlined"
              onClick={() => {
                setConfigurando(t);
                setFuncionalidades(t.funcionalidades.filter(esOperativa));
              }}
            >
              Funcionalidades
            </Button>
          )}
        </Stack>
      ),
    },
  ];

  return (
    <Container maxWidth="lg" sx={{ py: 3 }}>
      <PaginaCabecera
        titulo="Trabajadores"
        acciones={
          clienteId && (
            <Button variant="contained" onClick={() => setAbiertaAlta(true)}>
              Nuevo trabajador
            </Button>
          )
        }
      />

      <EstadoCarga
        cargando={isLoading}
        error={isError}
        mensajeError="No se pudo cargar la lista de trabajadores."
      >
        {trabajadores && (
          <TablaDatos
            columnas={columnas}
            filas={trabajadores}
            claveDeFila={(t) => t.id}
            mensajeVacio="No hay trabajadores todavía."
          />
        )}
      </EstadoCarga>

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
            {errorAlta && <Alert severity="error">{errorAlta}</Alert>}
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAbiertaAlta(false)}>Cancelar</Button>
          <Button
            type="submit"
            form="form-alta-trabajador"
            variant="contained"
            disabled={crear.isPending}
          >
            Guardar
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={configurando !== null} onClose={() => setConfigurando(null)}>
        <DialogTitle>Funcionalidades de {configurando?.nombre}</DialogTitle>
        <DialogContent>
          {FUNCIONALIDADES_OPERATIVAS.map((funcionalidad) => (
            <FormControlLabel
              key={funcionalidad}
              control={
                <Checkbox
                  checked={funcionalidades.includes(funcionalidad)}
                  onChange={(e) =>
                    setFuncionalidades((actuales) =>
                      e.target.checked
                        ? [...actuales.filter((f) => f !== funcionalidad), funcionalidad]
                        : actuales.filter((f) => f !== funcionalidad),
                    )
                  }
                />
              }
              label={etiquetaFuncionalidad(funcionalidad)}
            />
          ))}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfigurando(null)}>Cancelar</Button>
          <Button
            variant="contained"
            onClick={() => guardarFuncionalidades.mutate()}
            disabled={guardarFuncionalidades.isPending}
          >
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

      <DialogoConfirmacion
        abierto={desactivando !== null}
        titulo="Confirmar acción"
        mensaje={`¿Desactivar a ${desactivando?.nombre}? Esta acción elimina su acceso.`}
        color="error"
        pendiente={desactivar.isPending}
        onCancelar={() => setDesactivando(null)}
        onConfirmar={() => desactivando && desactivar.mutate()}
      />
    </Container>
  );
}
