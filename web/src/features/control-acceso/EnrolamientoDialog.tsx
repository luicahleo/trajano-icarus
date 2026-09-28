import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
} from '@mui/material';
import { useEffect, useRef, useState } from 'react';
import type { ResultadoEnrolamientoAcceso, TrabajadorResumen } from '../../lib/tipos';

interface EnrolamientoDialogProps {
  abierto: boolean;
  trabajador: TrabajadorResumen | null;
  pendiente: boolean;
  resultado: ResultadoEnrolamientoAcceso | null;
  error: string | null;
  onCancelar: () => void;
  onCapturar: (muestraBase64: string, formato: string) => void;
}

// Captura con cámara en directo, sin galería. La pista se detiene al cerrar
// (nunca queda retenida) y la foto no se persiste.
export function EnrolamientoDialog({
  abierto,
  trabajador,
  pendiente,
  resultado,
  error,
  onCancelar,
  onCapturar,
}: EnrolamientoDialogProps) {
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const [errorCamara, setErrorCamara] = useState<string | null>(null);

  useEffect(() => {
    if (!abierto) return;
    let cancelado = false;

    const iniciar = async () => {
      if (!navigator.mediaDevices?.getUserMedia) {
        setErrorCamara('No hay cámara disponible en este dispositivo.');
        return;
      }
      try {
        const stream = await navigator.mediaDevices.getUserMedia({ video: true });
        if (cancelado) {
          stream.getTracks().forEach((pista) => pista.stop());
          return;
        }
        streamRef.current = stream;
        if (videoRef.current) videoRef.current.srcObject = stream;
        setErrorCamara(null);
      } catch {
        setErrorCamara('No se pudo acceder a la cámara.');
      }
    };

    void iniciar();
    return () => {
      cancelado = true;
      streamRef.current?.getTracks().forEach((pista) => pista.stop());
      streamRef.current = null;
    };
  }, [abierto]);

  const capturar = () => {
    const video = videoRef.current;
    const canvas = canvasRef.current;
    if (!video || !canvas) return;
    const contexto = canvas.getContext('2d');
    let base64 = '';
    if (contexto && video.videoWidth > 0 && video.videoHeight > 0) {
      canvas.width = video.videoWidth;
      canvas.height = video.videoHeight;
      contexto.drawImage(video, 0, 0);
      base64 = canvas.toDataURL('image/jpeg').replace(/^data:[^,]+,/, '');
    }
    onCapturar(base64, 'image/jpeg');
  };

  return (
    <Dialog open={abierto} onClose={onCancelar} fullWidth maxWidth="sm">
      <DialogTitle>Enrolar rostro de {trabajador?.nombre}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2" color="text.secondary">
            Usa la cámara en directo. La captura solo se usa para cifrar la plantilla.
          </Typography>
          <video
            ref={videoRef}
            autoPlay
            playsInline
            muted
            style={{ width: '100%', borderRadius: 8, background: '#000', minHeight: 180 }}
          />
          <canvas ref={canvasRef} style={{ display: 'none' }} />
          {errorCamara && <Alert severity="warning">{errorCamara}</Alert>}
          {resultado?.exitoso && (
            <Alert severity="success">
              Enrolamiento confirmado (versión {resultado.versionEnrolamiento}).
            </Alert>
          )}
          {resultado && !resultado.exitoso && (
            <Alert severity="error">No se pudo enrolar el rostro.</Alert>
          )}
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancelar}>Cerrar</Button>
        <Button
          variant="contained"
          onClick={capturar}
          disabled={pendiente || Boolean(resultado?.exitoso)}
        >
          Capturar y enrolar
        </Button>
      </DialogActions>
    </Dialog>
  );
}
