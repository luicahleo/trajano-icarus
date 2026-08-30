// Cola offline: solo datos de negocio. Nunca tokens ni credenciales (anti-PII).
export type TipoOperacionOffline = 'produccion.crear' | 'mortalidad.crear';
export type EstadoOperacion = 'pendiente' | 'error';

export interface OperacionPendiente {
  id: string; // uuid local
  tipo: TipoOperacionOffline;
  galponId: string;
  cuerpo: unknown; // DatosRecogida | DatosBajas (definidos en features/avicola)
  estado: EstadoOperacion;
  intentos: number;
  creadoEn: string; // ISO
  proximoIntentoEn: string | null; // ISO; null = listo para enviar
}
