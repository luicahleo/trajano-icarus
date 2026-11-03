# Proceso portable: criterio con evidencia, registro de decisiones y revisor común

- Fecha: 2026-11-03
- Tarea: [`2026-11-03-proceso-portable-evidencia-decisiones`](../../ai/tareas/2026-11-03-proceso-portable-evidencia-decisiones/tarea.md)
- Plan: [`2026-11-03-proceso-portable-evidencia-decisiones.md`](../plans/2026-11-03-proceso-portable-evidencia-decisiones.md)
- Antecedentes: [sistema de agentes](2026-10-30-sistema-agentes-portables-design.md)
  y [agente documentador](2026-11-01-agente-documentador-design.md)

## Problema

El proceso de agentes ya obliga a ver una prueba en rojo, a verificar con la
puerta y a que el humano apruebe. Quedan tres huecos que el curso «Ingeniería de
Software con Agentes de IA» señala y que este repositorio confirma:

1. **Criterios sin evidencia.** El planificador escribe criterios «comprobables en
   la pantalla», pero nada dice qué prueba demuestra cada uno. «Compila y las
   pruebas pasan» no demuestra nada nuevo: ya pasaba antes del cambio. Un criterio
   sin evidencia es un deseo.
2. **Decisiones sin motivo.** Las decisiones transversales están en seis viñetas de
   `docs/proyecto/arquitectura.md` §9, sin motivo ni condición para revisarlas, y
   el resto se reparte en 52 specs. Un agente no puede saber si una tarea choca con
   una decisión ni si ha llegado el momento de reconsiderarla.
3. **Revisión solo por riesgo.** El revisor mira seguridad, tenant, PII,
   migraciones, reglas de negocio, máquinas de estados y `quality/`. No mira la
   calidad de las pruebas (aserciones débiles, pruebas del doble, pruebas
   existentes adaptadas al código) ni errores .NET comunes (lifetimes,
   `CancellationToken`, `.Result`, `!`), que aparecen en cualquier área.

Además, `quality/verify.mjs` mezcla el ejecutor con la lista de gates del
proyecto (`Icarus/Icarus.sln`, `web/`), y su mensaje de fallo usa voseo.

## Objetivo estratégico

Trajano-Icarus es el piloto de un kit de proceso que se replicará en proyectos con
la misma base (.NET 10 con building blocks, Serilog, Seq, Testcontainers, React y
puerta en Node). dev_Caserito es el ejemplo: tiene gates deterministas que aquí no
hay y no tiene el sistema de agentes. Por eso cada pieza de este bloque **separa
visiblemente lo genérico de lo específico**. No se extrae el kit todavía: la regla
del segundo uso real manda esperar a que otro proyecto lo adopte (bloque 3).

## Solución en una frase

Cada criterio de `tarea.md` declara su evidencia (`test` con ruta y nombre, o
`manual` justificado) y la CLI y el gate `Tareas` lo exigen por estado; las
decisiones transversales viven en `docs/decisiones/` con motivo humano y condición
de reconsideración, y los agentes las leen antes de diseñar; el revisor aplica un
checklist común portable y uno de dominio; y la puerta lee sus gates de
`quality/gates.json` con un ejecutor genérico que falla cerrado.

## Decisiones

Las que necesitan al usuario son D5, D9 y D12 (numeradas 2, 1 y 3 en `tarea.md`).

### D1. Formato: una línea `Evidencia:` bajo cada criterio numerado

```markdown
## Criterios de aceptación

1. Al guardar una recogida con más descarte que el total, la pantalla muestra
   el aviso y no guarda nada.
   Evidencia: test `web/src/gestion-avicola/Recogida.test.tsx` «rechaza más descarte que el total»
   Evidencia: manual — el aviso se valida en el dispositivo del galpón.
```

- Criterios numerados al inicio de línea dentro de `## Criterios de aceptación`.
  Un encabezado `###` agrupa y cierra el criterio anterior. Los números no se
  repiten, porque el rojo se registra por número.
- Cada criterio necesita al menos una línea `Evidencia:`, y puede tener varias.
- Alternativa descartada: una tabla `| # | Criterio | Evidencia |`. Los criterios
  ocupan varias líneas y las tablas markdown no admiten saltos; los diffs serían
  ilegibles. Las tareas ya usan listas numeradas.

### D2. Dos tipos de evidencia: `test` y `manual`

- ``test `<ruta>` «<nombre>»``: el nombre aparece literal en el archivo (el texto de
  `test(...)` o `it(...)`, o el método en .NET). Vale para node, Vitest y xUnit sin
  saber nada de cada uno.
