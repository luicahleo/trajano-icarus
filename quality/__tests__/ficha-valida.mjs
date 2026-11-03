// Ficha mínima válida que comparten las pruebas. No es un test: la puerta solo
// ejecuta *.test.mjs.

export const fichaValida = ({
  id = '2026-11-01-ejemplo',
  aprobado = 'abc1234',
  ruta = 'quality/lib/fichas.mjs',
} = {}) => `---
id: ${id}
tipo: calidad
modulos: [quality]
temas: [fichas, indice]
resumen: Ejemplo de ficha válida para las pruebas del formato.
aprobado: ${aprobado}
commits: [${aprobado}]
---

# Ejemplo

## Qué cambió

- Algo cambió.

## Por qué

Porque hacía falta.

## Decisiones

- Una decisión, con su motivo.

## Archivos clave

- \`${ruta}\` — formato y validación.

## Cómo probarlo

- \`node --test quality/__tests__/fichas.test.mjs\`

## Pendientes

- Ninguno.

## Enlaces

- [Tarea](tarea.md)
`;
