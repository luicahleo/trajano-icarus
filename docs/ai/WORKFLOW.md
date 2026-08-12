# Flujo de trabajo

El ciclo completo, para features, bloques de fase y cambios arquitectónicos. La
ceremonia se escala al riesgo: una corrección de una línea no necesita spec, y un
bounded context nuevo no se improvisa.

## Escalado según riesgo

| Riesgo | Ceremonia |
|---|---|
| Pregunta, explicación, diagnóstico | Investigar y responder. No modificar nada. |
| Cambio pequeño y claro | Implementar, probar en proporción, resumir. |
| Feature o bloque | El ciclo completo de abajo. |
| Cambio arquitectónico o irreversible | El ciclo completo, y confirmación explícita antes de ejecutar. |

## El ciclo

1. **Preparación.** `git status --short --branch` y `git log -5 --oneline`. Leer
   el spec o el plan vigente si existe. No listar el repositorio entero.
2. **Brainstorming.** Explorar intención, alternativas y restricciones antes de
   diseñar. Termina cuando las decisiones están tomadas, no cuando se acaban las
   ideas.
3. **Spec.** Se guarda en `docs/superpowers/specs/AAAA-MM-DD-<tema>-design.md`.
   Registra decisiones y su porqué, y declara lo que queda fuera de alcance.
4. **Plan.** Se guarda en `docs/superpowers/plans/AAAA-MM-DD-<tema>.md`. Tareas
   pequeñas ordenadas por dependencias, con rutas exactas, prueba roja esperada,
   comando de verificación y commit previsto.
5. **TDD.** Escribir el test, verlo fallar por el motivo correcto, escribir la
   implementación mínima, verlo pasar. Un test que nunca se vio en rojo no
   prueba nada.
6. **Integración.** Puerta de calidad completa en verde, y commit.
7. **Revisión.** No hay revisor humano: la revisión es la puerta más una lectura
   del diff propio antes de hacer push.
8. **Cierre.** Actualizar el plan con lo hecho. Si el trabajo queda a medias,
   escribir `docs/ai/HANDOFF.md` desde la plantilla; si cierra, borrarlo.

## Reglas del ciclo

- Un spec o un plan que ya no describe la realidad es peor que ninguno.
- Los pasos se saltan hacia arriba, nunca hacia abajo: se puede decidir que algo
  no necesita spec; no se puede implementar sin haber visto el test en rojo.
- El alcance no se amplía sin autorización. Si aparece algo fuera de alcance, se
  anota y se sigue.
