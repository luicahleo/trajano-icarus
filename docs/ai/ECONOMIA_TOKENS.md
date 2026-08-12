# Economía de tokens

El desarrollo alterna entre proveedores. Lo que se elige no es un modelo
concreto, que caduca, sino un **nivel de capacidad**.

## Niveles

| Nivel | Cuándo usarlo |
|---|---|
| Alto | Diseño, arquitectura, decisiones irreversibles, depuración difícil, cambios en la puerta de calidad o en el build |
| Intermedio | Implementación guiada por un plan, refactors acotados, tests |
| Económico | Renombrados, formato, ediciones mecánicas de una sola pasada |

Regla: usar el nivel más económico que mantenga la calidad, y **elevarlo ante
decisiones de criterio**. No bajarlo en cambios de configuración de build o de
verificación, aunque parezcan mecánicos: un error ahí es caro y silencioso.

## Equivalencias por proveedor

| Nivel | Anthropic | OpenAI / Codex | Google | Moonshot (Kimi) | DeepSeek |
|---|---|---|---|---|---|
| Alto | Opus | el modelo de razonamiento más capaz disponible | Gemini Pro | Kimi con razonamiento extendido | DeepSeek en modo razonador |
| Intermedio | Sonnet | el modelo general de la generación vigente | Gemini Flash | Kimi estándar | DeepSeek en modo conversacional |
| Económico | Haiku | el modelo compacto de la generación vigente | Gemini Flash Lite | Kimi estándar | DeepSeek en modo conversacional |

La tabla nombra familias, no versiones. Al cambiar de generación se actualiza
sola: solo hay que preguntarse qué modelo ocupa hoy cada casilla.

## Tamaño de sesión

- Una feature o un bloque por sesión.
- Preferir sesiones nuevas con un handoff breve antes que historiales largos: un
  contexto largo cuesta en cada turno, un handoff cuesta una vez.
- No usar subagentes salvo trabajo verdaderamente independiente que compense su
  coste.
- Guardar las decisiones duraderas en specs. El chat no es documentación.

Ver también [CONTEXT-EFFICIENCY.md](CONTEXT-EFFICIENCY.md).
