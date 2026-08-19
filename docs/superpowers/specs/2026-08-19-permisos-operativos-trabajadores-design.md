# Permisos operativos de trabajadores en Gestión Avícola — Diseño

Fecha: 2026-08-19
Estado: aprobado en brainstorming con el usuario

## Contexto

El entitlement actual distingue módulos del cliente y funcionalidades del
trabajador, pero usa el mismo catálogo para dos conceptos diferentes:

- administración de la estructura de la granja (`Granjas`, `Galpones`);
- trabajo operativo (`ProduccionHuevos`, `Mortalidad`).

Esto permite asignar `Granjas` o `Galpones` a un trabajador y obliga a la PWA a
pedir permisos estructurales para llegar a una recogida o una mortalidad. La UI
también consulta producción, mortalidad y eficiencia en bloque aunque el
trabajador solo tenga una de esas funcionalidades. El resultado son respuestas
403, pantallas completas de error y ciclos de navegación entre `/` y
`/avicola`.

El negocio aclara la separación: cada cliente tiene una única granja activa con
N galpones; solo el cliente administra esa estructura. Los trabajadores operan
únicamente sobre producción de huevos y mortalidad cuando el cliente se lo
asigna.

Este diseño corrige y completa:

- `docs/superpowers/specs/2026-08-14-entitlement-por-funcionalidad-design.md`;
- la decisión 8 y la arquitectura de navegación de
  `docs/superpowers/specs/2026-08-18-frontend-gestion-avicola-design.md`.

Las demás decisiones de esos documentos continúan vigentes.

## Decisiones

### 1. El cliente administra y también puede operar

El rol `Cliente` con el módulo `GestionAvicola` tiene todas las capacidades del
módulo. Puede crear, consultar, renombrar y desactivar su granja; crear,
consultar, modificar y desactivar sus galpones; ajustar inventario; y consultar
y gestionar producción de huevos y mortalidad.

No se configura funcionalidad por funcionalidad para el cliente. Retirar el
módulo retira todo el acceso efectivo al cliente y a sus trabajadores.

### 2. El trabajador solo recibe funcionalidades operativas

Las únicas funcionalidades asignables a un trabajador en este incremento son:

- `ProduccionHuevos`;
- `Mortalidad`.

`Granjas` y `Galpones` se conservan como capacidades internas porque expresan
la autorización estructural del cliente, pero no aparecen en la UI de
asignación y el backend rechaza asignarlas a un trabajador.

`Vacunacion`, `Alimentacion`, `Despachos` y `Precios` siguen en el catálogo del
módulo para el cliente, pero no se ofrecen a trabajadores hasta que sus
respectivos subproyectos definan el caso de uso operativo.

La lista enviada al endpoint de asignación reemplaza el conjunto operativo
completo del trabajador. Una lista vacía le quita todas las funcionalidades.

### 3. Lectura estructural implícita para poder operar

Un trabajador que tenga `ProduccionHuevos`, `Mortalidad` o ambas puede consultar
la única granja activa de su cliente y sus galpones. Esa lectura es contexto de
trabajo, no una funcionalidad asignable adicional.

El trabajador nunca puede:

- crear, renombrar ni desactivar la granja;
- crear, actualizar ni desactivar galpones;
- ajustar manualmente el inventario del galpón.

La mortalidad puede modificar el inventario únicamente como efecto de su caso
de uso de dominio. Eso no concede acceso al endpoint de ajuste manual.

### 4. Matriz de autorización de la API

| Operación | Cliente con `GestionAvicola` | Trabajador |
|---|---|---|
| Consultar granja y galpones | Permitido | Permitido con `ProduccionHuevos` o `Mortalidad` |
| Modificar granja o galpones | Permitido | Prohibido |
| Ajustar inventario manualmente | Permitido | Prohibido |
| Consultar y gestionar recogidas | Permitido | Solo con `ProduccionHuevos` |
| Consultar eficiencia | Permitido | Solo con `ProduccionHuevos` |
| Consultar y gestionar mortalidad | Permitido | Solo con `Mortalidad` |

La autorización real se aplica en cada endpoint. Ocultar botones o evitar una
consulta en la PWA es UX y no sustituye al backend.

### 5. Los permisos siempre son efectivos, no solo asignados

Para autorizar a un trabajador deben cumplirse simultáneamente estas
condiciones:

1. el cliente existe, está activo y tiene `GestionAvicola`;
2. el trabajador pertenece a ese cliente y está activo;
3. el trabajador tiene asignada la funcionalidad requerida.

La consulta de `/identidad/me` aplica las mismas reglas. Si se suspende al
cliente o se le retira el módulo, el trabajador recibe funcionalidades efectivas
vacías aunque se conserve su configuración persistida por trazabilidad. Al
reactivar el cliente o restaurar el módulo, recupera las asignaciones que aún
sean válidas.

