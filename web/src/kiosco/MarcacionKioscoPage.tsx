import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
} from '@mui/material';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '../lib/http';
import { confirmarSalidaKiosco, marcarKiosco } from './apiKiosco';
import type { ResultadoMarcacionKiosco } from './apiKiosco';
import { nuevoId } from './identificadores';
import { useCapturaFacial } from './useCapturaFacial';

type Fase = 'idle' | 'confirmando' | 'capturando' | 'resultado' | 'error' | 'incierto';
type Accion = 'Entrada' | 'Salida';

function horaBolivia(instanteUtc: string): string {
  return new Intl.DateTimeFormat('es-BO', {
    timeZone: 'America/La_Paz',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(instanteUtc));
}

export function MarcacionKioscoPage() {
  const [fase, setFase] = useState<Fase>('idle');
  const [accion, setAccion] = useState<Accion>('Entrada');
  const [resultado, setResultado] = useState<ResultadoMarcacionKiosco | null>(null);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [segundos, setSegundos] = useState<number | null>(null);
  const {
    videoRef,
    canvasRef,
    activa,
    error: errorCamara,
    iniciar,
    detener,
    capturar,
  } = useCapturaFacial();
  const clave = useRef('');

  const volver = useCallback(() => {
    detener();
    setResultado(null);
    setMensaje(null);
    setSegundos(null);
    setFase('idle');
  }, [detener]);

  const procesar = useCallback((r: ResultadoMarcacionKiosco) => {
    if (r.estado === 'Registrada') {
      setResultado(r);
      setFase('resultado');
      return;
    }
    if (r.estado === 'PropuestaSalida') {
      setResultado(r);
      setSegundos(
        r.expiraPropuestaUtc
          ? Math.max(0, Math.round((new Date(r.expiraPropuestaUtc).getTime() - Date.now()) / 1000))
          : null,
      );
      setFase('resultado');
      return;
    }
    setMensaje('No se pudo registrar la marcación. Consulta con el encargado.');
    setFase('error');
  }, []);

  const confirmar = async () => {
    clave.current = nuevoId();
    setFase('capturando');
    await iniciar();
  };

  const capturarYEnviar = async () => {
    const base64 = capturar() ?? '';
    detener();
    try {
      procesar(
        await marcarKiosco({
          accion,
          muestraBase64: base64,
          formato: 'image/jpeg',
          claveIdempotencia: clave.current,
        }),
      );
    } catch (fallo) {
      // No se interpreta como fallo definitivo: el servidor pudo confirmar.
      setMensaje(
        fallo instanceof ApiError
          ? (fallo.code ?? 'No se pudo confirmar la marcación.')
          : 'No se pudo confirmar la marcación.',
      );
      setFase('incierto');
    }
  };

  const confirmarLaSalida = async () => {
    if (!resultado?.propuestaId) return;
    try {
      procesar(await confirmarSalidaKiosco(resultado.propuestaId));
    } catch {
      setMensaje('No se pudo confirmar la salida.');
      setFase('error');
    }
  };

  // El resultado se limpia solo; la propuesta respeta su caducidad.
  useEffect(() => {
    if (fase !== 'resultado' || !resultado) return;
    if (resultado.propuestaId && resultado.expiraPropuestaUtc) {
      const expira = resultado.expiraPropuestaUtc;
      const intervalo = setInterval(() => {
        const restantes = Math.max(
          0,
          Math.round((new Date(expira).getTime() - Date.now()) / 1000),
        );
        setSegundos(restantes);
        if (restantes <= 0) volver();
      }, 1000);
      return () => clearInterval(intervalo);
    }
    const temporizador = setTimeout(volver, 6000);
    return () => clearTimeout(temporizador);
  }, [fase, resultado, volver]);

  return (
    <Stack
      sx={{ alignItems: 'center', justifyContent: 'center', minHeight: '100dvh', p: 3, gap: 3 }}
    >
      {fase === 'idle' && (
        <>
          <Typography variant="h4" component="h1">
            ¿Qué deseas registrar?
          </Typography>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={3}>
            <Button
              variant="contained"
              size="large"
              sx={{ width: 220, height: 140, fontSize: 24 }}
              onClick={() => {
                setAccion('Entrada');
                setFase('confirmando');
              }}
            >
              Entrada
            </Button>
            <Button
              variant="outlined"
              size="large"
              sx={{ width: 220, height: 140, fontSize: 24 }}
              onClick={() => {
                setAccion('Salida');
                setFase('confirmando');
              }}
            >
              Salida
            </Button>
          </Stack>
        </>
      )}

      <Dialog open={fase === 'confirmando'} onClose={() => setFase('idle')}>
        <DialogTitle>Confirmar</DialogTitle>
        <DialogContent>
          ¿Confirmas que deseas registrar tu {accion.toLowerCase()}?
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setFase('idle')}>Cancelar</Button>
          <Button variant="contained" onClick={() => void confirmar()}>
            Confirmar
          </Button>
        </DialogActions>
      </Dialog>

      {fase === 'capturando' && (
        <Stack spacing={2} sx={{ width: '100%', maxWidth: 480, alignItems: 'center' }}>
          <Typography variant="h6">Mira a la cámara</Typography>
          <video
            ref={videoRef}
            autoPlay
            playsInline
            muted
            style={{ width: '100%', borderRadius: 12, background: '#000', minHeight: 240 }}
          />
          <canvas ref={canvasRef} style={{ display: 'none' }} />
          {errorCamara && <Alert severity="warning">{errorCamara}</Alert>}
          <Stack direction="row" spacing={2}>
            <Button onClick={volver}>Cancelar</Button>
            <Button variant="contained" disabled={!activa} onClick={() => void capturarYEnviar()}>
              Capturar
            </Button>
          </Stack>
          {!activa && !errorCamara && <CircularProgress size={24} />}
        </Stack>
      )}

      {fase === 'resultado' && resultado && (
        <Stack spacing={2} sx={{ alignItems: 'center' }}>
          {resultado.propuestaId ? (
            <>
              <Typography variant="h5">
                Ya tienes una entrada abierta, {resultado.nombreCompleto ?? ''}.
              </Typography>
              <Typography variant="body1">¿Registrar la salida ahora?</Typography>
              <Stack direction="row" spacing={2}>
                <Button onClick={volver}>No</Button>
                <Button variant="contained" onClick={() => void confirmarLaSalida()}>
                  Sí, registrar salida
                </Button>
              </Stack>
              {segundos !== null && (
                <Typography variant="body2" color="text.secondary">
                  Caduca en {segundos} s
                </Typography>
              )}
            </>
          ) : (
            <>
              <Typography variant="h4">{resultado.nombreCompleto}</Typography>
              <Typography variant="h5">{resultado.tipo}</Typography>
              {resultado.instanteUtc && (
                <Typography variant="h6">{horaBolivia(resultado.instanteUtc)}</Typography>
              )}
            </>
          )}
        </Stack>
      )}

      {(fase === 'error' || fase === 'incierto') && (
        <Stack spacing={2} sx={{ alignItems: 'center', maxWidth: 420 }}>
          <Alert severity="error">{mensaje}</Alert>
          <Button variant="contained" onClick={volver}>
            Volver
          </Button>
        </Stack>
      )}

      <Box sx={{ height: 8 }} />
    </Stack>
  );
}
