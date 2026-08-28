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
