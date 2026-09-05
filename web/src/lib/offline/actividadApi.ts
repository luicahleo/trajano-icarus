// Señal de «el API respondió»: cualquier respuesta HTTP (aun un 401 o 500)
// prueba conectividad real con el backend, algo que ni navigator.onLine ni la
// sonda periódica del coordinador pueden ver entre disparos. El coordinador
// offline la usa para adelantar una sincronización pospuesta (diagnóstico
// SES-C54A1A220B07: el API volvió entre sondas y la cola esperó al timer
// ciego de 15 s; hizo falta recargar la página a mano).
const avisos = new Set<() => void>();

export function avisarActividadApi(): void {
  avisos.forEach((a) => a());
}

export function suscribirActividadApi(aviso: () => void): () => void {
  avisos.add(aviso);
  return () => avisos.delete(aviso);
}
