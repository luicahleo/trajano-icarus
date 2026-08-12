# Trabajo con agentes de IA

Este directorio define un proceso neutral para Codex, Kimi CLI, Claude, Gemini y
cualquier agente futuro. La información se divide por frecuencia de uso para no
pagar contexto inútil.

## Capas

1. `AGENTS.md` en la raíz: reglas esenciales, cargadas siempre.
2. `AGENTS.md` por árbol: reglas locales, solo al trabajar en ese árbol. Todavía
   no existe ninguno; llegan con el backend y el frontend.
3. Este directorio: proceso completo, bajo demanda.

## Documentos

| Documento | Cuándo leerlo |
|---|---|
| [WORKFLOW.md](WORKFLOW.md) | Antes de una feature, un bloque de fase o un cambio arquitectónico |
| [PUERTA_CALIDAD.md](PUERTA_CALIDAD.md) | Cuando un gate falla o hay que agregar uno |
| [FLUJO_GIT.md](FLUJO_GIT.md) | Antes de integrar, promover o tocar ramas |
| [ECONOMIA_TOKENS.md](ECONOMIA_TOKENS.md) | Al planificar la sesión: qué modelo y qué tamaño |
| [CONTEXT-EFFICIENCY.md](CONTEXT-EFFICIENCY.md) | Al explorar un repositorio que no conocés |
| [HANDOFF.template.md](HANDOFF.template.md) | Al cerrar una sesión con trabajo a medias |

## Qué leer al iniciar

| Tipo de tarea | Contexto inicial |
|---|---|
| Pregunta o diagnóstico | `AGENTS.md` y los archivos directamente relevantes |
| Cambio pequeño | Lo anterior más los tests vecinos |
| Feature o bloque | Lo anterior más `docs/ai/WORKFLOW.md` y el spec vigente |
| Continuación de otra sesión | Lo anterior más `docs/ai/HANDOFF.md`, validado contra git |

Los specs y planes de `docs/superpowers/` son **memoria consultable, no contexto
obligatorio**. Nunca cargarlos en bloque: buscar por nombre, símbolo o
dependencia y abrir solo los resultados relevantes.

## Compatibilidad entre agentes

El núcleo es siempre el mismo archivo. Cambia solo cómo llega a cada herramienta:

- Codex y Kimi CLI descubren `AGENTS.md` jerárquicamente, sin configuración.
- Claude Code lee `CLAUDE.md`, que importa el núcleo.
- Gemini CLI lee `GEMINI.md`, que importa el núcleo.
- Copilot lee `.github/copilot-instructions.md`, que apunta al núcleo en texto
  porque no soporta imports.
- DeepSeek es un modelo, no un harness: hereda el archivo del harness que lo
  hospeda.

Esos adaptadores están **generados**. No se editan a mano: se edita `AGENTS.md`
y se corre `node quality/generar-adaptadores.mjs`.

Las capacidades particulares de cada proveedor —memoria, hooks, subagentes,
comandos— son optimizaciones opcionales. Nunca deben ser necesarias para
entender ni ejecutar el proceso del proyecto.
