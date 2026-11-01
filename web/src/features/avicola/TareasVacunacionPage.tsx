import { Box, Button, Chip, Container, Paper, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import { PaginaCabecera } from '../../app/ui/PaginaCabecera';
import type { TareaVacunacionResumen } from '../../lib/tipos';
import { useAuth } from '../auth/AuthContext';
import { listarGalpones, listarGranjas } from './api';
import { CLAVE_GRANJAS } from './constantes';
import { CompletarTareaDialog } from './CompletarTareaDialog';
import { clasificarTarea } from './formatos';
import { agruparPorGalpon, textoTituloGrupo } from './tareasPendientes';
import { TextoRecortado } from './TextoRecortado';
import { useTareasPendientesVacunacion } from './useTareasPendientesVacunacion';

// Bandeja del trabajador: solo pendientes vencidas y de hoy, por galpón.
// Online: completar exige conexión, y CompletarTareaDialog ya lo controla.
export function TareasVacunacionPage() {
  const [completando, setCompletando] = useState<TareaVacunacionResumen | null>(null);
  const { cargando: cargandoSesion } = useAuth();
  const { pendientes, cargando: cargandoTareas, error, reintentar } =
    useTareasPendientesVacunacion();
  // Mientras la sesión carga, la consulta aún está deshabilitada y no debe
  // mostrarse el estado vacío.
  const cargando = cargandoSesion || cargandoTareas;
  const granjas = useQuery({ queryKey: CLAVE_GRANJAS, queryFn: listarGranjas });
  const granja = granjas.data?.[0];
  const galpones = useQuery({
    queryKey: ['avicola', 'galpones', granja?.id],
    queryFn: () => listarGalpones(granja!.id),
    enabled: Boolean(granja),
  });
  const grupos = agruparPorGalpon(pendientes, galpones.data ?? []);

  return (
    <Container maxWidth="sm" sx={{ py: 3 }}>
      <PaginaCabecera titulo="Tareas de vacunación" variante="h5" />
      <EstadoCarga
        cargando={cargando}
        error={error}
        mensajeError="No se pudieron cargar las tareas de vacunación."
        onReintentar={reintentar}
      >
        {grupos.length === 0 ? (
          <Typography color="text.secondary">No hay tareas de vacunación pendientes.</Typography>
        ) : (
          grupos.map((grupo) => {
            const idTitulo = `grupo-vacunacion-${grupo.galponId}`;
            return (
              <Box key={grupo.galponId} component="section" sx={{ mb: 3 }}>
                <Typography
                  id={idTitulo}
                  component="h2"
                  variant="subtitle1"
                  sx={{ fontWeight: 700, mb: 1 }}
                >
                  {textoTituloGrupo(grupo)}
                </Typography>
                <Box
                  component="ul"
                  aria-labelledby={idTitulo}
                  sx={{
                    listStyle: 'none',
                    p: 0,
                    m: 0,
                    display: 'flex',
                    flexDirection: 'column',
                    gap: 1.5,
                  }}
                >
                  {grupo.tareas.map((tarea) => {
                    const clasificacion = clasificarTarea(tarea.fechaProgramada);
                    const aplicacion = tarea.modoAplicacion ?? tarea.observacionesProgramadas;
                    return (
                      <Paper
                        key={tarea.id}
                        component="li"
                        variant="outlined"
                        sx={{ p: 2, display: 'flex', flexDirection: 'column', gap: 1 }}
                      >
                        <Chip
                          size="small"
                          label={clasificacion}
                          color={clasificacion === 'Vencida' ? 'error' : 'warning'}
                          sx={{ alignSelf: 'flex-start' }}
                        />
                        <TextoRecortado texto={tarea.vacuna} />
                        <Typography variant="body2" color="text.secondary">
                          {`Día ${tarea.edadDia} · Programada ${tarea.fechaProgramada}`}
                        </Typography>
                        {aplicacion && <TextoRecortado texto={aplicacion} variante="body2" />}
                        <Button
                          variant="contained"
                          onClick={() => setCompletando(tarea)}
                          sx={{ alignSelf: { xs: 'stretch', sm: 'flex-start' } }}
                        >
                          Completar
                        </Button>
                      </Paper>
                    );
                  })}
                </Box>
              </Box>
            );
          })
        )}
      </EstadoCarga>
      <CompletarTareaDialog
        key={completando?.id ?? 'ninguna'}
        tarea={completando}
        abierto={completando !== null}
        alCerrar={() => setCompletando(null)}
      />
    </Container>
  );
}
