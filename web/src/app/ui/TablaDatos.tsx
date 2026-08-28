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
