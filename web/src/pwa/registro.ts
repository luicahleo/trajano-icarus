import { registerSW } from 'virtual:pwa-register';

export function instalarServiceWorker(): void {
  registerSW({
    immediate: true,
    onRegisteredSW(_url, registro) {
      // El update check del navegador solo corre al cargar la página y puede
      // quedarse con un sw.js cacheado hasta 24 h. Estos chequeos hacen que
      // reabrir la app (o esperar un minuto con ella abierta) baste para
      // recoger el build recién desplegado; autoUpdate recarga al activar.
      if (!registro) return;
      const comprobarActualizacion = () => void registro.update();
      document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') comprobarActualizacion();
      });
      setInterval(comprobarActualizacion, 60_000);
    },
  });
}
