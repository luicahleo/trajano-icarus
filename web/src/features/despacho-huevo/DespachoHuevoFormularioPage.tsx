import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Box,
  Button,
  IconButton,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import DeleteOutlineRoundedIcon from '@mui/icons-material/DeleteOutlineRounded';
import AddRoundedIcon from '@mui/icons-material/AddRounded';
import { useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import { PaginaCabecera } from '../../app/ui/PaginaCabecera';
import {
  crearDespacho,
  editarDespacho,
  obtenerDespacho,
  obtenerPrecioHuevoVigente,
  type LineaDespacho,
} from './api';
import {
  ETIQUETAS_TAMANO,
  formatoFecha,
  formatoMoneda,
  HUEVOS_POR_AMARRA,
  MAX_UNIDADES_SUELTAS,
} from './constantes';

const TAMANOS = Object.keys(ETIQUETAS_TAMANO);

interface LineaFormulario {
  tamano: string;
  amarras: string;
  sueltas: string;
}

// Alta y edición de borradores (spec SP9B): líneas por tamaño con amarras y
// unidades sueltas (menos de una amarra). El resumen en vivo estima el total
// con el precio vigente; un borrador se puede preparar sin precios, pero el
// despacho los exige.
export function DespachoHuevoFormularioPage() {
  const { id } = useParams<{ id: string }>();
  const esEdicion = Boolean(id);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [lineas, setLineas] = useState<LineaFormulario[]>([
    { tamano: 'Extra', amarras: '', sueltas: '' },
  ]);
  const [error, setError] = useState<string | null>(null);

  const { data: despacho, isLoading: cargandoDespacho } = useQuery({
    queryKey: ['despachos-huevo', 'detalle', id],
    queryFn: () => obtenerDespacho(id!),
    enabled: esEdicion,
  });

  const { data: precios, isError: errorPrecios } = useQuery({
    queryKey: ['despachos-huevo', 'precios-vigentes'],
    queryFn: obtenerPrecioHuevoVigente,
  });

  // Carga inicial del borrador a editar: una sola vez, cuando llega.
  const [precargado, setPrecargado] = useState(false);
  if (esEdicion && despacho && !precargado) {
    setLineas(
      despacho.detalles.map((d) => ({
        tamano: d.tamano,
        amarras: String(d.cantidadAmarras),
        sueltas: String(d.unidadesSueltas),
      })),
    );
    setPrecargado(true);
  }

  const precioDe = useMemo(() => {
    const indice = new Map((precios?.detalles ?? []).map((d) => [d.tamano, d.precioUnitario]));
    return (tamano: string): number | null => indice.get(tamano) ?? null;
  }, [precios]);

  const guardar = useMutation({
    mutationFn: async (lineasApi: LineaDespacho[]) => {
      if (esEdicion) {
        await editarDespacho(id!, { lineas: lineasApi });
        return { id: id! };
      }
      return crearDespacho({ lineas: lineasApi });
    },
    onSuccess: (resultado) => {
      queryClient.invalidateQueries({ queryKey: ['despachos-huevo'] });
      navigate(`/despachos/${resultado.id}`);
    },
    onError: (e) =>
      setError(e instanceof Error ? e.message : 'No se pudo guardar el despacho.'),
  });

  const enviar = () => {
    setError(null);
    if (lineas.length === 0) {
      setError('El despacho debe tener al menos una línea.');
      return;
    }
    if (
      lineas.some((l) => {
        const amarras = l.amarras === '' ? '0' : l.amarras;
        const sueltas = l.sueltas === '' ? '0' : l.sueltas;
        return (
          !Number.isInteger(Number(amarras)) ||
          !Number.isInteger(Number(sueltas)) ||
          Number(amarras) < 0 ||
          Number(sueltas) < 0
        );
      })
    ) {
      setError('Las cantidades deben ser números enteros mayores o iguales que cero.');
      return;
    }
    if (lineas.some((l) => Number(l.sueltas === '' ? '0' : l.sueltas) > MAX_UNIDADES_SUELTAS)) {
      setError(`Las unidades sueltas deben estar entre 0 y ${MAX_UNIDADES_SUELTAS}.`);
      return;
    }
    if (lineas.every((l) => Number(l.amarras === '' ? '0' : l.amarras) === 0 && Number(l.sueltas === '' ? '0' : l.sueltas) === 0)) {
      setError('Toda línea necesita amarras o unidades sueltas mayores que cero.');
      return;
    }
    const tamanos = lineas.map((l) => l.tamano);
    if (new Set(tamanos).size !== tamanos.length) {
      setError('Cada tamaño de huevo solo puede aparecer una vez.');
      return;
    }
    guardar.mutate(
      lineas.map((l) => ({
        tamano: l.tamano,
        cantidadAmarras: Number(l.amarras === '' ? '0' : l.amarras),
        unidadesSueltas: Number(l.sueltas === '' ? '0' : l.sueltas),
      })),
    );
  };

  const totalAmarras = lineas.reduce((a, l) => a + Number(l.amarras === '' ? '0' : l.amarras), 0);
  const totalHuevos = lineas.reduce((a, l) => {
    const amarras = Number(l.amarras === '' ? '0' : l.amarras);
    const sueltas = Number(l.sueltas === '' ? '0' : l.sueltas);
    return a + amarras * HUEVOS_POR_AMARRA + sueltas;
  }, 0);
  // El estimado solo tiene sentido cuando el precio vigente cubre todos los
  // tamaños declarados; si falta alguno, el despacho fallaría al congelar.
  const cubreTodos = lineas.every((l) => precioDe(l.tamano) !== null);
  const totalBs = cubreTodos
    ? lineas.reduce((a, l) => {
        const amarras = Number(l.amarras === '' ? '0' : l.amarras);
        const sueltas = Number(l.sueltas === '' ? '0' : l.sueltas);
        return a + (amarras * HUEVOS_POR_AMARRA + sueltas) * precioDe(l.tamano)!;
      }, 0)
    : null;

  const titulo = esEdicion ? 'Editar despacho' : 'Nuevo despacho';

  return (
    <Box sx={{ py: 3, px: { xs: 2, md: 4 } }}>
      <PaginaCabecera titulo={titulo} />

      <EstadoCarga cargando={esEdicion && cargandoDespacho} error={false}>
        <Stack spacing={2}>
          {precios && (
            <Typography variant="body2" color="text.secondary">
              Publicación de precios de huevo vigente desde el{' '}
              {formatoFecha(precios.fechaVigencia)} (notificada el{' '}
              {formatoFecha(precios.fechaNotificacion)}).
            </Typography>
          )}
          {esEdicion && despacho && despacho.estado !== 'Borrador' && (
            <Alert severity="warning">Solo un borrador se puede editar.</Alert>
          )}
          {errorPrecios && (
            <Alert severity="info">
              No hay publicación de precios de huevo vigente: podés preparar el borrador, pero
              el despacho exige precios vigentes.
            </Alert>
          )}
          {error && <Alert severity="error">{error}</Alert>}

          {lineas.map((linea, indice) => (
            <Paper key={indice} variant="outlined" sx={{ p: 2 }}>
              <Stack
                direction={{ xs: 'column', sm: 'row' }}
                spacing={2}
                sx={{ alignItems: 'flex-start' }}
              >
                <TextField
                  select
                  label="Tamaño de huevo"
                  value={linea.tamano}
                  onChange={(e) =>
                    setLineas((actuales) =>
                      actuales.map((l, i) => (i === indice ? { ...l, tamano: e.target.value } : l)),
                    )
                  }
                  sx={{ minWidth: 200 }}
                >
                  {TAMANOS.map((tamano) => (
                    <MenuItem key={tamano} value={tamano}>
                      {ETIQUETAS_TAMANO[tamano]}
                    </MenuItem>
                  ))}
                </TextField>
                <TextField
                  label="Amarras"
                  type="number"
                  value={linea.amarras}
                  onChange={(e) =>
                    setLineas((actuales) =>
                      actuales.map((l, i) => (i === indice ? { ...l, amarras: e.target.value } : l)),
                    )
                  }
                  slotProps={{ htmlInput: { min: 0, step: 1 } }}
                  sx={{ maxWidth: 160 }}
                />
                <TextField
                  label="Unidades sueltas"
                  type="number"
                  value={linea.sueltas}
                  onChange={(e) =>
                    setLineas((actuales) =>
                      actuales.map((l, i) => (i === indice ? { ...l, sueltas: e.target.value } : l)),
                    )
                  }
                  slotProps={{ htmlInput: { min: 0, max: MAX_UNIDADES_SUELTAS, step: 1 } }}
                  helperText={`Menos de una amarra (máx. ${MAX_UNIDADES_SUELTAS}).`}
                  sx={{ maxWidth: 180 }}
                />
                <Typography variant="body2" sx={{ pt: 3 }}>
                  {precioDe(linea.tamano) === null
                    ? 'Sin precio vigente'
                    : `Precio por huevo: ${formatoMoneda(precioDe(linea.tamano)!)}`}
                </Typography>
                <IconButton
                  aria-label="Quitar línea"
                  disabled={lineas.length === 1}
                  onClick={() => setLineas((actuales) => actuales.filter((_, i) => i !== indice))}
                >
                  <DeleteOutlineRoundedIcon />
                </IconButton>
              </Stack>
            </Paper>
          ))}

          <Box>
            <Button
              startIcon={<AddRoundedIcon />}
              onClick={() =>
                setLineas((actuales) => [
                  ...actuales,
                  { tamano: 'Primera', amarras: '', sueltas: '' },
                ])
              }
            >
              Agregar tamaño
            </Button>
          </Box>

          <Typography variant="subtitle1">Total amarras: {totalAmarras}</Typography>
          <Typography variant="subtitle1">Total huevos: {totalHuevos}</Typography>
          <Typography variant="subtitle1">
            Total estimado: {totalBs === null ? 'sin precios vigentes para todos los tamaños' : formatoMoneda(totalBs)}
          </Typography>

          <Stack direction="row" spacing={1}>
            <Button variant="contained" onClick={enviar} disabled={guardar.isPending}>
              {esEdicion ? 'Guardar cambios' : 'Crear borrador'}
            </Button>
            <Button onClick={() => navigate(-1)}>Cancelar</Button>
          </Stack>
        </Stack>
      </EstadoCarga>
    </Box>
  );
}
