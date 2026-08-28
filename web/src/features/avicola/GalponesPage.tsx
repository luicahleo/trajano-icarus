import {
  Box,
  Button,
  Container,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  TextField,
  Typography,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { EstadoCarga } from '../../app/ui/EstadoCarga';
import { PaginaCabecera } from '../../app/ui/PaginaCabecera';
import { useFuncionalidad } from '../auth/useFuncionalidad';
import { crearGalpon, listarGalpones, listarGranjas, renombrarGranja } from './api';
import { CLAVE_GRANJAS } from './constantes';
import { TarjetaGalpon } from './TarjetaGalpon';
import { VacunacionNotificacion } from './VacunacionNotificacion';

const CAMPOS_GALPON = [
  'numero',
  'capacidadMaxima',
  'gallinasActuales',
  'fechaNacimientoLote',
] as const;

function etiquetaCampo(campo: (typeof CAMPOS_GALPON)[number]): string {
  if (campo === 'numero') return 'Número';
  if (campo === 'capacidadMaxima') return 'Capacidad máxima';
  if (campo === 'gallinasActuales') return 'Gallinas actuales';
  return 'Fecha de poblado del lote';
}

export function GalponesPage() {
  const qc = useQueryClient();
  const gq = useQuery({ queryKey: CLAVE_GRANJAS, queryFn: listarGranjas });
  const granja = gq.data?.[0];
  const aq = useQuery({
    queryKey: ['avicola', 'galpones', granja?.id],
    queryFn: () => listarGalpones(granja!.id),
    enabled: !!granja,
  });
  const puedeG = useFuncionalidad('Galpones');
  const puedeR = useFuncionalidad('Granjas');
  const [alta, setAlta] = useState(false);
  const [ren, setRen] = useState(false);
  const [nombre, setNombre] = useState('');
  const [form, setForm] = useState({
    numero: '',
    capacidadMaxima: '',
    gallinasActuales: '',
    fechaNacimientoLote: '',
  });
  const crear = useMutation({
    mutationFn: () =>
      crearGalpon(granja!.id, {
        numero: form.numero,
        capacidadMaxima: Number(form.capacidadMaxima),
        gallinasActuales: Number(form.gallinasActuales),
        fechaNacimientoLote: form.fechaNacimientoLote,
        descripcion: null,
      }),
    onSuccess: () => {
      setAlta(false);
      void qc.invalidateQueries({ queryKey: ['avicola', 'galpones'] });
    },
  });
  const renombrar = useMutation({
    mutationFn: () => renombrarGranja(granja!.id, nombre),
    onSuccess: () => {
      setRen(false);
      void qc.invalidateQueries({ queryKey: CLAVE_GRANJAS });
    },
  });

  if (gq.isLoading) {
    return (
      <Container maxWidth="lg" sx={{ py: 3 }}>
        <EstadoCarga cargando error={false} />
      </Container>
    );
  }
  if (gq.isError || !granja) {
    return (
      <Container maxWidth="lg" sx={{ py: 3 }}>
        <EstadoCarga cargando={false} error mensajeError="No se pudo cargar la granja." />
      </Container>
    );
  }

  return (
    <Container maxWidth="lg" sx={{ py: 3 }}>
      <VacunacionNotificacion galpones={aq.data ?? []} />
      <PaginaCabecera
        titulo={granja.nombre}
        acciones={
          puedeR && (
            <Button
              variant="outlined"
              onClick={() => {
                setNombre(granja.nombre);
                setRen(true);
              }}
            >
              Renombrar
            </Button>
          )
        }
      />
      <EstadoCarga
        cargando={aq.isLoading}
        error={aq.isError}
        mensajeError="No se pudieron cargar los galpones."
      >
        {aq.data?.length ? (
          <>
            <Box
              sx={{
                display: 'grid',
                gap: 2,
                gridTemplateColumns: 'repeat(auto-fill,minmax(240px,1fr))',
              }}
            >
              {aq.data.map((x) => (
                <TarjetaGalpon key={x.id} galpon={x} />
              ))}
            </Box>
            {puedeG && (
              <Button variant="contained" sx={{ mt: 2 }} onClick={() => setAlta(true)}>
                Nuevo galpón
              </Button>
            )}
          </>
        ) : (
          <Box sx={{ py: 6, textAlign: 'center' }}>
            <Typography color="text.secondary">Todavía no hay galpones.</Typography>
            {puedeG && (
              <Button variant="contained" sx={{ mt: 2 }} onClick={() => setAlta(true)}>
                Crear el primero
              </Button>
            )}
          </Box>
        )}
      </EstadoCarga>
      <Dialog open={alta} onClose={() => setAlta(false)}>
        <DialogTitle>Nuevo galpón</DialogTitle>
        <DialogContent>
          {CAMPOS_GALPON.map((campo) => (
            <TextField
              key={campo}
              label={etiquetaCampo(campo)}
              type={campo === 'fechaNacimientoLote' ? 'date' : 'text'}
              value={form[campo]}
              onChange={(e) => setForm({ ...form, [campo]: e.target.value })}
              fullWidth
              margin="dense"
            />
          ))}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAlta(false)}>Cancelar</Button>
          <Button onClick={() => crear.mutate()} disabled={crear.isPending}>
            Guardar
          </Button>
        </DialogActions>
      </Dialog>
      <Dialog open={ren} onClose={() => setRen(false)}>
        <DialogTitle>Renombrar granja</DialogTitle>
        <DialogContent>
          <TextField
            label="Nombre de la granja"
            value={nombre}
            onChange={(e) => setNombre(e.target.value)}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRen(false)}>Cancelar</Button>
          <Button onClick={() => renombrar.mutate()}>Guardar</Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
}
