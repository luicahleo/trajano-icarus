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

type Fase =
  | 'idle'
  | 'confirmando'
  | 'capturando'
  | 'cuenta'
  | 'procesando'
  | 'resultado'
  | 'error'
  | 'incierto'
  | 'incidencia';
type Accion = 'Entrada' | 'Salida';

const DURACION_CUENTA = 3;
const LIMPIEZA_EXITO_MS = 6_000;
const LIMPIEZA_INCIDENCIA_MS = 4_000;

function horaBolivia(instanteUtc: string): string {
  return new Intl.DateTimeFormat('es-BO', {
    timeZone: 'America/La_Paz',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(instanteUtc));
}

function mensajeIntentos(intentos: number, maximo: number): string {
  const restantes = maximo - intentos;
  if (restantes === 1) return 'Último intento';
  return `Quedan ${restantes} intentos`;
}

export function MarcacionKioscoPage() {
  const [fase, setFase] = useState<Fase>('idle');
  const [accion, setAccion] = useState<Accion>('Entrada');
  const [resultado, setResultado] = useState<ResultadoMarcacionKiosco | null>(null);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [segundosCuenta, setSegundosCuenta] = useState<number | null>(null);
  const [segundosPropuesta, setSegundosPropuesta] = useState<number | null>(null);
  const [flujoId, setFlujoId] = useState<string | null>(null);
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
  const ocupado = useRef(false);
  const [bloqueado, setBloqueado] = useState(false);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const limpiarTemporizadores = useCallback(() => {
    if (timerRef.current) {
      clearInterval(timerRef.current);
      timerRef.current = null;
    }
    if (timeoutRef.current) {
      clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
  }, []);

  const volver = useCallback(() => {
    limpiarTemporizadores();
    detener();
    setResultado(null);
    setMensaje(null);
    setSegundosCuenta(null);
    setSegundosPropuesta(null);
    setFlujoId(null);
    setFase('idle');
    ocupado.current = false;
    setBloqueado(false);
  }, [detener, limpiarTemporizadores]);

  const procesar = useCallback(
    (r: ResultadoMarcacionKiosco) => {
      ocupado.current = false;
      setBloqueado(false);
      if (r.flujoId) setFlujoId(r.flujoId);

      if (r.estado === 'Registrada') {
        setResultado(r);
        setFase('resultado');
        detener();
        return;
      }
      if (r.estado === 'PropuestaSalida') {
        setResultado(r);
        setSegundosPropuesta(
          r.expiraPropuestaUtc
            ? Math.max(0, Math.round((new Date(r.expiraPropuestaUtc).getTime() - Date.now()) / 1000))
            : null,
        );
        setFase('resultado');
        detener();
        return;
      }
      if (r.estado === 'Incidencia') {
        setFase('incidencia');
        detener();
        timeoutRef.current = setTimeout(volver, LIMPIEZA_INCIDENCIA_MS);
        return;
      }
      if (r.estado === 'Rechazada') {
        setMensaje(mensajeIntentos(r.intentos ?? 1, r.maximoIntentos ?? DURACION_CUENTA));
        setFase('capturando');
        return;
      }
      setMensaje('No se pudo registrar la marcación. Consulta con el encargado.');
      setFase('error');
      detener();
    },
    [volver, detener],
  );

  const capturarYEnviar = useCallback(async () => {
    if (ocupado.current) return;
    ocupado.current = true;
    setBloqueado(true);
    limpiarTemporizadores();
    setFase('procesando');
    const base64 = capturar() ?? '';
    try {
      procesar(
        await marcarKiosco({
          accion,
          muestraBase64: base64,
          formato: 'image/jpeg',
          claveIdempotencia: clave.current,
          flujoId: flujoId ?? undefined,
        }),
      );
    } catch (fallo) {
      ocupado.current = false;
      setBloqueado(false);
      setMensaje(
        fallo instanceof ApiError
          ? (fallo.code ?? 'No se pudo confirmar la marcación.')
          : 'No se pudo confirmar la marcación.',
      );
      setFase('incierto');
    }
  }, [accion, capturar, flujoId, limpiarTemporizadores, procesar]);

  const iniciarCuenta = useCallback(() => {
    if (ocupado.current || (fase !== 'capturando' && fase !== 'incierto')) return;
    ocupado.current = true;
    setBloqueado(true);
    clave.current = nuevoId();
    setSegundosCuenta(DURACION_CUENTA);
    setFase('cuenta');
    timerRef.current = setInterval(() => {
      setSegundosCuenta((anterior) => {
        if (anterior === null || anterior <= 1) {
          if (timerRef.current) clearInterval(timerRef.current);
          timerRef.current = null;
          // Liberar el candado de la cuenta para que la captura real pueda ejecutarse.
          ocupado.current = false;
          setBloqueado(false);
          void capturarYEnviar();
          return null;
        }
        return anterior - 1;
      });
    }, 1_000);
  }, [capturarYEnviar, fase]);

  const confirmar = async () => {
    clave.current = nuevoId();
    setFlujoId(null);
    setFase('capturando');
    await iniciar();
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

  useEffect(() => {
    if (fase !== 'resultado' || !resultado) return;
    if (resultado.propuestaId && resultado.expiraPropuestaUtc) {
      const expira = resultado.expiraPropuestaUtc;
      const intervalo = setInterval(() => {
        const restantes = Math.max(
          0,
          Math.round((new Date(expira).getTime() - Date.now()) / 1000),
        );
        setSegundosPropuesta(restantes);
        if (restantes <= 0) volver();
      }, 1_000);
      return () => clearInterval(intervalo);
    }
    const temporizador = setTimeout(volver, LIMPIEZA_EXITO_MS);
    return () => clearTimeout(temporizador);
  }, [fase, resultado, volver]);

  useEffect(() => limpiarTemporizadores, [limpiarTemporizadores]);

  const puedeIniciarCaptura = fase === 'capturando' && activa && !bloqueado;

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
        <DialogContent>¿Confirmas que deseas registrar tu {accion.toLowerCase()}?</DialogContent>
        <DialogActions>
          <Button onClick={() => setFase('idle')}>Cancelar</Button>
          <Button variant="contained" onClick={() => void confirmar()}>
            Confirmar
          </Button>
        </DialogActions>
      </Dialog>

      {(fase === 'capturando' || fase === 'cuenta' || fase === 'procesando') && (
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
          {mensaje && <Alert severity="info">{mensaje}</Alert>}
          {fase === 'cuenta' && segundosCuenta !== null && (
            <Typography variant="h2" aria-live="polite">
              {segundosCuenta}
            </Typography>
          )}
          {fase === 'procesando' && <CircularProgress size={32} />}
          <Stack direction="row" spacing={2}>
            <Button onClick={volver}>Cancelar</Button>
            <Button
              variant="contained"
              disabled={!puedeIniciarCaptura}
              onClick={() => void iniciarCuenta()}
            >
              Iniciar captura
            </Button>
          </Stack>
          {!activa && !errorCamara && fase === 'capturando' && <CircularProgress size={24} />}
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
              {segundosPropuesta !== null && (
                <Typography variant="body2" color="text.secondary">
                  Caduca en {segundosPropuesta} s
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

      {fase === 'incidencia' && (
        <Stack spacing={2} sx={{ alignItems: 'center', maxWidth: 420 }}>
          <Alert severity="info">Incidencia registrada. El encargado revisará el caso.</Alert>
          <Button variant="contained" onClick={volver}>
            Entendido
          </Button>
        </Stack>
      )}

      {(fase === 'error' || fase === 'incierto') && (
        <Stack spacing={2} sx={{ alignItems: 'center', maxWidth: 420 }}>
          <Alert severity="error">{mensaje}</Alert>
          {fase === 'incierto' && (
            <Typography variant="body2" color="text.secondary">
              Comprobando registro
            </Typography>
          )}
          <Stack direction="row" spacing={2}>
            <Button onClick={volver}>Volver</Button>
            {fase === 'incierto' && (
              <Button variant="contained" onClick={() => void iniciarCuenta()}>
                Iniciar captura
              </Button>
            )}
          </Stack>
        </Stack>
      )}

      <Box sx={{ height: 8 }} />
    </Stack>
  );
}
