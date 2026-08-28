import VisibilityOffRoundedIcon from '@mui/icons-material/VisibilityOffRounded';
import VisibilityRoundedIcon from '@mui/icons-material/VisibilityRounded';
import { IconButton, InputAdornment, TextField } from '@mui/material';
import type { TextFieldProps } from '@mui/material';
import { useState } from 'react';

type CampoContrasenaProps = Omit<TextFieldProps, 'type'>;

export function CampoContrasena({
  autoComplete = 'new-password',
  slotProps,
  ...rest
}: CampoContrasenaProps) {
  const [visible, setVisible] = useState(false);
  const inputProps = typeof slotProps?.input === 'object' ? slotProps.input : undefined;

  return (
    <TextField
      {...rest}
      type={visible ? 'text' : 'password'}
      autoComplete={autoComplete}
      slotProps={{
        ...slotProps,
        input: {
          ...inputProps,
          endAdornment: (
            <InputAdornment position="end">
              <IconButton
                aria-label={visible ? 'Ocultar contraseña' : 'Mostrar contraseña'}
                onClick={() => setVisible((actual) => !actual)}
                edge="end"
              >
                {visible ? <VisibilityOffRoundedIcon /> : <VisibilityRoundedIcon />}
              </IconButton>
            </InputAdornment>
          ),
        },
      }}
    />
  );
}
