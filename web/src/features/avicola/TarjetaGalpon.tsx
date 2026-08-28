import { Box, Card, CardActionArea, CardContent, Chip, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import type { Galpon } from '../../lib/tipos';
import { useFuncionalidad } from '../auth/useFuncionalidad';
import { obtenerEficiencia } from './api';
import { hoyIso } from './constantes';

export function TarjetaGalpon({ galpon }: { galpon: Galpon }) {
  const hoy = hoyIso();
  const puedeProduccion = useFuncionalidad('ProduccionHuevos');
  const eficiencia = useQuery({
    queryKey: ['avicola', 'eficiencia', galpon.id, hoy, hoy],
    queryFn: () => obtenerEficiencia(galpon.id, hoy, hoy),
    enabled: puedeProduccion,
  });
  const dia = eficiencia.data?.dias?.[0];

  return (
    <Card>
      <CardActionArea
        component={Link}
        to={`/avicola/galpones/${galpon.id}`}
        aria-label={`Abrir galpón ${galpon.numero}`}
      >
        <CardContent>
          <Typography variant="h6">Galpón {galpon.numero}</Typography>
          <Typography>
            {galpon.gallinasActuales} / {galpon.capacidadMaxima} gallinas
          </Typography>
          {dia && (
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
              <Typography component="span" sx={{ whiteSpace: 'nowrap' }}>
                {dia.eficiencia} %
              </Typography>
              {dia.bajoUmbral && (
                <Chip
                  size="small"
                  color="error"
                  label="Bajo umbral — considerar descarte"
                  sx={{ maxWidth: '100%' }}
                />
              )}
            </Box>
          )}
        </CardContent>
      </CardActionArea>
    </Card>
  );
}
