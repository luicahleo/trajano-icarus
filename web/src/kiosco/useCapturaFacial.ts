import { useCallback, useEffect, useRef, useState } from 'react';

// Captura facial del kiosco: mantiene la cámara solo mientras se captura y
// detiene las pistas al salir. Nunca persiste la imagen.
export function useCapturaFacial() {
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const [activa, setActiva] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const detener = useCallback(() => {
    streamRef.current?.getTracks().forEach((pista) => pista.stop());
    streamRef.current = null;
    setActiva(false);
  }, []);

  const iniciar = useCallback(async () => {
    if (!navigator.mediaDevices?.getUserMedia) {
      setError('No hay cámara disponible en este dispositivo.');
      return;
    }
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ video: true });
      streamRef.current = stream;
      if (videoRef.current) videoRef.current.srcObject = stream;
      setActiva(true);
      setError(null);
    } catch {
      setError('No se pudo acceder a la cámara.');
    }
  }, []);

  useEffect(() => detener, [detener]);

  const capturar = useCallback((): string | null => {
    const video = videoRef.current;
    const canvas = canvasRef.current;
    if (!video || !canvas) return null;
    const contexto = canvas.getContext('2d');
    if (!contexto || video.videoWidth === 0 || video.videoHeight === 0) return '';
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    contexto.drawImage(video, 0, 0);
    return canvas.toDataURL('image/jpeg').replace(/^data:[^,]+,/, '');
  }, []);

  return { videoRef, canvasRef, activa, error, iniciar, detener, capturar };
}
