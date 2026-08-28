import { Container, List, ListItem, ListItemText, Paper, Stack, TextField } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import { PaginaCabecera } from '../../app/ui/PaginaCabecera';
import { obtenerEficiencia } from './api';
import { hoyIso } from './constantes';

function haceDias(n: number): string {
  const d = new Date();
  d.setDate(d.getDate() - n);
  return d.toISOString().slice(0, 10);
}

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
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
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
        {!q.data?.dias.length ? (
          <Paper sx={{ p: 4, textAlign: 'center', color: 'text.secondary' }}>
            Sin registros en el rango elegido.
          </Paper>
        ) : (
          <Paper>
            <List>
              {q.data.dias.map((d) => (
                <ListItem
                  key={d.fecha}
                  sx={{ display: 'flex', justifyContent: 'space-between', gap: 2 }}
                >
                  <ListItemText primary={d.fecha} secondary={`${d.totalVendible} huevos`} />
                  <ListItemText
                    primary={`${d.eficiencia} %`}
                    secondary={d.bajoUmbral ? 'Bajo umbral — considerar descarte' : undefined}
                    sx={{ textAlign: 'right', color: d.bajoUmbral ? 'error.main' : undefined }}
                  />
                </ListItem>
              ))}
            </List>
          </Paper>
        )}
      </EstadoCarga>
    </Container>
  );
}
