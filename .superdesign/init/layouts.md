# Layouts compartidos

## AppLayout

- Ruta: `web/src/app/AppLayout.tsx`
- Shell responsive con barra superior, navegación lateral, contenido y sesión.

```tsx
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

```
## NavegacionPrincipal

- Ruta: `web/src/app/NavegacionPrincipal.tsx`
- Menú lateral para escritorio y cajón móvil.

```tsx
import { List, ListItem, ListItemButton, ListItemIcon, ListItemText } from '@mui/material';
import { NavLink } from 'react-router-dom';
import type { EnlaceNavegacion } from './navegacion';

interface NavegacionPrincipalProps {
  enlaces: EnlaceNavegacion[];
  alNavegar?: () => void;
}

export function NavegacionPrincipal({ enlaces, alNavegar }: NavegacionPrincipalProps) {
  return (
    <List sx={{ px: 1.5, py: 2 }}>
      {enlaces.map((enlace) => (
        <ListItem key={enlace.ruta} disablePadding sx={{ mb: 0.5 }}>
          <ListItemButton
            component={NavLink}
            to={enlace.ruta}
            onClick={alNavegar}
            sx={{
              borderRadius: '12px',
              color: 'text.primary',
              gap: 1.5,
              px: 1.5,
              py: 1.25,
              '&.active': {
                backgroundColor: 'action.selected',
                color: 'primary.main',
                fontWeight: 700,
              },
              '&.active .MuiListItemIcon-root': { color: 'primary.main' },
              '&.active .MuiListItemText-primary': { fontWeight: 700 },
            }}
          >
            <ListItemIcon sx={{ minWidth: 0, color: 'text.secondary', '& svg': { fontSize: 22 } }}>
              {enlace.icono}
            </ListItemIcon>
            <ListItemText primary={enlace.etiqueta} />
          </ListItemButton>
        </ListItem>
      ))}
    </List>
  );
}

```

## SelectorTema

- Ruta: `web/src/app/SelectorTema.tsx`
- Conmutador de tema claro/oscuro.

```tsx
import { IconButton, useColorScheme } from '@mui/material';
import DarkModeRoundedIcon from '@mui/icons-material/DarkModeRounded';
import LightModeRoundedIcon from '@mui/icons-material/LightModeRounded';

export function SelectorTema() {
  const { mode, systemMode, setMode } = useColorScheme();
  const esOscuro = (mode === 'system' ? systemMode : mode) === 'dark';

  return (
    <IconButton
      color="inherit"
      aria-label={esOscuro ? 'Cambiar a modo claro' : 'Cambiar a modo oscuro'}
      onClick={() => setMode(esOscuro ? 'light' : 'dark')}
    >
      {esOscuro ? <LightModeRoundedIcon /> : <DarkModeRoundedIcon />}
    </IconButton>
  );
}

```

## BannerSinConexion

- Ruta: `web/src/app/BannerSinConexion.tsx`
- Aviso global de desconexión del shell Icarus.

```tsx
import { Alert } from '@mui/material';
import { useConexion } from './useConexion';
import { usePendientesOffline } from './offline/usePendientesOffline';

export function BannerSinConexion() {
  const online = useConexion();
  const pendientes = usePendientesOffline();
  if (online) return null;
  const conteo =
    pendientes === 0
      ? ''
      : pendientes === 1
        ? ' 1 registro pendiente de sincronizar.'
        : ` ${pendientes} registros pendientes de sincronizar.`;
  return (
    <Alert severity="warning" sx={{ borderRadius: 0 }}>
      Sin conexión: los registros se guardan en este dispositivo y se sincronizarán al volver la
      red.{conteo}
    </Alert>
  );
}

```