- `manual — <justificación>`: solo lo que no se automatiza con sensatez. La
  justificación tiene al menos 10 caracteres, para que «manual — x» no pase.
- Alternativa descartada: un tipo `comando` o `gate`. Abre la puerta a «Evidencia:
  comando dotnet test», que es justo el antipatrón. Lo que comprueba un gate se
  demuestra con la prueba del gate.
- El desarrollador puede ajustar ruta y nombre al escribir la prueba; cambiar
  `test` por `manual` exige volver a `PLANIFICACION`.

### D3. El rojo se registra como evento `comando` con prefijo fijo

```powershell
node quality/tarea.mjs registrar <id> comando --texto "Rojo criterio 3: node --test quality/__tests__/x.test.mjs — falla porque falta la regla" --rol desarrollador --proveedor <p>
```

- El gate busca `^Rojo criterio N:` en los eventos `comando` de la bitácora.
  Basta un rojo por criterio, aunque el criterio cite varias pruebas.
- Usa un tipo de evento que ya existe: no cambia `validarBitacora` ni la CLI.
- Alternativa descartada: un comando `rojo` que ejecute la prueba y guarde el
  código de salida. Sería evidencia escrita por la CLI, más fuerte, pero obliga a
  que la CLI sepa ejecutar pruebas de tres ecosistemas y a filtrar una sola
  prueba en cada uno. Se reconsidera cuando el bloque 2 traiga `check-tdd` de
  dev_Caserito, que ya resuelve parte de eso.
- Riesgo aceptado: el rojo es autodeclarado. El gate exige que exista; el revisor
  lo contrasta con la prueba.

### D4. Dónde se comprueba: CLI al salir de planificación y gate por estado

| Estado | Comprobación |
|---|---|
| `RECIBIDA`, `PLANIFICACION`, `ESPERA_DECISION`, `BLOQUEADA` | Ninguna: los criterios pueden estar a medio escribir |
| `DESARROLLO` | Formato: cada criterio con evidencia válida, sin números repetidos |
| `VERIFICACION` a `DOCUMENTACION` | Formato, más cada prueba citada existe y contiene su nombre, más el rojo de cada criterio con prueba |
| `CERRADA`, `CANCELADA` | Ninguna |

- `avanzar` de `PLANIFICACION` a `DESARROLLO` exige además **al menos un
  criterio**. El gate no lo exige en `DESARROLLO`, porque un cambio pequeño entra
  directo desde `RECIBIDA` sin criterios.
- Las tareas cerradas no se revisan: son historia, y sus pruebas pueden moverse o
  renombrarse después. Comprobarlas haría que el gate se pudriera con cada
  refactorización. Así tampoco se tocan los `tarea.md` de las cuatro tareas
  cerradas, que no tienen evidencia.
- La comprobación de existencia empieza en `VERIFICACION`, que es justo cuando el
  verificador ejecuta la puerta: el gate `Tareas` revisa la propia tarea que se
  verifica.
- Alternativa descartada: un gate nuevo en `verify.mjs`. El gate `Tareas` ya lee
  cada `tarea.md` y cada bitácora.

### D5. Ruta de las decisiones: `docs/decisiones/NNN-slug.md` (decide el usuario)

- `docs/proyecto/` describe el producto vigente revisado contra el código; una
  decisión con su historia no encaja ahí. `docs/ai/` es proceso; las decisiones son
  del producto y de su arquitectura.
- En el kit, la carpeta, su `README.md` y su `plantilla.md` son portables; los
  registros numerados son de cada proyecto.
- Alternativa: `docs/proyecto/decisiones/`, más cerca de `arquitectura.md`.

### D6. Plantilla de seis apartados, inmutable una vez aceptada

```markdown
# NNN. Título en forma de decisión

- Estado: Aceptada
- Fecha: AAAA-MM-DD
- Alcance: Trajano-Icarus | proceso

## Contexto
## Decisión
## Motivo
## Alternativas descartadas
## Reconsiderar cuándo
```

- Seis apartados: estado y fecha (bloque inicial), contexto, decisión, motivo,
  alternativas descartadas y reconsiderar cuándo. `Alcance` dice si la decisión es
  del proyecto o del proceso, y sirve a la separación genérico/específico.
- El agente redacta; **el motivo lo pone el humano**. Si el agente no lo conoce
  por una fuente citable (una respuesta del usuario registrada en una bitácora o un
  pasaje explícito de una spec, enlazado), escribe
  `PENDIENTE: lo completa el humano.` Un motivo inventado es peor que ninguno.
