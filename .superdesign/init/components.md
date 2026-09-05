# Componentes compartidos

El frontend usa Material UI; estos son los adaptadores compartidos propios más relevantes.

## PaginaCabecera

- Ruta: `web/src/app/ui/PaginaCabecera.tsx`
- Cabecera reutilizable con título, subtítulo y acciones.

```tsx
import { Box, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';

interface PaginaCabeceraProps {
  titulo: ReactNode;
  subtitulo?: ReactNode;
  acciones?: ReactNode;
  variante?: 'h1' | 'h2' | 'h3' | 'h4' | 'h5' | 'h6';
}

export function PaginaCabecera({
  titulo,
  subtitulo,
  acciones,
  variante = 'h4',
}: PaginaCabeceraProps) {
  return (
    <Stack
      direction="row"
      spacing={2}
      sx={{ justifyContent: 'space-between', alignItems: 'flex-start', mb: 3, flexWrap: 'wrap' }}
    >
      <Box>
        <Typography variant={variante}>{titulo}</Typography>
        {subtitulo && (
          <Typography variant="body2" color="text.secondary">
            {subtitulo}
          </Typography>
        )}
      </Box>
      {acciones && (
        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexShrink: 0 }}>{acciones}</Box>
      )}
    </Stack>
  );
}

```
## TablaDatos

- Ruta: `web/src/app/ui/TablaDatos.tsx`
- Tabla genérica accesible con mensaje de vacío.

```tsx
import {
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
} from '@mui/material';
import type { ReactNode } from 'react';

export interface Columna<T> {
  clave: string;
  encabezado: ReactNode;
  alinear?: 'left' | 'right' | 'center';
  render: (fila: T) => ReactNode;
}

interface TablaDatosProps<T> {
  columnas: Columna<T>[];
  filas: T[];
  claveDeFila: (fila: T) => string;
  mensajeVacio?: string;
  etiqueta?: string;
}

export function TablaDatos<T>({
  columnas,
  filas,
  claveDeFila,
  mensajeVacio = 'No hay datos para mostrar.',
  etiqueta,
}: TablaDatosProps<T>) {
  return (
    <TableContainer component={Paper}>
      <Table aria-label={etiqueta}>
        <TableHead>
          <TableRow>
            {columnas.map((columna) => (
              <TableCell key={columna.clave} align={columna.alinear ?? 'left'}>
                {columna.encabezado}
              </TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {filas.length === 0 ? (
            <TableRow>
              <TableCell
                colSpan={columnas.length}
                align="center"
                sx={{ py: 4, color: 'text.secondary' }}
              >
                {mensajeVacio}
              </TableCell>
            </TableRow>
          ) : (
            filas.map((fila) => (
              <TableRow key={claveDeFila(fila)}>
                {columnas.map((columna) => (
                  <TableCell key={columna.clave} align={columna.alinear ?? 'left'}>
                    {columna.render(fila)}
                  </TableCell>
                ))}
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </TableContainer>
  );
}

```

## EstadoCarga

- Ruta: `web/src/app/ui/EstadoCarga.tsx`
- Estados compartidos de carga y error.

```tsx
import { Alert, Box, Button, CircularProgress } from '@mui/material';
import type { ReactNode } from 'react';

interface EstadoCargaProps {
  cargando: boolean;
  error: boolean;
  mensajeError?: string;
  onReintentar?: () => void;
  children?: ReactNode;
}

export function EstadoCarga({
  cargando,
  error,
  mensajeError = 'No se pudo cargar la información.',
  onReintentar,
  children,
}: EstadoCargaProps) {
  if (cargando) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
        <CircularProgress aria-label="Cargando" />
      </Box>
    );
  }
  if (error) {
    return (
      <Alert
        severity="error"
        action={
          onReintentar ? (
            <Button color="inherit" size="small" onClick={onReintentar}>
              Reintentar
            </Button>
          ) : undefined
        }
      >
        {mensajeError}
      </Alert>
    );
  }
  return <>{children}</>;
}

```

## DialogoConfirmacion

- Ruta: `web/src/app/ui/DialogoConfirmacion.tsx`
- Confirmación reutilizable para acciones sensibles.

```tsx
import { Button, Dialog, DialogActions, DialogContent, DialogTitle } from '@mui/material';
import type { ReactNode } from 'react';

interface DialogoConfirmacionProps {
  abierto: boolean;
  titulo: string;
  mensaje: ReactNode;
  etiquetaConfirmar?: string;
  color?: 'primary' | 'secondary' | 'error' | 'success' | 'info' | 'warning';
  pendiente?: boolean;
  onCancelar: () => void;
  onConfirmar: () => void;
}

export function DialogoConfirmacion({
  abierto,
  titulo,
  mensaje,
  etiquetaConfirmar = 'Confirmar',
  color = 'primary',
  pendiente = false,
  onCancelar,
  onConfirmar,
}: DialogoConfirmacionProps) {
  return (
    <Dialog open={abierto} onClose={onCancelar}>
      <DialogTitle>{titulo}</DialogTitle>
      <DialogContent>{mensaje}</DialogContent>
      <DialogActions>
        <Button onClick={onCancelar}>Cancelar</Button>
        <Button variant="contained" color={color} onClick={onConfirmar} disabled={pendiente}>
          {etiquetaConfirmar}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

```
