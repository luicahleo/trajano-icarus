import { Chip, Container, Stack, TextField } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import { PaginaCabecera } from '../../app/ui/PaginaCabecera';
import { TablaDatos } from '../../app/ui/TablaDatos';
import type { Columna } from '../../app/ui/TablaDatos';
import type { EficienciaDia } from '../../lib/tipos';
import { obtenerEficiencia } from './api';
import { hoyIso } from './constantes';

function haceDias(n: number): string {
  const d = new Date();
  d.setDate(d.getDate() - n);
  return d.toISOString().slice(0, 10);
}

const COLUMNAS: Columna<EficienciaDia>[] = [
  { clave: 'fecha', encabezado: 'Fecha', render: (d) => d.fecha },
  { clave: 'huevos', encabezado: 'Huevos vendibles', render: (d) => d.totalVendible },
  { clave: 'eficiencia', encabezado: 'Eficiencia', render: (d) => `${d.eficiencia} %` },
  {
    clave: 'estado',
    encabezado: 'Estado',
    render: (d) =>
      d.bajoUmbral ? (
        <Chip size="small" color="error" label="Bajo umbral — considerar descarte" />
      ) : null,
  },
];

export function EficienciaPage() {
  const { galponId = '' } = useParams();
  const [desde, setDesde] = useState(haceDias(13));
  const [hasta, setHasta] = useState(hoyIso());
  const q = useQuery({
    queryKey: ['avicola', 'eficiencia', galponId, desde, hasta],
    queryFn: () => obtenerEficiencia(galponId, desde, hasta),
  });

  return (
    <Container maxWidth="lg" sx={{ py: 3 }}>
      <PaginaCabecera titulo="Eficiencia" />
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 3 }}>
        <TextField
          label="Desde"
          type="date"
          value={desde}
          onChange={(e) => setDesde(e.target.value)}
          slotProps={{ inputLabel: { shrink: true } }}
        />
        <TextField
          label="Hasta"
          type="date"
          value={hasta}
          onChange={(e) => setHasta(e.target.value)}
          slotProps={{ inputLabel: { shrink: true }, htmlInput: { max: hoyIso() } }}
        />
      </Stack>
      <EstadoCarga
        cargando={q.isLoading}
        error={q.isError}
        mensajeError="No se pudo cargar la eficiencia."
      >
        <TablaDatos
          columnas={COLUMNAS}
          filas={q.data?.dias ?? []}
          claveDeFila={(d) => d.fecha}
          mensajeVacio="Sin registros en el rango elegido."
        />
      </EstadoCarga>
    </Container>
  );
}