- Un registro aceptado no se edita. Solo dos cambios: sustituir `PENDIENTE` por el
  motivo que dé el humano, y pasar el estado a `Sustituida por NNN` con su enlace en
  el mismo cambio que crea el registro nuevo.
- Estados: `Aceptada` y `Sustituida por NNN`. No hay `Propuesta`: una decisión
  nueva entra con la tarea que la introduce y el humano la acepta al aprobar la
  tarea.

### D7. Lectura obligatoria sin índice

- Planificador, desarrollador y revisor listan `docs/decisiones/` y leen las
  aceptadas antes de diseñar, implementar o revisar. Son ocho registros de unas 30
  líneas: unos 3000 tokens por sesión que diseña.
- Si la tarea contradice una decisión o cumple su condición de «Reconsiderar
  cuándo», el agente registra la pregunta con `registrar <id> decision` y pasa a
  `ESPERA_DECISION`. No la reinterpreta ni la ignora.
- Sin índice generado: con ocho archivos de nombre descriptivo, listar la carpeta
  basta. Reconsiderar con más de 20 registros, con un índice como el de las fichas.

### D8. Migración inicial: ocho registros

| N | Registro | Motivo |
|---|---|---|
| 001 | Trajano-Icarus es el piloto del kit de proceso (alcance: proceso) | Del usuario, en la nota de origen de la tarea |
| 002 | Errores por excepciones de dominio, no Result pattern | `PENDIENTE` |
| 003 | Multi-tenant por filas y claims | `PENDIENTE` |
| 004 | Soft delete en agregados de negocio | `PENDIENTE` |
| 005 | Fechas de negocio de Bolivia e instantes técnicos en UTC | `PENDIENTE` |
| 006 | Online deliberado para pedidos y despachos; offline solo recogida y mortalidad | `PENDIENTE` |
| 007 | Sin procesos programados para vigencias o crédito | `PENDIENTE` |
| 008 | Notificaciones internas persistentes como fuente de verdad | `PENDIENTE` |

- La búsqueda en las specs no encontró motivos explícitos para 003 a 008: solo
  afirmaciones («SP8 es deliberadamente online», «el registro persistente sigue
  siendo la fuente de verdad»). Esas citas van al contexto, enlazadas, no al motivo.
- `arquitectura.md` §9 pasa a enlazar los registros: una sola fuente.
- La fecha de 002 a 008 es la del registro, con la aclaración de que la decisión
  es anterior.

### D9. Revisión ligera en toda tarea con código y profunda por riesgo (decide el usuario)

- **Ligera:** checklist común sobre el diff. En toda tarea que cambia código,
  pruebas o configuración.
- **Profunda:** común y de dominio, con glosario y `docs/proyecto/` del área.
  Obligatoria cuando el diff toca una zona sensible listada en `dominio.md`: las
  de hoy (seguridad, autenticación, permisos, tenant, migraciones, reglas de
  negocio delicadas, máquinas de estados) y `quality/`.
- El revisor decide la profundidad leyendo las rutas del diff contra la lista de
  `dominio.md`; el orquestador puede pedir la profunda con una nota al avanzar. Así
  no hace falta coordinar la decisión entre roles.
- El verificador avanza a `REVISION` toda tarea que no sea exclusivamente
  documental; solo esas van directo a `ENTORNO`.
- **Por qué sí:** los defectos del checklist común no se correlacionan con el
  riesgo del área. Una prueba que solo afirma `NotNull` o una aserción relajada
  para que pase pueden estar en cualquier pantalla, y la puerta las da por buenas.
  Es la consecuencia directa de «compila y las pruebas pasan no basta».
- **Coste:** una lectura del diff por tarea de código, con el nivel alto del
  revisor. Un nivel intermedio para la ligera exigiría un octavo rol; se
  reconsidera si el coste se nota.
- **Alternativa (si el usuario dice no):** el orquestador sigue decidiendo cuándo
  interviene el revisor, y este aplica los dos checklists siempre que interviene.

### D10. Checklists en `docs/ai/revision/`

- `comun.md` es portable; `dominio.md` es de este proyecto y contiene también la
  lista de zonas que exigen revisión profunda.
- El rol revisor solo cita sus rutas. Los roles se copian a los adaptadores y no
  admiten enlaces relativos, y así el rol queda igual entre proyectos.
- Alternativa descartada: los checklists dentro del rol. Mezclaría lo portable con
  lo propio en un archivo que se copia a dos adaptadores.

### D11. Gates en `quality/gates.json` con ejecutor genérico que falla cerrado

