import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Box,
  Button,
  Chip,
  Divider,
  IconButton,
  List,
  ListItem,
  ListItemText,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import AddRoundedIcon from '@mui/icons-material/AddRounded';
import DoneAllRoundedIcon from '@mui/icons-material/DoneAllRounded';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import { useConexion } from '../../app/useConexion';
import { BarraFiltros } from '../../app/ui/BarraFiltros';
import type { DeclaracionFiltro, ValoresFiltros } from '../../app/ui/BarraFiltros';
import { ControlesPaginacion } from '../../app/ui/ControlesPaginacion';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import { PaginaCabecera } from '../../app/ui/PaginaCabecera';
import { TablaDatos } from '../../app/ui/TablaDatos';
import type { Columna } from '../../app/ui/TablaDatos';
import { TAMANO_PAGINA_POR_DEFECTO } from '../../lib/paginacion';
import { INTERVALO_SONDEO_MS } from '../../lib/sondeo';
import { useAuth } from '../auth/AuthContext';
import { listarTrabajadores } from '../trabajadores/api';
import {
  listarDespachos,
  listarGranjas,
  listarNotificacionesDespachoHuevo,
  marcarNotificacionDespachoHuevoLeida,
  type DespachoHuevoResumen,
} from './api';
import {
  COLOR_ESTADO,
  ETIQUETAS_ESTADO,
  formatoFecha,
  formatoMoneda,
  mensajeNotificacionDespachoHuevo,
} from './constantes';

