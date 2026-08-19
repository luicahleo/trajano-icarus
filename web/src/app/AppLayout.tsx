import { AppBar, Box, Button, Drawer, IconButton, List, ListItemButton, ListItemText, Toolbar, Typography, useMediaQuery } from '@mui/material';
import MenuRoundedIcon from '@mui/icons-material/MenuRounded';
import LogoutRoundedIcon from '@mui/icons-material/LogoutRounded';
import { Suspense } from 'react';
import { Link as RouterLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../features/auth/AuthContext';
import { useState } from 'react';
import type { Rol } from '../lib/tipos';
import { CargandoRuta } from './CargandoRuta';
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
  const { rol, cerrarSesion, tieneFuncionalidad } = useAuth();
  const navigate = useNavigate();
  const esMovil = useMediaQuery('(max-width:600px)');
  const [menuAbierto, setMenuAbierto] = useState(false);
  const enlaces: EnlaceMenu[] = [...(rol ? (ENLACES_POR_ROL[rol] ?? []) : []), ...(rol === 'Cliente' || (rol === 'Trabajador' && tieneFuncionalidad('ProduccionHuevos', 'Mortalidad')) ? [{ etiqueta: 'Gestión Avícola', ruta: '/avicola' }] : [])];

  const salir = () => {
    cerrarSesion();
    navigate('/login');
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100dvh' }}>
      <AppBar position="sticky" color="primary">
        <Toolbar sx={{ gap: 1 }}>
          {esMovil && <IconButton color="inherit" aria-label="Abrir menú" onClick={() => setMenuAbierto(true)}><MenuRoundedIcon /></IconButton>}
          <Typography
            variant="h6"
            component={RouterLink}
            to="/"
            sx={{ flexGrow: 1, color: 'inherit', textDecoration: 'none' }}
          >
            Icarus
          </Typography>
          {!esMovil && enlaces.map((enlace) => (
            <Button key={enlace.ruta} component={RouterLink} to={enlace.ruta} color="inherit">
              {enlace.etiqueta}
            </Button>
          ))}
          <Button color="inherit" startIcon={<LogoutRoundedIcon />} onClick={salir}>
            Cerrar sesión
          </Button>
        </Toolbar>
      </AppBar>
      <Drawer open={menuAbierto} onClose={() => setMenuAbierto(false)}><List sx={{ width: 240 }}>{enlaces.map((enlace) => <ListItemButton key={enlace.ruta} component={RouterLink} to={enlace.ruta} onClick={() => setMenuAbierto(false)}><ListItemText primary={enlace.etiqueta} /></ListItemButton>)}</List></Drawer>
      <BannerSinConexion />
      <Box component="main" sx={{ flexGrow: 1 }}>
        <Suspense fallback={<CargandoRuta />}>
          <Outlet />
        </Suspense>
      </Box>
    </Box>
  );
}