```json
{ "nombre": "Frontend lint", "tipo": "npm", "script": "lint", "cwd": "web" }
```

- JSON declarativo con tres tipos: `node` y `dotnet` (con `args`) y `npm` (con
  `script`). `cwd` opcional, relativo y sin `..`. `motivo` opcional, para el porqué
  que hoy está en comentarios. Una clave desconocida es un error: un
  `"omitir": true` no puede desactivar un gate en silencio.
- `quality/lib/gates.mjs` valida y resuelve; la rareza de npm en Windows (CLI de
  npm lanzado por node, sin shell, por el aviso DEP0190) pasa al resolvedor
  genérico. `verify.mjs` queda sin rutas del proyecto.
- **Falla cerrado:** archivo ausente, JSON inválido, lista vacía o un gate inválido
  hacen fallar la puerta sin ejecutar nada. `ejecutarGates([])` ya no da verde.
- `verify.mjs` sigue exportando `GATES` (resueltos desde el JSON), así
  `verify.test.mjs` no cambia. Las pruebas nuevas van en archivos nuevos.
- **Prueba que congela la lista:** `gates-proyecto.test.mjs` compara la
  configuración con los diez gates vigentes. Quitar o debilitar uno exige cambiar
  esa prueba en el mismo diff, y el revisor lo ve. El bloque 2 la ampliará al
  traer gates.
- Alternativas descartadas: un `gates.config.mjs` (admite comentarios, pero
  también lógica, y no se valida igual en otro proyecto) y YAML (dependencia
  nueva).

### D12. Voseo restante en el kit (decide el usuario)

`quality/verify.mjs:68` («Arreglá») está en alcance. Recomendado: corregir también
`quality/check-adaptadores.mjs:36` («Corré»), `quality/check-enlaces.mjs:90`
(«Corregí», «creá»), `quality/check-mojibake.mjs:84` («Escribí», «necesitás»,
«ponelo») y `docs/ai/README.md:26` («conocés»). Son las
únicas apariciones fuera de specs, planes y tareas, ninguna prueba las cita, y
viajarían al kit.

### D13. Inventario en `docs/ai/KIT_PROCESO.md`, sin extraer nada

Una tabla por pieza con su parte portable y su parte propia. Cada documento nuevo
lo declara también al principio. El inventario anota lo específico que hoy está
dentro de piezas portables y no se toca en este bloque, por ejemplo la lista
`MODULOS` de `quality/lib/fichas.mjs`. Es la entrada del bloque 3.

### D14. Sin gate de formato para las decisiones

Ocho archivos escritos una vez y casi inmutables no justifican un gate. El gate
Enlaces ya cubre los enlaces. Reconsiderar si aparecen registros mal formados.

## Arquitectura

```text
quality/
  verify.mjs                 ejecutor genérico (portable)
  gates.json                 lista de gates (Trajano-Icarus)
  lib/gates.mjs              validar y resolver gates (portable)
  lib/criterios.mjs          leer y validar criterios con evidencia (portable)
  check-tareas.mjs           gate Tareas: suma revisarCriterios por estado
  tarea.mjs                  plantilla de crear; avanzar PLANIFICACION→DESARROLLO
  lib/tareas.mjs             validarTransicion con problemasCriterios
  __tests__/criterios.test.mjs, gates.test.mjs (portables)
  __tests__/gates-proyecto.test.mjs (Trajano-Icarus)
docs/
  decisiones/README.md, plantilla.md (portables), 001…008 (Trajano-Icarus)
  ai/revision/comun.md (portable), dominio.md (Trajano-Icarus)
  ai/KIT_PROCESO.md
```

`quality/lib/criterios.mjs` es lógica pura: recibe el texto de `tarea.md`, los
eventos y una función `leerPrueba(ruta)` que devuelve el contenido o `null`.
`check-tareas.mjs` inyecta la lectura con la misma comprobación de rutas que ya usa
para las fichas (`existeArchivoEnRepositorio`): relativa, dentro del repositorio y
fuera de `.git`.

## Fuera de alcance

- Gates de dev_Caserito: bloque 2. Extraer el kit y replicarlo: bloque 3.
- Result pattern, feature flags y cambios de arquitectura del backend.
- Gate de formato de decisiones (D14) y generalizar `MODULOS` (D13).
- Reescribir los `tarea.md` cerrados.

## Riesgos

Ver `tarea.md`. El principal: el rojo autodeclarado (D3) y la tarea que salta de
`ESPERA_DECISION` a `DESARROLLO` sin pasar por la comprobación de la CLI (el gate
sigue revisando el formato).
