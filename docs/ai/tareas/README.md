# Tareas de agentes

Cada carpeta es una tarea creada con `quality/tarea.mjs`. Contiene `tarea.md`,
`bitacora.jsonl`, que solo admite eventos nuevos, y desde el cierre `ficha.md`.
La bitácora no se edita a mano: el gate `Tareas` rehace la historia y falla si
deja de ser legal.

Las reglas están en [el protocolo de agentes](../agentes/PROTOCOLO.md).

## Criterios con evidencia

> Portable: el formato y su comprobación forman parte del kit de proceso.

Cada criterio de aceptación de `tarea.md` declara cómo se demuestra. Un
criterio sin evidencia es un deseo, y «compila y las pruebas pasan» no es
evidencia: ya pasaban antes del cambio.

```markdown
## Criterios de aceptación

1. Al guardar una recogida con más descarte que el total, la pantalla muestra
   el aviso y no guarda nada.
   Evidencia: test `web/src/gestion-avicola/Recogida.test.tsx` «rechaza más descarte que el total»
   Evidencia: manual — el aviso se valida en el dispositivo del galpón.
```

- Criterios numerados al inicio de línea, dentro de `## Criterios de aceptación`.
  Un encabezado `###` agrupa y cierra el criterio anterior. Los números no se
  repiten.
- Cada criterio lleva al menos una línea `Evidencia:`, de uno de dos tipos:
  - ``Evidencia: test `<ruta>` «<nombre>»``: el nombre aparece literal en el
    archivo de pruebas (el texto de `test(...)` o `it(...)`, o el nombre del
    método en .NET).
  - `Evidencia: manual — <justificación>`: solo lo que no se automatiza con
    sensatez. La justificación dice por qué, con 10 caracteres como mínimo.
- El planificador propone ruta y nombre; el desarrollador puede ajustarlos al
  escribir la prueba. Cambiar `test` por `manual` exige volver a
  `PLANIFICACION`.
- El desarrollador registra el rojo de cada criterio con prueba en cuanto lo ve:
  `node quality/tarea.mjs registrar <id> comando --texto "Rojo criterio <N>: <comando> — <motivo del fallo>" --rol desarrollador --proveedor <p>`.

Lo comprueban la CLI y el gate `Tareas`:

- `avanzar` de `PLANIFICACION` a `DESARROLLO` exige al menos un criterio y que
  todos tengan evidencia válida.
- De `DESARROLLO` a `DOCUMENTACION`, el gate exige el formato.
- Desde `VERIFICACION`, además, que cada prueba citada exista y contenga su
  nombre, y que cada criterio con prueba tenga su «Rojo criterio N:».
- Las tareas cerradas no se revisan: son historia, y sus pruebas pueden haberse
  movido.

## Consultar

[INDICE.md](INDICE.md) tiene una línea por ficha: id, tipo, módulos, temas y
resumen. Busca un término en él en lugar de leerlo entero, abre solo la ficha que
coincida y parte de sus «Archivos clave». Abre la spec o el plan solo si la ficha
no basta.

## Ficha de cierre

La escribe el documentador en `DOCUMENTACION` a partir de la plantilla de
`node quality/tarea.mjs ficha <id>`. Toda tarea cerrada tiene una.

```markdown
---
id: <id de la tarea, igual a su carpeta>
tipo: funcionalidad | correccion | calidad | configuracion | documentacion
modulos: [GestionAvicola, web]
temas: [vacunacion, campana]
resumen: Una línea de 20 a 160 caracteres.
aprobado: <commit de la aprobación en la bitácora>
commits: [<commits clave>]
---

# <título de la tarea>

## Qué cambió
## Por qué
## Decisiones
## Archivos clave
## Cómo probarlo
## Pendientes
## Enlaces
```

- Las siete secciones van en ese orden y ninguna queda vacía.
- Máximo 60 líneas y 4000 bytes.
- `modulos` sale de una lista cerrada: `Identity`, `Clientes`, `GestionAvicola`,
  `ControlAcceso`, `BuildingBlocks`, `Host`, `GestorCaisy`, `web`, `infra`,
  `deploy`, `quality`, `docs`.
- `temas`: de 1 a 8, en kebab-case y sin acentos, para que la búsqueda no
  dependa de escribir bien el acento.
- «Archivos clave»: hasta 12 viñetas con la forma ``- `ruta` — papel``. La ruta
  debe existir; si renombras o borras un archivo citado, corrige la ficha
  antigua.
- «Cómo probarlo»: las pruebas dirigidas y una remisión a los criterios de
  `tarea.md`, sin copiarlos.
- «Pendientes»: lo que quedó fuera, incluida la documentación de producto que
  falte. «Ninguno.» si no hay.
- Enlaza, no copies: reglas en el glosario y en `docs/proyecto/`, detalle en la
  spec y el plan.
- Sin datos personales ni credenciales.

## Índice

`INDICE.md` se genera con `node quality/tarea.mjs indice`. No se edita a mano;
ante un conflicto, se regenera. El gate `Tareas` falla si no coincide con las
fichas.
