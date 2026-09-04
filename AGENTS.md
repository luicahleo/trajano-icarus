# Trajano-Icarus — instrucciones para agentes

Fuente única para Codex, Kimi CLI, Claude, Gemini, Copilot y cualquier agente
futuro. Mantener este archivo **corto**: solo reglas que aplican a casi cualquier
tarea. Lo específico vive en `docs/ai/`.

Los archivos `CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md` y los
`.*ignore` por proveedor están **generados** desde este archivo. No editarlos a
mano: editar este, y correr `node quality/generar-adaptadores.mjs`.

## Prioridades

- Seguir el pedido explícito del usuario y no ampliar el alcance sin
  autorización.
- Documentos, textos e identificadores de dominio en español correcto, con
  acentos y en UTF-8 sin BOM. Nunca mojibake.
- Anti-PII no negociable: nunca registrar datos biométricos, documentos de
  identidad, credenciales, tokens ni registros nominales de acceso de
  trabajadores. Usar mensajes de error genéricos.
- Preservar los cambios ajenos y evitar operaciones destructivas.
- Nunca afirmar que algo está verde sin haber ejecutado el comando y visto la
  salida.

## Ramas

- `develop` es la rama por defecto y de trabajo: commit y push directos tras
  verificar. No hay pull requests.
- `master` es producción: recibe `develop` por merge fast-forward, **solo a
  pedido explícito del usuario**.
- No crear ramas de trabajo salvo pedido explícito.
- Detalle en `docs/ai/FLUJO_GIT.md`.

## Descubrimiento eficiente

1. Empezar solo con `git status --short --branch` y `git log -5 --oneline`.
2. No ejecutar listados recursivos ni volcados de todos los archivos al iniciar.
3. Buscar por término, símbolo o ruta probable; limitar el resultado primero y
   refinar antes de ampliar.
4. Leer fragmentos antes que archivos completos. Abrir solo el spec, el plan, el
   código y los tests vinculados con la tarea actual.
5. Revisar primero `git diff --stat` y después solo los diffs relevantes.
6. Resumir logs y errores por causa, archivo y línea; no pegar salidas extensas.

`docs/superpowers/specs/` y `docs/superpowers/plans/` son memoria consultable,
no contexto obligatorio. No cargarlos en bloque.

## Selección de proceso

- Pregunta, explicación o diagnóstico: investigar y responder; no modificar.
- Cambio pequeño y claro: implementar, probar en proporción y resumir.
- Feature, bloque o cambio arquitectónico: seguir `docs/ai/WORKFLOW.md` desde
  brainstorming hasta cierre.
- Continuación de otra sesión: leer primero `docs/ai/HANDOFF.md` si existe, y
  verificar cada afirmación importante contra git y los archivos actuales.

## Proyecto

- Trajano-Icarus es la refactorización de ICARUS: control de acceso de
  trabajadores (zonas, biométricos) y gestión avícola (granjas, galpones,
  producción de huevos, mortalidad, vacunación, alimentación, despachos,
  precios).
- El vocabulario y las reglas del negocio están en
  `docs/dominio/glosario-avicola.md`. Consultarlo antes de nombrar una entidad o
  inventar una regla.
- Backend .NET bajo `Icarus/`: solución con building blocks (Domain, Application,
  Observability), módulo Identity completo (JWT, usuarios, roles), módulo
  Clientes completo (agregados Cliente/Trabajador, filtros de tenant, entitlement
  por módulo) y módulo GestionAvicola (agregados Granja/Galpón —una granja activa
  por cliente—, recogidas de producción con huevos de descarte, mortalidad con
  ajuste de inventario, eficiencia diaria con umbral del 70 % y vacunación
  (catálogo global de programas de CAISY subido por el Administrador, asignación
  por galpón con día 0 = fecha de poblado, notificación de tareas al trabajador)),
  y el catálogo global de Notificaciones de Precios de Alimentos de CAISY
  (cuentas globales GestorCaisy con funcionalidades componibles, importación
  del PDF original a borrador editable, publicación versionada con vigencia),
  con puerta de calidad con gates de backend. El frontend React
  (PWA) vive bajo `web/` e incluye la UI de Gestión Avícola offline-first para
  recogida y mortalidad en el rol Trabajador (cola IndexedDB con idempotencia,
  precalentado del día, sesión offline sin persistir el token y sincronización
  automática), además de eficiencia, vacunación y la administración del catálogo
  de vacunación.
- Cuando existan, sus `AGENTS.md` locales complementarán a este archivo al
  trabajar en esos árboles.

## Verificación

- Durante TDD, ejecutar el test dirigido; la suite completa al integrar o cerrar.
- Un test que nunca se vio en rojo no prueba nada.
- Informar las pruebas no ejecutadas y el motivo.
- Desde el plan 2 la puerta exige Docker corriendo: los tests de integración
  usan Testcontainers.MsSql.

## Puerta de calidad

- Para cambios de código, configuración, build, tests o una mezcla de código y
  documentación, ejecutar `./verify.ps1` (o `./verify.sh`) antes de cada
  commit y push. Es obligatorio y sustituye a la revisión humana del código.
- Para un cambio exclusivamente documental, ejecutar como mínimo los gates de
  mojibake, enlaces y `git diff --check`. La puerta completa queda recomendada,
  pero no es requisito para ese commit documental aislado.
- Prohibido `--no-verify` en commit o push.
- Prohibido relajar una baseline, un umbral o una exclusión para que pase el
  gate. Si el gate falla, se arregla el contenido, no el gate.
- Las baselines de `quality/` solo se actualizan hacia mejor, en commit propio
  que explique la mejora.
- Detalle de cada gate: `docs/ai/PUERTA_CALIDAD.md`.

## Economía de contexto

- Una feature o bloque por sesión.
- Preferir sesiones nuevas con handoff breve frente a historiales largos.
- Usar el nivel de modelo más económico que mantenga la calidad; elevarlo ante
  decisiones de criterio. No bajarlo en cambios de configuración de build o de
  verificación, aunque parezcan mecánicos.
- No usar subagentes salvo trabajo verdaderamente independiente que compense su
  coste.
- Guardar las decisiones duraderas en specs; el chat no es documentación.
- Ahorro de tokens (regla decidida para este proyecto): el prompt caching es
  automático del proveedor, sin configuración ni acción; la palanca real es la
  sesión nueva por feature con handoff en `docs/ai/HANDOFF.md`. Usar `/compact`
  solo como red de seguridad cerca del límite de contexto, siempre con pista
  explícita de qué preservar. No usar mem0 en este proyecto: es redundante con
  los handoffs escritos y suma tokens en cada turno.
- Detalle en `docs/ai/ECONOMIA_TOKENS.md` y `docs/ai/CONTEXT-EFFICIENCY.md`.

Mapa completo de documentación: `docs/ai/README.md`.
