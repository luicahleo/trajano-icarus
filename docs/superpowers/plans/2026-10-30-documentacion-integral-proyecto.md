# Documentación integral de Trajano-Icarus Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Publicar dentro del repositorio una referencia integral, navegable y verificada del sistema Trajano-Icarus vigente.

**Architecture:** Una carpeta `docs/proyecto/` separa la documentación vigente de los specs históricos. Cada documento cubre un subsistema y enlaza al código; `README.md` actúa como índice y `maquinas-estados.md` consolida los ciclos de vida.

**Tech Stack:** Markdown, Mermaid, .NET/EF Core como fuentes backend, React/TypeScript como fuentes PWA y ASP.NET MVC como fuente GestorCaisy.

**Spec:** `docs/superpowers/specs/2026-10-30-documentacion-integral-proyecto-design.md`

## Global Constraints

- Español correcto, UTF-8 sin BOM, sin mojibake.
- El código vigente es la fuente de verdad; no presentar specs históricos como implementación.
- Sin secretos, credenciales, tokens, datos biométricos ni PII real.
- Sin dependencias nuevas ni cambios de comportamiento.
- Sin commit ni push salvo petición explícita del usuario.

## Review Focus

- Un estado o transición descrito desde un spec pero ausente en el código: contrastar con agregado y handlers.
- Una regla de tenant o permiso omitida: contrastar endpoint, policy y repositorio.
- Un cálculo presentado de forma ambigua: contrastar fórmula y unidad con dominio.
- Un flujo offline descrito como general cuando solo aplica a producción/mortalidad: contrastar dispatcher y cola.
- Un enlace Markdown roto o un diagrama con sintaxis inválida: ejecutar los gates documentales y validación disponible.

---

### Task 1: Inventario y mapa documental

**Files:**
- Create: `docs/proyecto/README.md`
- Modify: `docs/ai/README.md`

**Interfaces:**
- Consumes: estructura de la solución, Program.cs, glosario y specs relevantes.
- Produces: índice estable de los documentos de las tareas siguientes.

- [x] Inventariar proyectos, módulos, aplicaciones, contexts EF y features frontend.
- [x] Crear el índice con audiencia, mapa del producto y enlaces a todos los documentos.
- [x] Enlazar la nueva referencia desde `docs/ai/README.md`.
- [x] Comprobar que todos los enlaces del índice tengan destino al finalizar las demás tareas.

### Task 2: Arquitectura, identidad, permisos y datos

**Files:**
- Create: `docs/proyecto/arquitectura.md`
- Create: `docs/proyecto/identidad-clientes-permisos.md`
- Create: `docs/proyecto/datos.md`

**Interfaces:**
- Consumes: csproj, Program.cs, building blocks, Identity, Clientes, DbContexts y configuraciones EF.
- Produces: grafos de arquitectura y entidades, matriz de roles/funcionalidades y referencia de persistencia.

- [x] Documentar capas, dependencias, pipeline HTTP, contexts, migraciones y despliegue lógico.
- [x] Documentar autenticación, refresh, tenant, roles, módulos y funcionalidades.
- [x] Documentar entidades, relaciones, esquemas, índices, borrado lógico y concurrencia.
- [x] Contrastar cada afirmación con el archivo de código correspondiente.

### Task 3: Gestión avícola, recogida y mortalidad

**Files:**
- Create: `docs/proyecto/gestion-avicola.md`
- Create: `docs/proyecto/recogida-huevos-mortalidad.md`

**Interfaces:**
- Consumes: agregados Granja/Galpon, producción, mortalidad, eficiencia, vacunación, handlers y offline PWA.
- Produces: reglas, secuencias online/offline, fórmulas y máquina de estados de vacunación.

- [x] Documentar estructura, inventario, lotes y programa/tarea de vacunación.
- [x] Documentar recogidas vendibles/descarte, sellado diario, idempotencia y correcciones.
- [x] Documentar mortalidad, snapshots, eficiencia y umbral del 70 %.
- [x] Documentar precalentado, IndexedDB, cola, sincronización y conflictos.

### Task 4: Alimento, huevo y máquinas de estados

**Files:**
- Create: `docs/proyecto/pedidos-recepcion-alimentos.md`
- Create: `docs/proyecto/despacho-recepcion-huevos.md`
- Create: `docs/proyecto/maquinas-estados.md`

**Interfaces:**
- Consumes: agregados de precios/pedidos/despachos, handlers, repositorios y endpoints.
- Produces: flujos financieros y catálogo consolidado de estados, guardas y efectos.

- [x] Documentar publicaciones de precios y congelación de importes.
- [x] Documentar pedido, cupo, devolución, aceptación, entrega, recepción y balance.
- [x] Documentar despacho/recepción de huevo, recibo, crédito, correcciones y ajustes.
- [x] Consolidar todas las máquinas de estados, incluidas vacunación y control de acceso.

### Task 5: Control de acceso y aplicaciones

**Files:**
- Create: `docs/proyecto/control-acceso.md`
- Create: `docs/proyecto/frontend-pwa.md`
- Create: `docs/proyecto/gestorcaisy.md`

**Interfaces:**
- Consumes: ControlAcceso, ARGOS, router/features PWA, Program MVC y clientes API.
- Produces: flujos de kiosco/jornada/incidencia y mapas de ambas aplicaciones.

- [x] Documentar activación del kiosco, enrolamiento, marcación, jornadas e incidencias sin exponer PII.
- [x] Documentar rutas, guardas, estado remoto, sesión PWA y límites del offline.
- [x] Documentar cookie HttpOnly, refresh, autorización y operaciones GestorCaisy.

### Task 6: API, operación y cierre

**Files:**
- Create: `docs/proyecto/api.md`
- Create: `docs/proyecto/operacion-calidad.md`

**Interfaces:**
- Consumes: todos los endpoints, configuración pública, observabilidad, verify y despliegue.
- Produces: catálogo API y guía operativa no secreta.

- [x] Catalogar método, ruta, propósito, actor/policy y respuestas destacadas por módulo.
- [x] Documentar observabilidad, anti-PII, pruebas, puerta de calidad y despliegue.
- [x] Ejecutar `node quality/check-mojibake.mjs`, `node quality/check-enlaces.mjs` y `git diff --check`.
- [x] Ejecutar `./verify.ps1` si el entorno lo permite y revisar `git status`/diff final.
- [x] Informar cualquier verificación no disponible sin declararla como superada.
