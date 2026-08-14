import { Suspense } from 'react';
import { createBrowserRouter } from 'react-router-dom';
import { ProtectedRoute } from '../features/auth/ProtectedRoute';
import { RequiereRol } from '../features/auth/RequiereRol';
import type { Rol } from '../lib/tipos';
import { AppLayout } from './AppLayout';
import { CargandoRuta } from './CargandoRuta';
import {
  ClienteDetallePage,
  ClienteNuevoPage,
  ClientesListaPage,
  InicioPage,
  LoginPage,
  NotFoundPage,
  TrabajadoresPage,
} from './paginasDiferidas';
import { RedirigirSegunRol } from './RedirigirSegunRol';

const admin: Rol[] = ['Administrador'];

export const router = createBrowserRouter([
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
        path: '*',
        element: <NotFoundPage />,
      },
    ],
  },
]);
