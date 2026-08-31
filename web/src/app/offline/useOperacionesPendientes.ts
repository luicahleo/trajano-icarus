import { useEffect, useState } from 'react';
import type { OperacionPendiente } from '../../lib/offline/tipos';
import { listarOperaciones, suscribirPendientes } from './coordinador';

// Operaciones en cola de un galpón, reactiva: se recarga al encolar, editar,
// descartar o sincronizar (cualquier cambio notificado por el coordinador).
export function useOperacionesPendientes(galponId: string): OperacionPendiente[] {
  const [ops, setOps] = useState<OperacionPendiente[]>([]);
  useEffect(() => {
    let activo = true;
    const cargar = () =>
      void listarOperaciones().then((todas) => {
        if (activo) setOps(todas.filter((o) => o.galponId === galponId));
      });
    cargar();
    const desuscribir = suscribirPendientes(cargar);
    return () => {
      activo = false;
      desuscribir();
    };
  }, [galponId]);
  return ops;
}
