import type { ReactNode } from 'react';
import BusinessRoundedIcon from '@mui/icons-material/BusinessRounded';
import EggRoundedIcon from '@mui/icons-material/EggRounded';
import GroupsRoundedIcon from '@mui/icons-material/GroupsRounded';
import LocalShippingRoundedIcon from '@mui/icons-material/LocalShippingRounded';
import SendRoundedIcon from '@mui/icons-material/SendRounded';
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

const ENLACE_PEDIDOS: EnlaceNavegacion = {
  etiqueta: 'Pedidos de alimento',
  ruta: '/pedidos',
  icono: <LocalShippingRoundedIcon />,
};

const ENLACE_DESPACHOS: EnlaceNavegacion = {
  etiqueta: 'Despachos de huevo',
  ruta: '/despachos',
  icono: <SendRoundedIcon />,
};

export function obtenerEnlacesNavegacion(
  rol: Rol | null,
  tieneFuncionalidadAvicola: boolean,
  tienePedidoAlimento = false,
  tieneDespachoHuevo = false,
): EnlaceNavegacion[] {
  const propios = rol ? (ENLACES_POR_ROL[rol] ?? []) : [];
  const avicola =
    rol === 'Cliente' || (rol === 'Trabajador' && tieneFuncionalidadAvicola)
      ? [ENLACE_AVICOLA]
      : [];
  const pedidos =
    rol === 'Cliente' || (rol === 'Trabajador' && tienePedidoAlimento) ? [ENLACE_PEDIDOS] : [];
  const despachos =
    rol === 'Cliente' || (rol === 'Trabajador' && tieneDespachoHuevo) ? [ENLACE_DESPACHOS] : [];
  return [...propios, ...pedidos, ...despachos, ...avicola];
}

export function obtenerTituloRuta(ruta: string, enlaces: EnlaceNavegacion[]): string {
  const enlace = enlaces.find(({ ruta: base }) => ruta === base || ruta.startsWith(`${base}/`));
  return enlace?.etiqueta ?? 'Inicio';
}