// Bandeja compartida del tenant (spec SP9B): todos los usuarios del tenant con
// la funcionalidad ven los mismos despachos. Deliberadamente online. Desde
// 2026-09-14 filtra, pagina y muestra el folio y el autor resuelto en el
// cliente (el backend manda el id, nunca el nombre).
export function DespachosHuevoPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { clienteId } = useAuth();

  const [pagina, setPagina] = useState(1);
  const [filtros, setFiltros] = useState<ValoresFiltros>({});

  const {
    data: resultado,
    isLoading,
    isError,
  } = useQuery({
    queryKey: ['despachos-huevo', pagina, filtros],
    queryFn: () =>
      listarDespachos({ pagina, tamanoPagina: TAMANO_PAGINA_POR_DEFECTO }, filtros),
  });

  const { data: granjas } = useQuery({
    queryKey: ['despachos-huevo', 'granjas'],
    queryFn: listarGranjas,
  });

  // El nombre del autor vive en Clientes: GestionAvicola tiene prohibido
  // depender de ese módulo. El cruce se hace acá con la lista cacheada de
  // Trabajadores, no una consulta por fila.
  const { data: trabajadores } = useQuery({
    queryKey: ['despachos-huevo', 'trabajadores', clienteId],
    queryFn: () => listarTrabajadores(clienteId!),
    enabled: clienteId !== null,
  });

  // La bandeja de novedades ya se llenaba desde SP9C y ninguna pantalla la
  // mostraba (spec SP9F). El backend filtra por rol qué tipos devuelve.
  const hayConexion = useConexion();

  const { data: notificaciones } = useQuery({
    queryKey: ['despachos-huevo', 'notificaciones'],
    queryFn: listarNotificacionesDespachoHuevo,
    // Sin conexión no se sondea: la app es offline-first a propósito y un
    // reintento cada treinta segundos solo acumularía fallos.
    refetchInterval: hayConexion ? INTERVALO_SONDEO_MS : false,
    // Una pestaña oculta no necesita el badge al día.
    refetchIntervalInBackground: false,
  });

  // Huella de la bandeja de novedades. El contador solo no alcanza: marcar una
  // como leída mientras llega otra lo dejaría igual. No depende del orden en
  // que el servidor devuelva la lista.
  const huellaNovedades = notificaciones
    ? [
        notificaciones.contador,
        notificaciones.items.length,
        notificaciones.items.reduce((max, n) => (n.fechaUtc > max ? n.fechaUtc : max), ''),
      ].join(':')
    : null;
  const huellaPrevia = useRef<string | null>(null);

  useEffect(() => {
    if (huellaNovedades === null) return;
    if (huellaPrevia.current === null) {
      huellaPrevia.current = huellaNovedades; // primer render: nada que refrescar
      return;
    }
    if (huellaPrevia.current === huellaNovedades) return;
    huellaPrevia.current = huellaNovedades;
    // Solo la bandeja. Invalidar por el prefijo 'despachos-huevo' alcanzaría a
    // esta misma query de novedades y el refresco se realimentaría sin fin; la
    // clave de la bandeja es la única cuyo segundo elemento es la página.
    queryClient.invalidateQueries({
      predicate: (query) =>
        query.queryKey[0] === 'despachos-huevo' && typeof query.queryKey[1] === 'number',
    });
  }, [huellaNovedades, queryClient]);

  const marcarLeida = useMutation({
    mutationFn: marcarNotificacionDespachoHuevoLeida,
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['despachos-huevo', 'notificaciones'] }),
  });

  const sinLeer = (notificaciones?.items ?? []).filter((n) => !n.leida);

  const aplicarFiltros = (valores: ValoresFiltros) => {
    setFiltros(valores);
    setPagina(1);
  };

  const restablecerFiltros = () => {
    setFiltros({});
    setPagina(1);
  };

  const nombreAutor = (id: string | null) =>
    id
      ? (trabajadores?.find((t) => t.id === id)?.nombre ?? 'Autor no disponible')
      : 'Cliente';

  const declaraciones: DeclaracionFiltro[] = [
    {
      clave: 'granjaId',
      etiqueta: 'Granja',
      tipo: 'seleccion',
      opciones: (granjas ?? []).map((g) => ({ valor: g.id, etiqueta: g.nombre })),
    },
    {
      clave: 'estado',
      etiqueta: 'Estado',
      tipo: 'seleccion',
      opciones: Object.entries(ETIQUETAS_ESTADO).map(([valor, etiqueta]) => ({
        valor,
        etiqueta,
      })),
    },
    { clave: 'desde', etiqueta: 'Desde', tipo: 'fecha' },
    { clave: 'hasta', etiqueta: 'Hasta', tipo: 'fecha' },
    { clave: 'numero', etiqueta: 'Folio', tipo: 'texto' },
  ];
  if ((trabajadores?.length ?? 0) > 0) {
    declaraciones.push({
      clave: 'creadoPorTrabajadorId',
      etiqueta: 'Autor',
      tipo: 'seleccion',
      opciones: (trabajadores ?? []).map((t) => ({ valor: t.id, etiqueta: t.nombre })),
    });
  }

  const columnas: Columna<DespachoHuevoResumen>[] = [
    { clave: 'folio', encabezado: 'Folio', render: (d) => d.folio },
    {
      clave: 'estado',
      encabezado: 'Estado',
      render: (d) => (
        <Chip
          size="small"
          label={ETIQUETAS_ESTADO[d.estado] ?? d.estado}
          color={COLOR_ESTADO[d.estado] ?? 'default'}
        />
      ),
    },
    {
      clave: 'fechaDespacho',
      encabezado: 'Fecha de despacho',
      render: (d) => (d.fechaDespacho ? formatoFecha(d.fechaDespacho) : '—'),
    },
    { clave: 'amarras', encabezado: 'Amarras', alinear: 'right', render: (d) => d.totalAmarras },
    { clave: 'huevos', encabezado: 'Huevos', alinear: 'right', render: (d) => d.totalHuevos },
    { clave: 'autor', encabezado: 'Autor', render: (d) => nombreAutor(d.creadoPorTrabajadorId) },
    {
      clave: 'total',
      encabezado: 'Total',
      alinear: 'right',
      render: (d) => (d.totalBs === null ? '—' : formatoMoneda(d.totalBs)),
    },
    {
      clave: 'acciones',
      encabezado: 'Acciones',
      alinear: 'right',
      render: (d) => (
        <Button size="small" component={RouterLink} to={`/despachos/${d.id}`}>
          Ver
        </Button>
      ),
    },
  ];

  return (
    <Box sx={{ py: 3, px: { xs: 2, md: 4 } }}>
      <PaginaCabecera
        titulo="Despachos de huevo"
        acciones={
          <Button
            variant="contained"
            startIcon={<AddRoundedIcon />}
            component={RouterLink}
            to="/despachos/nuevo"
          >
            Nuevo despacho
          </Button>
        }
      />

      <EstadoCarga
        cargando={isLoading}
        error={isError}
        mensajeError="No se pudo cargar la lista de despachos."
      >
        <Stack spacing={2}>
          {sinLeer.length > 0 && (
            <Paper variant="outlined" sx={{ mb: 1 }}>
              <Box sx={{ px: 2, pt: 1.5 }}>
                <Typography variant="subtitle2">
                  Novedades del despacho de huevo ({notificaciones?.contador})
                </Typography>
              </Box>
              <List dense>
                {sinLeer.slice(0, 5).map((n) => (
                  <ListItem
                    key={n.id}
                    secondaryAction={
                      <IconButton
                        edge="end"
                        aria-label="Marcar como leída"
                        onClick={() => marcarLeida.mutate(n.id)}
                        size="small"
                      >
                        <DoneAllRoundedIcon fontSize="small" />
                      </IconButton>
                    }
                    onClick={
                      n.despachoHuevoId
                        ? () => navigate(`/despachos/${n.despachoHuevoId}`)
                        : undefined
                    }
                    sx={{ cursor: n.despachoHuevoId ? 'pointer' : 'default' }}
                  >
                    <ListItemText
                      primary={mensajeNotificacionDespachoHuevo(n.tipo)}
                      secondary={
                        n.meta
                          ? `${n.meta} · ${new Date(n.fechaUtc).toLocaleString('es-BO')}`
                          : new Date(n.fechaUtc).toLocaleString('es-BO')
                      }
                    />
                  </ListItem>
                ))}
              </List>
            </Paper>
          )}

          <BarraFiltros
            filtros={declaraciones}
            valores={filtros}
            onAplicar={aplicarFiltros}
            onRestablecer={restablecerFiltros}
          />

          <TablaDatos
            columnas={columnas}
            filas={resultado?.items ?? []}
            claveDeFila={(d) => d.id}
            mensajeVacio="No hay despachos todavía. Creá el primero."
            etiqueta="Despachos de huevo"
          />

          <ControlesPaginacion
            numeroPagina={resultado?.numeroPagina ?? 1}
            total={resultado?.total ?? 0}
            tamanoPagina={resultado?.tamanoPagina ?? TAMANO_PAGINA_POR_DEFECTO}
            onCambiarPagina={setPagina}
          />

          <Divider />
          <Typography variant="body2" color="text.secondary">
            El despacho es compartido del tenant: cualquiera con la función puede verlo,
            editarlo o despacharlo mientras esté en borrador. El despacho fija la fecha,
            congela los precios vigentes por tamaño y exige la foto de la nota de entrega.
          </Typography>
        </Stack>
      </EstadoCarga>
    </Box>
  );
}