### 6. Asignación desde la PWA

La pantalla de trabajadores permite al cliente abrir la configuración de un
trabajador activo y marcar `Producción de huevos`, `Mortalidad` o ambas. También
muestra las funcionalidades asignadas en la lista para que el estado sea
visible sin entrar al diálogo.

El guardado usa el endpoint existente:

`PUT /clientes/{clienteId}/trabajadores/{trabajadorId}/funcionalidades`

La UI no presenta funcionalidades estructurales ni funcionalidades futuras sin
caso de uso implementado. El backend mantiene la validación como autoridad y
responde con un error genérico ante una funcionalidad no asignable o no
disponible para el cliente.

### 7. Navegación sin ciclos

Después del login:

- `Administrador` entra en `/admin/clientes`;
- `Cliente` con Gestión Avícola entra en `/avicola`;
- `Trabajador` con alguna funcionalidad operativa entra en `/avicola`;
- `Trabajador` sin funcionalidades efectivas entra en `/inicio`.

`/inicio` es un destino terminal e informa que la cuenta no tiene tareas
habilitadas. Una guarda nunca redirige a `/` cuando `/` volvería a dirigir a la
ruta rechazada.

El enlace “Gestión Avícola” se muestra al trabajador solo cuando tiene
`ProduccionHuevos` o `Mortalidad`.

### 8. La UI carga únicamente lo autorizado

La pantalla avícola se compone por funcionalidades independientes:

- la estructura de granja y galpones se consulta si existe acceso avícola;
- producción y eficiencia solo se consultan y muestran con
  `ProduccionHuevos`;
- mortalidad solo se consulta y muestra con `Mortalidad`;
- las acciones de administración estructural solo aparecen para `Cliente`.

Un 403 de una sección no autorizada se evita no haciendo esa petición. Un error
real de una sección autorizada se presenta en esa sección y no elimina datos
cargados correctamente de otra funcionalidad.

Ejemplos:

- trabajador con solo `ProduccionHuevos`: ve granja, galpones, recogidas y
  eficiencia; no consulta ni muestra mortalidad;
- trabajador con solo `Mortalidad`: ve granja, galpones y bajas; no consulta ni
  muestra producción ni eficiencia;
- trabajador con ambas: ve y opera ambas secciones;
- cliente: ve y opera todo lo actualmente implementado.

### 9. Compatibilidad de datos

Los valores numéricos existentes del enum `Funcionalidades` permanecen estables.
No se renumeran flags.

No se hace una migración destructiva de datos: los bits históricos `Granjas` y
`Galpones` se ignoran al calcular permisos efectivos y no se devuelven a la
PWA. El siguiente guardado de la configuración reemplaza el conjunto completo y
los elimina naturalmente. La semilla del trabajador demo pasa a usar una
funcionalidad operativa. No se alteran los módulos de los clientes ni registros
de producción o mortalidad.

## Pruebas de aceptación

### Backend

- Un cliente con `GestionAvicola` obtiene 200 en estructura, producción,
  eficiencia y mortalidad.
- Un trabajador con `ProduccionHuevos` obtiene 200 en lectura estructural y
  producción, y 403 en mortalidad y mutaciones estructurales.
- Un trabajador con `Mortalidad` obtiene 200 en lectura estructural y
  mortalidad, y 403 en producción, eficiencia y mutaciones estructurales.
- Un trabajador con ambas obtiene 200 en ambas áreas operativas.
- Un trabajador sin funcionalidades obtiene 403 en todo el módulo.
- Un cliente suspendido, un trabajador desactivado o un cliente sin el módulo
  dejan al trabajador sin acceso efectivo.
- La asignación rechaza `Granjas`, `Galpones` y funcionalidades todavía no
  operativas para trabajadores.

### Frontend

- El cliente puede ver y reemplazar las funcionalidades operativas de cada
  trabajador.
- El login y la restauración de sesión llevan al destino permitido sin ciclos.
- Cada combinación de permisos realiza únicamente las llamadas autorizadas y
  muestra únicamente sus secciones y acciones.
- El trabajador no ve controles para administrar granja, galpones o inventario.
- El cliente conserva todos los controles estructurales y operativos.

## Fuera de alcance

- Implementar vacunación, alimentación, despachos o precios.
- Asignar esas funcionalidades futuras antes de implementar sus casos de uso.
- Permisos más finos dentro de producción o mortalidad, como separar consulta,
  alta, edición y desactivación.
- Cambiar la regla de una sola granja activa por cliente.
- Cola offline, sincronización en segundo plano o resolución de conflictos.
- Corregir otros defectos visuales que no intervengan en autorización,
  asignación o navegación.
