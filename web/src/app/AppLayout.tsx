import { AppBar, Box, Button, Toolbar, Typography } from '@mui/material';
import LogoutRoundedIcon from '@mui/icons-material/LogoutRounded';
import { Suspense } from 'react';
import { Link as RouterLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../features/auth/AuthContext';
import type { Rol } from '../lib/tipos';
import { CargandoRuta } from './CargandoRuta';
import { BannerSinConexion } from './BannerSinConexion';
import { BannerSinConexion } from './BannerSinConexion';

interface EnlaceMenu {
  etiqueta: string;
  ruta: string;
}

const ENLACES_POR_ROL: Partial<Record<Rol, EnlaceMenu[]>> = {
  Administrador: [{ etiqueta: 'Clientes', ruta: '/admin/clientes' }],
  Cliente: [{ etiqueta: 'Trabajadores', ruta: '/trabajadores' }],
};

export function AppLayout() {
  const { rol, cerrarSesion } = useAuth();
  const navigate = useNavigate();
  const enlaces: EnlaceMenu[] = rol ? (ENLACES_POR_ROL[rol] ?? []) : [];

  const salir = () => {
    cerrarSesion();
    navigate('/login');
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100dvh' }}>
      <AppBar position="sticky" color="primary">
        <Toolbar sx={{ gap: 1 }}>
          <Typography
            variant="h6"
            component={RouterLink}
            to="/"
            sx={{ flexGrow: 1, color: 'inherit', textDecoration: 'none' }}
          >
            Icarus
          </Typography>
          {enlaces.map((enlace) => (
            <Button key={enlace.ruta} component={RouterLink} to={enlace.ruta} color="inherit">
              {enlace.etiqueta}
            </Button>
          ))}
          <Button color="inherit" startIcon={<LogoutRoundedIcon />} onClick={salir}>
            Cerrar sesión
          </Button>
        </Toolbar>
      </AppBar>
      <BannerSinConexion />
      <BannerSinConexion />
      <Box component="main" sx={{ flexGrow: 1 }}>
        <Suspense fallback={<CargandoRuta />}>
          <Outlet />
        </Suspense>
      </Box>
    </Box>
  );
}
