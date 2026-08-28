import type { ReactNode } from 'react';
import BusinessRoundedIcon from '@mui/icons-material/BusinessRounded';
import EggRoundedIcon from '@mui/icons-material/EggRounded';
import GroupsRoundedIcon from '@mui/icons-material/GroupsRounded';
import VaccinesRoundedIcon from '@mui/icons-material/VaccinesRounded';
import type { Rol } from '../lib/tipos';

export interface EnlaceNavegacion {
  etiqueta: string;
  ruta: string;
  icono: ReactNode;
}

const ENLACES_POR_ROL: Partial<Record<Rol, EnlaceNavegacion[]>> = {
  Administrador: [
    { etiqueta: 'Clientes', ruta: '/admin/clientes', icono: <BusinessRoundedIcon /> },
    { etiqueta: 'Vacunación', ruta: '/admin/vacunacion', icono: <VaccinesRoundedIcon /> },
  ],
  Cliente: [{ etiqueta: 'Trabajadores', ruta: '/trabajadores', icono: <GroupsRoundedIcon /> }],
};

const ENLACE_AVICOLA: EnlaceNavegacion = {
  etiqueta: 'Gestión Avícola',
  ruta: '/avicola',
  icono: <EggRoundedIcon />,
};

export function obtenerEnlacesNavegacion(
  rol: Rol | null,
  tieneFuncionalidadAvicola: boolean,
): EnlaceNavegacion[] {
  const propios = rol ? (ENLACES_POR_ROL[rol] ?? []) : [];
  const avicola =
    rol === 'Cliente' || (rol === 'Trabajador' && tieneFuncionalidadAvicola)
      ? [ENLACE_AVICOLA]
      : [];
  return [...propios, ...avicola];
}

export function obtenerTituloRuta(ruta: string, enlaces: EnlaceNavegacion[]): string {
  const enlace = enlaces.find(({ ruta: base }) => ruta === base || ruta.startsWith(`${base}/`));
  return enlace?.etiqueta ?? 'Inicio';
}
