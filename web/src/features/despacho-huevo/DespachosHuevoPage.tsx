import { useQuery } from '@tanstack/react-query';
import { Box, Button, Chip, Divider, Stack, Typography } from '@mui/material';
import AddRoundedIcon from '@mui/icons-material/AddRounded';
import { Link as RouterLink } from 'react-router-dom';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import { PaginaCabecera } from '../../app/ui/PaginaCabecera';
import { TablaDatos } from '../../app/ui/TablaDatos';
import type { Columna } from '../../app/ui/TablaDatos';
import { listarDespachos, type DespachoHuevoResumen } from './api';
import { COLOR_ESTADO, ETIQUETAS_ESTADO, formatoFecha, formatoMoneda } from './constantes';

// Bandeja compartida del tenant (spec SP9B): todos los usuarios del tenant con
// la funcionalidad ven los mismos despachos. Deliberadamente online.
export function DespachosHuevoPage() {
  const { data: despachos, isLoading, isError } = useQuery({
    queryKey: ['despachos-huevo'],
    queryFn: listarDespachos,
  });

  const columnas: Columna<DespachoHuevoResumen>[] = [
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
          <TablaDatos
            columnas={columnas}
            filas={despachos ?? []}
            claveDeFila={(d) => d.id}
            mensajeVacio="No hay despachos todavía. Creá el primero."
            etiqueta="Despachos de huevo"
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
