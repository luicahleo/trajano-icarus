import {
  AppBar,
  Box,
  Button,
  Divider,
  Drawer,
  IconButton,
  Toolbar,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import MenuRoundedIcon from '@mui/icons-material/MenuRounded';
import LogoutRoundedIcon from '@mui/icons-material/LogoutRounded';
import { Suspense, useState } from 'react';
import { Link as RouterLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../features/auth/AuthContext';
import { CargandoRuta } from './CargandoRuta';
import { BannerSinConexion } from './BannerSinConexion';
import { obtenerEnlacesNavegacion, obtenerTituloRuta } from './navegacion';
import { NavegacionPrincipal } from './NavegacionPrincipal';
import { PendientesOffline } from './offline/PendientesOffline';
import { PrecalentadoOffline } from './offline/PrecalentadoOffline';
import { SelectorTema } from './SelectorTema';

const ANCHO_NAVEGACION = 248;
const ANCHO_NAVEGACION_MOVIL = 288;

export function AppLayout() {
  const { rol, correo, cerrarSesion, tieneFuncionalidad } = useAuth();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const tema = useTheme();
  const esMovil = useMediaQuery(tema.breakpoints.down('md'));
  const [menuAbierto, setMenuAbierto] = useState(false);
  const enlaces = obtenerEnlacesNavegacion(
    rol,
    tieneFuncionalidad('ProduccionHuevos', 'Mortalidad', 'Vacunacion'),
  );
  const titulo = obtenerTituloRuta(pathname, enlaces);

  const salir = () => {
    cerrarSesion();
    navigate('/login');
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100dvh' }}>
      <Box
        component="a"
        href="#contenido-principal"
        sx={{
          position: 'absolute',
          left: 8,
          top: -64,
          zIndex: (t) => t.zIndex.tooltip,
          px: 2,
          py: 1,
          borderRadius: '12px',
          backgroundColor: 'background.paper',
          color: 'primary.main',
          fontWeight: 700,
          textDecoration: 'none',
          '&:focus': { top: 8 },
        }}
      >
        Saltar al contenido
      </Box>
      <AppBar
        position="sticky"
        elevation={0}
        sx={{ backgroundColor: 'marca.fondo', color: 'marca.texto', backgroundImage: 'none' }}
      >
        <Toolbar sx={{ gap: 1.5 }}>
          {esMovil && enlaces.length > 0 && (
            <IconButton
              color="inherit"
              edge="start"
              aria-label="Abrir menú"
              onClick={() => setMenuAbierto(true)}
            >
              <MenuRoundedIcon />
            </IconButton>
          )}
          <Typography
            variant="h6"
            component={RouterLink}
            to="/"
            sx={{ color: 'inherit', textDecoration: 'none', whiteSpace: 'nowrap' }}
          >
            Trajano Icarus
          </Typography>
          <Box
            aria-hidden
            sx={{
              display: { xs: 'none', sm: 'block' },
              opacity: 0.5,
              userSelect: 'none',
            }}
          >
            /
          </Box>
          <Typography
            component="h1"
            variant="subtitle1"
            sx={{
              flexGrow: 1,
              minWidth: 0,
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
              opacity: 0.9,
            }}
          >
            {titulo}
          </Typography>
          {correo && (
            <Typography
              variant="body2"
              title={correo}
              sx={{
                display: { xs: 'none', sm: 'block' },
                maxWidth: 240,
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                whiteSpace: 'nowrap',
                opacity: 0.85,
              }}
            >
              {correo}
            </Typography>
          )}
          <SelectorTema />
          <PendientesOffline />
          <Button color="inherit" startIcon={<LogoutRoundedIcon />} onClick={salir}>
            Cerrar sesión
          </Button>
        </Toolbar>
      </AppBar>
      <BannerSinConexion />
      <PrecalentadoOffline />
      <Box sx={{ display: 'flex', flexGrow: 1, minHeight: 0 }}>
        {enlaces.length > 0 && (
          <Box
            component="nav"
            aria-label="Navegación principal"
            sx={{ flexShrink: 0, width: { md: ANCHO_NAVEGACION } }}
          >
            {esMovil ? (
              <Drawer
                open={menuAbierto}
                onClose={() => setMenuAbierto(false)}
                sx={{
                  '& .MuiDrawer-paper': {
                    width: ANCHO_NAVEGACION_MOVIL,
                    backgroundColor: 'background.paper',
                  },
                }}
              >
                <Toolbar sx={{ px: 2 }}>
                  <Typography variant="h6" color="primary.main">
                    Trajano Icarus
                  </Typography>
                </Toolbar>
                <Divider />
                <NavegacionPrincipal enlaces={enlaces} alNavegar={() => setMenuAbierto(false)} />
              </Drawer>
            ) : (
              <Drawer
                variant="permanent"
                sx={{
                  '& .MuiDrawer-paper': {
                    position: 'static',
                    width: ANCHO_NAVEGACION,
                    borderRight: '1px solid',
                    borderColor: 'divider',
                    backgroundColor: 'background.paper',
                  },
                }}
              >
                <NavegacionPrincipal enlaces={enlaces} />
              </Drawer>
            )}
          </Box>
        )}
        <Box
          component="main"
          id="contenido-principal"
          sx={{ flexGrow: 1, minWidth: 0, backgroundColor: 'background.default' }}
        >
          <Suspense fallback={<CargandoRuta />}>
            <Outlet />
          </Suspense>
        </Box>
      </Box>
    </Box>
  );
}
