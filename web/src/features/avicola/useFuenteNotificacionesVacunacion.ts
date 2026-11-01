import type { FuenteNotificaciones } from '../../app/notificaciones/fuentes';
import { useAuth } from '../auth/AuthContext';
import { useFuncionalidad } from '../auth/useFuncionalidad';
import { clasificarTarea } from './formatos';
import { textoResumenVacunacion } from './tareasPendientes';
import { useTareasPendientesVacunacion } from './useTareasPendientesVacunacion';

// Solo el Trabajador con Vacunacion. Una entrada-resumen sin estado «leído»:
// desaparece al completar las tareas.
export function useFuenteNotificacionesVacunacion(): FuenteNotificaciones {
  const { tieneRol } = useAuth();
  const esTrabajador = tieneRol('Trabajador');
  const puede = useFuncionalidad('Vacunacion');
  const { pendientes, error } = useTareasPendientesVacunacion(esTrabajador);
  const visible = esTrabajador && puede;
  const vencidas = pendientes.filter(
    (t) => clasificarTarea(t.fechaProgramada) === 'Vencida',
  ).length;
  return {
    id: 'vacunacion',
    seccion: 'Vacunación',
    visible,
    conteo: visible ? pendientes.length : 0,
    entradas:
      visible && pendientes.length > 0
        ? [
            {
              id: 'resumen',
              texto: textoResumenVacunacion(pendientes.length, vencidas),
              destino: '/avicola/tareas',
            },
          ]
        : [],
    error: visible && error,
  };
}
