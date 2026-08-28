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
