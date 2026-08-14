import { registerSW } from 'virtual:pwa-register';

export function instalarServiceWorker(): void {
  registerSW({ immediate: true });
}
