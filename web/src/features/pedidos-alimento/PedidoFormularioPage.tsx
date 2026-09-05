import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Box,
  Button,
  Chip,
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
import { listarGalpones, listarGranjas } from '../avicola/api';
import {
  crearPedido,
  editarPedido,
  obtenerPedido,
  obtenerPrecioVigente,
  type LineaPedido,
} from './api';
import {
  ETIQUETAS_TIPO_ALIMENTO,
  formatoMoneda,
} from './constantes';

const TIPOS = Object.keys(ETIQUETAS_TIPO_ALIMENTO);

const DIAS_POR_GRANEL_MINIMO_TIPO = 2;
const TONELADAS_MINIMAS_TOTAL = 6;

interface LineaFormulario {
  tipoAlimento: string;
  cantidad: string;
}

// Alta y edición de borradores (spec SP8): un pedido admite una sola
// presentación, cantidades enteras y tipos únicos. La publicación vigente da
// el precio de referencia y las edades de los galpones sirven de
// recomendación informativa, sin obligar galpón ni cantidad.
export function PedidoFormularioPage() {
  const { id } = useParams<{ id: string }>();
  const esEdicion = Boolean(id);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [presentacion, setPresentacion] = useState<'Bolsa' | 'Granel'>('Bolsa');
  const [lineas, setLineas] = useState<LineaFormulario[]>([
    { tipoAlimento: 'PosturaUno', cantidad: '' },
  ]);
  const [error, setError] = useState<string | null>(null);

  const { data: pedido, isLoading: cargandoPedido } = useQuery({
    queryKey: ['pedidos-alimento', 'detalle', id],
    queryFn: () => obtenerPedido(id!),
    enabled: esEdicion,
  });

  const { data: precios, isError: errorPrecios } = useQuery({
    queryKey: ['pedidos-alimento', 'precios-vigentes'],
    queryFn: obtenerPrecioVigente,
  });

  const { data: granjas } = useQuery({
    queryKey: ['granjas'],
    queryFn: listarGranjas,
  });

  const granjaId = granjas?.[0]?.id;
  const { data: galpones } = useQuery({
    queryKey: ['galpones', granjaId],
    queryFn: () => listarGalpones(granjaId!),
    enabled: Boolean(granjaId),
  });

  // Carga inicial del borrador a editar: una sola vez, cuando llega.
  const [precargado, setPrecargado] = useState(false);
  if (esEdicion && pedido && !precargado) {
    setPresentacion(pedido.lineas[0].presentacion === 'Granel' ? 'Granel' : 'Bolsa');
    setLineas(
      pedido.lineas.map((l) => ({
        tipoAlimento: l.tipoAlimento,
        cantidad: String(l.cantidadSolicitada),
      })),
    );
    setPrecargado(true);
  }

  const precioDe = useMemo(() => {
    const indice = new Map(
      (precios?.detalles ?? []).map((d) => [`${d.tipoAlimento}|${d.presentacion}`, d]),
    );
    return (tipoAlimento: string) =>
      indice.get(`${tipoAlimento}|${presentacion}`)?.precioFinalPor40Kg ?? null;
  }, [precios, presentacion]);

  const recomendaciones = useMemo(() => {
    const hoy = new Date();
    return (galpones ?? []).map((g) => {
      const nacimiento = new Date(`${g.fechaNacimientoLote}T00:00:00`);
      const edad = Math.max(0, Math.floor((hoy.getTime() - nacimiento.getTime()) / 86_400_000));
      const sugeridos = (precios?.detalles ?? [])
        .filter(
          (d) =>
            d.presentacion === presentacion &&
            d.edadDesdeDias !== null &&
            d.edadHastaDias !== null &&
            edad >= d.edadDesdeDias &&
            edad <= d.edadHastaDias,
        )
        .map((d) => ETIQUETAS_TIPO_ALIMENTO[d.tipoAlimento] ?? d.tipoAlimento);
      return { galpon: g.numero, edad, sugeridos: [...new Set(sugeridos)] };
    });
  }, [galpones, precios, presentacion]);

  const guardar = useMutation({
    mutationFn: async (detalles: LineaPedido[]) => {
      if (esEdicion) {
        await editarPedido(id!, { detalles });
        return { id: id! };
      }
      return crearPedido({ detalles });
    },
    onSuccess: (resultado) => {
      queryClient.invalidateQueries({ queryKey: ['pedidos-alimento'] });
      navigate(`/pedidos/${resultado.id}`);
    },
    onError: (e) =>
      setError(e instanceof Error ? e.message : 'No se pudo guardar el pedido.'),
  });

  const enviar = () => {
    setError(null);
    if (lineas.length === 0) {
      setError('El pedido debe tener al menos una línea.');
      return;
    }
    const cantidades = lineas.map((l) => Number(l.cantidad));
    if (lineas.some((l) => l.cantidad === '' || !Number.isInteger(Number(l.cantidad)) || Number(l.cantidad) <= 0)) {
      setError('Las cantidades deben ser números enteros mayores que cero.');
      return;
    }
    const tipos = lineas.map((l) => l.tipoAlimento);
    if (new Set(tipos).size !== tipos.length) {
      setError('Cada tipo de alimento solo puede aparecer una vez.');
      return;
    }
    if (
      presentacion === 'Granel' &&
      (cantidades.some((c) => c < DIAS_POR_GRANEL_MINIMO_TIPO) ||
        cantidades.reduce((a, b) => a + b, 0) < TONELADAS_MINIMAS_TOTAL)
    ) {
      setError('El granel exige al menos 2 t por tipo y 6 t en total.');
      return;
    }
    guardar.mutate(
      lineas.map((l) => ({ tipoAlimento: l.tipoAlimento, presentacion, cantidad: Number(l.cantidad) })),
    );
  };

  const total = lineas.reduce((acumulado, linea) => {
    const precio = precioDe(linea.tipoAlimento);
    if (precio === null) return acumulado;
    const equivalente = presentacion === 'Bolsa' ? Number(linea.cantidad) : Number(linea.cantidad) * 25;
    return acumulado + precio * equivalente;
  }, 0);

  const titulo = esEdicion ? 'Editar pedido' : 'Nuevo pedido';

  return (
    <Box sx={{ py: 3, px: { xs: 2, md: 4 } }}>
      <PaginaCabecera titulo={titulo} />

      <EstadoCarga cargando={esEdicion && cargandoPedido} error={false}>
        <Stack spacing={2}>
          {esEdicion && pedido && pedido.estado !== 'Borrador' && (
            <Alert severity="warning">Solo un borrador se puede editar.</Alert>
          )}
          {errorPrecios && (
            <Alert severity="info">
              No hay publicación de precios vigente: podés preparar el borrador, pero
              el envío exige precios vigentes.
            </Alert>
          )}
          {error && <Alert severity="error">{error}</Alert>}

          <TextField
            select
            label="Presentación"
            value={presentacion}
            onChange={(e) => setPresentacion(e.target.value as 'Bolsa' | 'Granel')}
            disabled={esEdicion && Boolean(pedido)}
            helperText={
              presentacion === 'Bolsa'
                ? 'Cantidad en bolsas de 40 kg.'
                : 'Cantidad en toneladas enteras: mínimo 2 t por tipo y 6 t en total.'
            }
            sx={{ maxWidth: 320 }}
          >
            <MenuItem value="Bolsa">Bolsa (40 kg)</MenuItem>
            <MenuItem value="Granel">Granel (toneladas)</MenuItem>
          </TextField>

          {recomendaciones.length > 0 && (
            <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', rowGap: 1 }}>
              {recomendaciones.map((r) => (
                <Chip
                  key={r.galpon}
                  size="small"
                  variant="outlined"
                  label={
                    r.sugeridos.length > 0
                      ? `Galpón ${r.galpon} (${r.edad} días): ${r.sugeridos.join(', ')}`
                      : `Galpón ${r.galpon} (${r.edad} días)`
                  }
                />
              ))}
            </Stack>
          )}

          {lineas.map((linea, indice) => (
            <Paper key={indice} variant="outlined" sx={{ p: 2 }}>
              <Stack
                direction={{ xs: 'column', sm: 'row' }}
                spacing={2}
                sx={{ alignItems: 'flex-start' }}
              >
                <TextField
                  select
                  label="Tipo de alimento"
                  value={linea.tipoAlimento}
                  onChange={(e) =>
                    setLineas((actuales) =>
                      actuales.map((l, i) => (i === indice ? { ...l, tipoAlimento: e.target.value } : l)),
                    )
                  }
                  sx={{ minWidth: 220 }}
                >
                  {TIPOS.map((tipo) => (
                    <MenuItem key={tipo} value={tipo}>
                      {ETIQUETAS_TIPO_ALIMENTO[tipo]}
                    </MenuItem>
                  ))}
                </TextField>
                <TextField
                  label={presentacion === 'Bolsa' ? 'Bolsas' : 'Toneladas'}
                  type="number"
                  value={linea.cantidad}
                  onChange={(e) =>
                    setLineas((actuales) =>
                      actuales.map((l, i) => (i === indice ? { ...l, cantidad: e.target.value } : l)),
                    )
                  }
                  slotProps={{ htmlInput: { min: 1, step: 1 } }}
                  sx={{ maxWidth: 180 }}
                />
                <Typography variant="body2" sx={{ pt: 3 }}>
                  {precioDe(linea.tipoAlimento) === null
                    ? 'Sin precio vigente'
                    : `Precio por 40 kg: ${formatoMoneda(precioDe(linea.tipoAlimento)!)}`}
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
              onClick={() => setLineas((actuales) => [...actuales, { tipoAlimento: 'PosturaUno', cantidad: '' }])}
            >
              Agregar tipo
            </Button>
          </Box>

          <Typography variant="subtitle1">
            Total estimado: {formatoMoneda(total)}
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
