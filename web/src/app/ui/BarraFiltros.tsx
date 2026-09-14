import { useState } from 'react';
import { Button, MenuItem, Stack, TextField } from '@mui/material';

// Barra de filtros reusable (spec 2026-09-14): recibe una DECLARACIÓN de
// filtros (tipo, etiqueta, opciones) y no conoce pedidos ni despachos. El
// siguiente listado del sistema solo tiene que declarar sus filtros.
export type TipoFiltro = 'texto' | 'fecha' | 'seleccion';

export interface OpcionFiltro {
  valor: string;
  etiqueta: string;
}

export interface DeclaracionFiltro {
  clave: string;
  etiqueta: string;
  tipo: TipoFiltro;
  opciones?: OpcionFiltro[];
}

export type ValoresFiltros = Record<string, string>;

interface BarraFiltrosProps {
  filtros: DeclaracionFiltro[];
  valores?: ValoresFiltros;
  onAplicar: (valores: ValoresFiltros) => void;
  onRestablecer?: () => void;
}

function valoresIniciales(
  filtros: DeclaracionFiltro[],
  valores?: ValoresFiltros,
): ValoresFiltros {
  const inicial: ValoresFiltros = {};
  for (const filtro of filtros) inicial[filtro.clave] = valores?.[filtro.clave] ?? '';
  return inicial;
}

export function BarraFiltros({ filtros, valores, onAplicar, onRestablecer }: BarraFiltrosProps) {
  const [actuales, setActuales] = useState<ValoresFiltros>(() =>
    valoresIniciales(filtros, valores),
  );

  const cambiar = (clave: string, valor: string) =>
    setActuales((previos) => ({ ...previos, [clave]: valor }));

  const restablecer = () => {
    setActuales(valoresIniciales(filtros));
    onRestablecer?.();
  };

  return (
    <Stack
      direction={{ xs: 'column', sm: 'row' }}
      spacing={2}
      useFlexGap
      sx={{ flexWrap: 'wrap', alignItems: { xs: 'stretch', sm: 'center' } }}
    >
      {filtros.map((filtro) =>
        filtro.tipo === 'seleccion' ? (
          <TextField
            key={filtro.clave}
            select
            size="small"
            label={filtro.etiqueta}
            value={actuales[filtro.clave] ?? ''}
            onChange={(evento) => cambiar(filtro.clave, evento.target.value)}
            sx={{ minWidth: 180 }}
          >
            <MenuItem value="">Todas</MenuItem>
            {(filtro.opciones ?? []).map((opcion) => (
              <MenuItem key={opcion.valor} value={opcion.valor}>
                {opcion.etiqueta}
              </MenuItem>
            ))}
          </TextField>
        ) : (
          <TextField
            key={filtro.clave}
            size="small"
            type={filtro.tipo === 'fecha' ? 'date' : 'text'}
            label={filtro.etiqueta}
            value={actuales[filtro.clave] ?? ''}
            onChange={(evento) => cambiar(filtro.clave, evento.target.value)}
            slotProps={filtro.tipo === 'fecha' ? { inputLabel: { shrink: true } } : undefined}
            sx={{ minWidth: 180 }}
          />
        ),
      )}
      <Button variant="contained" onClick={() => onAplicar(actuales)}>
        Aplicar filtros
      </Button>
      <Button variant="text" onClick={restablecer}>
        Restablecer
      </Button>
    </Stack>
  );
}
