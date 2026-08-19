import { Suspense } from 'react';
import { createBrowserRouter } from 'react-router-dom';
import { ProtectedRoute } from '../features/auth/ProtectedRoute';
import { RequiereRol } from '../features/auth/RequiereRol';
import { RequiereFuncionalidad } from '../features/auth/RequiereFuncionalidad';
import type { Rol } from '../lib/tipos';
import { AppLayout } from './AppLayout';
import { CargandoRuta } from './CargandoRuta';
import { ErrorDiagnosticoPage } from './ErrorDiagnosticoPage';
import {
  ClienteDetallePage,
  ClienteNuevoPage,
  ClientesListaPage,
  InicioPage,
  LoginPage,
  NotFoundPage,
  TrabajadoresPage,
  AvicolaInicioPage,
  GalponesPage,
  GalponPage,
  EficienciaPage,
} from './paginasDiferidas';
import { RedirigirSegunRol } from './RedirigirSegunRol';
import { RaizAplicacion } from './RaizAplicacion';

const admin: Rol[] = ['Administrador'];

export const router = createBrowserRouter([
  {
    element: <RaizAplicacion />,
    errorElement: <ErrorDiagnosticoPage />,
    children: [
      {
        path: '/login',
        element: (
          <Suspense fallback={<CargandoRuta />}>
            <LoginPage />
          </Suspense>
        ),
      },
      {
        element: <AppLayout />,
        children: [
          {
            path: '/',
            element: (
              <ProtectedRoute>
                <RedirigirSegunRol />
              </ProtectedRoute>
            ),
          },
          {
            path: '/inicio',
            element: (
              <ProtectedRoute>
                <InicioPage />
              </ProtectedRoute>
            ),
          },
          {
            path: '/admin/clientes',
            element: (
              <ProtectedRoute>
                <RequiereRol roles={admin}>
                  <ClientesListaPage />
                </RequiereRol>
              </ProtectedRoute>
            ),
          },
          {
            path: '/admin/clientes/nuevo',
            element: (
              <ProtectedRoute>
                <RequiereRol roles={admin}>
                  <ClienteNuevoPage />
                </RequiereRol>
              </ProtectedRoute>
            ),
          },
          {
            path: '/admin/clientes/:id',
            element: (
              <ProtectedRoute>
                <RequiereRol roles={admin}>
                  <ClienteDetallePage />
                </RequiereRol>
              </ProtectedRoute>
            ),
          },
          {
            path: '/trabajadores',
            element: (
              <ProtectedRoute>
                <RequiereRol roles={['Cliente']}>
                  <TrabajadoresPage />
                </RequiereRol>
              </ProtectedRoute>
            ),
          },
          {
            path: '/avicola',
            element: (
              <ProtectedRoute>
                  <RequiereFuncionalidad funcionalidades={['ProduccionHuevos', 'Mortalidad']}>
                  <Suspense fallback={<CargandoRuta />}>
                    <AvicolaInicioPage />
                  </Suspense>
                </RequiereFuncionalidad>
              </ProtectedRoute>
            ),
          },
          { path: '/avicola/galpones', element: <ProtectedRoute><RequiereFuncionalidad funcionalidades={['ProduccionHuevos', 'Mortalidad']}><Suspense fallback={<CargandoRuta />}><GalponesPage /></Suspense></RequiereFuncionalidad></ProtectedRoute> },
          { path: '/avicola/galpones/:galponId', element: <ProtectedRoute><RequiereFuncionalidad funcionalidades={['ProduccionHuevos', 'Mortalidad']}><Suspense fallback={<CargandoRuta />}><GalponPage /></Suspense></RequiereFuncionalidad></ProtectedRoute> },
          { path: '/avicola/galpones/:galponId/eficiencia', element: <ProtectedRoute><RequiereFuncionalidad funcionalidades={['ProduccionHuevos']}><Suspense fallback={<CargandoRuta />}><EficienciaPage /></Suspense></RequiereFuncionalidad></ProtectedRoute> },
          {
            path: '*',
            element: <NotFoundPage />,
          },
        ],
      },
    ],
  },
]);
