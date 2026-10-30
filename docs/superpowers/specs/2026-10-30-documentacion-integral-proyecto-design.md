# Diseño: documentación integral de Trajano-Icarus

## Intención

Crear una guía de referencia en español que permita entender el sistema completo
sin reconstruir su funcionamiento a partir de decenas de specs históricos. La
fuente de verdad para el comportamiento será el código vigente; los specs y el
glosario se usarán para intención, vocabulario y contexto, señalando cualquier
diferencia relevante.

La documentación servirá a desarrollo, mantenimiento, operación y personas que se
incorporan al proyecto. Debe explicar tanto la arquitectura transversal como los
flujos de negocio, en especial recogida de huevos, mortalidad, pedidos de
alimento, despacho y recepción de alimento, despacho y recepción de huevo,
crédito, vacunación y control de acceso.

## Alcance

- Visión general, actores, capacidades, mapa de navegación y stack tecnológico.
- Arquitectura de solución, dependencias entre proyectos, bounded contexts,
  pipeline HTTP, persistencia y aislamiento de tenants.
- Dominio y reglas de clientes, trabajadores, granjas, galpones, producción,
  mortalidad, eficiencia, vacunación, precios, pedidos, entregas, recepciones,
  despachos de huevo, crédito y control de acceso.
- Máquinas de estados completas, con estados, transiciones, actores, guardas,
  efectos, invariantes y reintentos.
- Contratos y catálogo de endpoints, permisos y respuestas relevantes.
- PWA, rutas, sesión, offline-first, IndexedDB, idempotencia y sincronización.
- Aplicación MVC GestorCaisy y su consumo exclusivo de la API.
- Modelo de datos y grafos de entidades, secuencias y dependencias.
- Observabilidad, anti-PII, configuración, migraciones, calidad, pruebas,
  despliegue y operación.
- Diagramas Mermaid embebidos en Markdown: contexto, contenedores, componentes,
  entidades, secuencias, estados y despliegue.
- Índice principal y enlaces relativos entre documentos y código.

## Decisiones

- Ubicación nueva: `docs/proyecto/`, para separar la referencia vigente de la
  memoria histórica de `docs/superpowers/`.
- Punto de entrada: `docs/proyecto/README.md`, enlazado desde `docs/ai/README.md`.
- Formato: Markdown UTF-8 sin BOM y Mermaid, sin dependencias nuevas ni imágenes
  generadas que puedan quedar desactualizadas.
- Todos los comportamientos se verificarán contra dominio, Application,
  endpoints, persistencia, frontend y pruebas según corresponda.
- Las fechas y estados se documentarán tal como estén implementados, no como
  deberían ser. Las limitaciones comprobadas se consignarán sin convertir esta
  tarea en una corrección de código.
- No se documentarán secretos, valores de `.env`, credenciales, tokens, datos
  biométricos ni información personal real.
- Se usarán referencias de ruta estables a archivos; no números de línea que
  envejecen con cualquier cambio.

## Estructura prevista

1. `README.md`: mapa general y guía de lectura.
2. `arquitectura.md`: solución, módulos, grafos, HTTP, datos y despliegue.
3. `identidad-clientes-permisos.md`: roles, tenants, entitlement y cuentas CAISY.
4. `gestion-avicola.md`: granjas, galpones, vacunación y reglas estructurales.
5. `recogida-huevos-mortalidad.md`: recogidas, descarte, inventario, eficiencia y
   offline-first.
6. `pedidos-recepcion-alimentos.md`: precios, pedido, despacho, recepción y balance.
7. `despacho-recepcion-huevos.md`: precios, despacho, recibo, crédito y ajustes.
8. `control-acceso.md`: kiosco, ARGOS, enrolamiento, marcación, jornadas e
   incidencias.
9. `maquinas-estados.md`: catálogo consolidado de todas las máquinas de estados.
10. `frontend-pwa.md`: componentes, rutas, sesión, caché y sincronización.
11. `gestorcaisy.md`: MVC, seguridad de cookie y flujos de oficina.
12. `api.md`: catálogo de endpoints agrupado por módulo.
13. `datos.md`: contextos EF, entidades, relaciones, índices y concurrencia.
14. `operacion-calidad.md`: configuración no secreta, observabilidad, pruebas,
    puerta de calidad y despliegue.

## Fuera de alcance

- Cambiar comportamiento, arquitectura, esquemas, dependencias o pruebas.
- Reescribir todos los specs históricos o declararlos vigentes.
- Generar una documentación OpenAPI nueva ni modificar contratos.
- Documentar código de terceros, `node_modules`, `dist`, binarios o migraciones
  una por una como si fueran diseño actual.
- Hacer commit, push o promoción a producción sin una petición explícita.

## Verificación

- Revisión cruzada de cada flujo con su agregado, handlers, endpoints y UI.
- Comprobación de enlaces, mojibake y `git diff --check`.
- Validación de sintaxis Mermaid con las herramientas disponibles, sin añadir
  dependencias; si no existe un validador local, se informará esa limitación.
- `./verify.ps1` al cierre por ser un cambio documental amplio; como mínimo se
  ejecutarán los gates documentales obligatorios del repositorio.

## Supuestos adoptados

- «Todo» significa todo el producto y sus transversales implementados en este
  repositorio, no cada línea de código ni cada archivo histórico.
- La documentación queda en el repositorio en español y no requiere publicación
  web externa.
- No se necesitan subagentes para completar el trabajo; la exploración y la
  redacción se realizarán en esta sesión.
