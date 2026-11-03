# Protocolo de agentes

Reglas comunes de todos los roles, sea cual sea el proveedor. Las decisiones y sus
motivos están en el
[spec del sistema de agentes](../../superpowers/specs/2026-10-30-sistema-agentes-portables-design.md).
`AGENTS.md` sigue mandando sobre este documento.

## Principios

- Cada tarea tiene un solo responsable, y el responsable lo fija el estado.
- El estado vive en la bitácora, nunca en la conversación.
- Registra mientras trabajas. Un proveedor puede detenerse sin aviso.
- La evidencia la escribe la CLI, no el agente.
- Solo el usuario aprueba una funcionalidad. Las pruebas automáticas no bastan.

## Estados y dueños

| Estado | Dueño | Salidas |
|---|---|---|
| `RECIBIDA` | orquestador | `PLANIFICACION`, `DESARROLLO`, `ESPERA_DECISION` |
| `PLANIFICACION` | planificador | `DESARROLLO`, `ESPERA_DECISION` |
| `ESPERA_DECISION` | humano | `PLANIFICACION`, `DESARROLLO` |
| `DESARROLLO` | desarrollador | `VERIFICACION`, `PLANIFICACION`, `ESPERA_DECISION` |
| `VERIFICACION` | verificador | `DESARROLLO`, y con evidencia verde `REVISION` o `ENTORNO` |
| `REVISION` | revisor | `DESARROLLO`, `ESPERA_DECISION`, y con evidencia verde `ENTORNO` |
| `ENTORNO` | operaciones | `DESARROLLO`, y con evidencia verde y commit `PRUEBA_MANUAL` |
| `PRUEBA_MANUAL` | humano | Solo `aprobar` o `rechazar` |
| `APROBADA` | orquestador | `DOCUMENTACION` |
| `DOCUMENTACION` | documentador | `CERRADA`, con ficha válida, índice al día y sin commits pendientes de push |
| `BLOQUEADA` | orquestador | Su estado previo |
| `CERRADA`, `CANCELADA` | nadie | Ninguna |

Todo estado no terminal puede pasar a `BLOQUEADA` y a `CANCELADA`. El orquestador
puede encaminar cualquier estado. Cuando el dueño es el humano, el orquestador
registra su respuesta.

`APROBADA` a `CERRADA` solo aparece en las tres bitácoras cerradas antes de que
existiera `DOCUMENTACION`. La CLI ya no la permite.

## Rechazo tras la prueba manual

| Clasificación | Cuándo | Destino |
|---|---|---|
| `funcional` | El código no cumple lo acordado | `DESARROLLO` |
| `entorno` | Docker, puertos, configuración o arranque | `ENTORNO` |
| `negocio` | La regla no está clara | `ESPERA_DECISION` |
| `alcance` | Es funcionalidad nueva | `PLANIFICACION` |

El informe debe traer pasos, resultado actual y resultado esperado. Si falta algo,
pídelo antes de clasificar.

## Comandos

```powershell
node quality/tarea.mjs crear <slug> --titulo "<título>" --rol orquestador --proveedor <p>
node quality/tarea.mjs resumen [<id>]
node quality/tarea.mjs avanzar <id> <ESTADO> --rol <rol> --proveedor <p> [--nota "<texto>"]
node quality/tarea.mjs registrar <id> <decision|bloqueo|comando|nota> --texto "<texto>" --rol <rol> --proveedor <p>
node quality/tarea.mjs verificar <id> --rol verificador --proveedor <p>
node quality/tarea.mjs rechazar <id> --clasificacion <c> --pasos "<p>" --actual "<a>" --esperado "<e>" --rol orquestador --proveedor <p>
node quality/tarea.mjs relevo <id> --proveedor <p> --motivo "<motivo>"
node quality/tarea.mjs ficha <id> --rol documentador --proveedor <p>
node quality/tarea.mjs indice
```

`<p>` identifica al proveedor en minúsculas, por ejemplo `anthropic` o `moonshot`.

Solo el usuario ejecuta, en su propia terminal:

```powershell
node quality/tarea.mjs aprobar <id>
```

## Commit y push

1. El desarrollador deja los cambios sin commitear.
2. El verificador registra la evidencia sobre ese contenido.
3. El orquestador hace commit con rutas concretas, incluida `docs/ai/tareas/<id>/`.
4. Operaciones levanta el entorno y pasa a `PRUEBA_MANUAL`.
5. Tras la aprobación, el orquestador hace push a `develop` y pasa a
   `DOCUMENTACION`.
6. El documentador escribe la ficha y regenera el índice, sin commit.
7. El orquestador avanza a `CERRADA` y commitea la bitácora, la ficha y el índice
   como cambio documental, tras los gates de mojibake, enlaces y tareas y
   `git diff --check`.

Una corrección es siempre un commit nuevo. Cualquier cambio de contenido invalida la
evidencia y obliga a verificar de nuevo.

## Ficha de cierre

Toda tarea cerrada tiene una ficha en `docs/ai/tareas/<id>/ficha.md`, con el
formato de [tareas/README.md](../tareas/README.md). La ficha enlaza, no copia.
`docs/proyecto/` y el glosario se actualizan en `DESARROLLO`, junto al código:
tras la aprobación solo se anota lo que falte.

## Relevo entre proveedores

1. Ejecuta `resumen` y contrasta cada dato con git.
2. Ejecuta `relevo` con tu proveedor y el motivo.
3. Continúa como dueño del estado actual. No repitas lo que ya tiene evidencia vigente.

## Datos prohibidos en la bitácora

Nunca registres credenciales, tokens, contenido de `.env`, datos biométricos,
documentos de identidad ni datos nominales de trabajadores. Describe los casos con
roles, por ejemplo «un trabajador del galpón». La CLI y el gate `Tareas` detectan
credenciales con formato reconocible. Los datos personales dependen de tu criterio.

## Límites de esta fase

Ningún rol despliega ni promueve `master`. El despliegue sigue siendo un pedido
explícito del usuario, fuera de este sistema.
