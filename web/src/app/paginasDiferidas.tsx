import { lazy } from 'react';

export const LoginPage = lazy(() =>
  import('../features/auth/LoginPage').then((modulo) => ({ default: modulo.LoginPage })),
);
export const InicioPage = lazy(() =>
  import('./InicioPage').then((modulo) => ({ default: modulo.InicioPage })),
);
export const NotFoundPage = lazy(() =>
  import('./NotFoundPage').then((modulo) => ({ default: modulo.NotFoundPage })),
);
export const ClientesListaPage = lazy(() =>
  import('../features/admin/clientes/ClientesListaPage').then((modulo) => ({ default: modulo.ClientesListaPage })),
);
export const ClienteNuevoPage = lazy(() =>
  import('../features/admin/clientes/ClienteNuevoPage').then((modulo) => ({ default: modulo.ClienteNuevoPage })),
);
export const ClienteDetallePage = lazy(() =>
  import('../features/admin/clientes/ClienteDetallePage').then((modulo) => ({ default: modulo.ClienteDetallePage })),
);
export const TrabajadoresPage = lazy(() =>
  import('../features/trabajadores/TrabajadoresPage').then((modulo) => ({ default: modulo.TrabajadoresPage })),
);
export const AvicolaInicioPage = lazy(() =>
  import('../features/avicola/AvicolaInicioPage').then((modulo) => ({ default: modulo.AvicolaInicioPage })),
);
export const GalponesPage = lazy(() => import('../features/avicola/GalponesPage').then((modulo) => ({ default: modulo.GalponesPage })));
export const GalponPage = lazy(() => import('../features/avicola/GalponPage').then((modulo) => ({ default: modulo.GalponPage })));
