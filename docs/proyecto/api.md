# Catálogo de API

## 1. Convenciones

Todas las rutas de esta referencia llevan el prefijo **`/api`**. La autenticación
principal es `Authorization: Bearer <JWT>`; el kiosco usa su propia cookie. Los
enums viajan como nombres. Las mutaciones JSON usan `Content-Type: application/json`;
archivos e imágenes usan multipart/form-data según el caso.

Abreviaturas de autorización:

| Etiqueta | Significado |
|---|---|
| Admin | Claim rol `Administrador`. |
| Autenticado | Cualquier principal Bearer válido. |
| Cliente | Claim rol `Cliente`. |
| Cliente CA | Cliente con módulo ControlAcceso vigente. |
| Nombres | Rol Cliente o Trabajador. |
| F:`X` | Funcionalidad avícola efectiva `X`. |
| Estructura | ProduccionHuevos, Mortalidad o Vacunacion. |
| Catálogo V | Vacunacion. |
| CAISY Alimento | GestorCaisy + `GestorPedidoAlimento`. |
| CAISY Huevo | GestorCaisy + `GestorRecepcionHuevos`. |
| CAISY Veterinario | GestorCaisy + `GestorVeterinario`. |
| Kiosco | Cookie de kiosco vigente + antiforgery cuando es mutación. |

Respuestas habituales: 200 JSON, 201 con id, 204 sin cuerpo, archivo binario,
400 validación, 401 sin sesión/tenant inactivo, 403 sin permiso, 404 recurso no
visible, 409 estado/concurrencia, 413 cuerpo excedido y 429 rate limit. Los errores
usan ProblemDetails e ids técnicos seguros.

## 2. Salud e identidad

| Método | Ruta | Acceso | Propósito |
|---|---|---|---|
| GET | `/health` | Público | Salud simple `{ estado: "ok" }`. |
| POST | `/identidad/sesion` | Público | Login; access token y cookie refresh HttpOnly. |
| POST | `/identidad/sesion/renovar` | Cookie refresh | Rotación atómica y nuevo access/refresh. |
| GET | `/identidad/me` | Autenticado | Usuario, rol, tenant, módulos y funcionalidades efectivas. |
| POST | `/diagnosticos/frontend` | Público + rate limit | Recibir diagnóstico sanitizado; 202. Máximo 16 KiB. |

## 3. Clientes y trabajadores

| Método | Ruta | Acceso | Propósito |
|---|---|---|---|
| POST | `/clientes` | Admin | Crear tenant y cuenta Cliente. |
| GET | `/clientes` | Admin | Listar tenants. |
| POST | `/clientes/{id}/suspender` | Admin | Suspender tenant. |
| POST | `/clientes/{id}/reactivar` | Admin | Reactivar tenant. |
| PUT | `/clientes/{id}/modulos` | Admin | Reemplazar módulos habilitados. |
| POST | `/clientes/{clienteId}/trabajadores` | Cliente | Crear trabajador y cuenta. |
| GET | `/clientes/{clienteId}/trabajadores` | Cliente | Listar trabajadores del tenant. |
| GET | `/clientes/{clienteId}/trabajadores/nombres` | Nombres | Solo ids/nombres para pantallas compartidas. |
| PUT | `/clientes/{clienteId}/trabajadores/{trabajadorId}/funcionalidades` | Cliente | Reemplazar funciones del trabajador. |
| POST | `/clientes/trabajadores/{id}/cese` | Cliente | Registrar fecha de cese. |
| DELETE | `/clientes/trabajadores/{id}` | Cliente | Desactivar trabajador. |

## 4. Cuentas globales CAISY

Todas exigen Admin.

| Método | Ruta | Propósito |
|---|---|---|
| POST | `/usuarios-caisy` | Crear cuenta con funciones componibles. |
| GET | `/usuarios-caisy` | Listar cuentas. |
| PUT | `/usuarios-caisy/{id}/funcionalidades` | Reemplazar bitmask funcional. |
| DELETE | `/usuarios-caisy/{id}` | Desactivar cuenta. |

## 5. Granjas, galpones, producción y eficiencia

| Método | Ruta | Acceso | Propósito |
|---|---|---|---|
| POST | `/granjas` | F:Granjas | Crear granja activa. |
| GET | `/granjas` | Estructura | Listar granjas visibles. |
| GET | `/granjas/{id}` | Estructura | Detalle. |
| PUT | `/granjas/{id}` | F:Granjas | Renombrar. |
| DELETE | `/granjas/{id}` | F:Granjas | Desactivar. |
| POST | `/granjas/{granjaId}/galpones` | F:Galpones | Crear galpón. |
| GET | `/granjas/{granjaId}/galpones` | Estructura | Listar galpones. |
| GET | `/galpones/{id}` | Estructura | Detalle, lote e inventario. |
| PUT | `/galpones/{id}` | F:Galpones | Actualizar datos/capacidad/fechas. |
| PUT | `/galpones/{id}/inventario` | F:Galpones | Ajuste manual de gallinas. |
| DELETE | `/galpones/{id}` | F:Galpones | Desactivar. |
| POST | `/galpones/{galponId}/produccion` | F:ProduccionHuevos | Registrar recogida idempotente. |
| GET | `/galpones/{galponId}/produccion?fecha=` | F:ProduccionHuevos | Recogidas y totales del día. |
| GET | `/galpones/{galponId}/eficiencia?desde=&hasta=` | F:ProduccionHuevos | Eficiencia calculada por rango. |
| POST | `/galpones/{galponId}/mortalidad` | F:Mortalidad | Registrar baja e inventario. |
| GET | `/galpones/{galponId}/mortalidad?fecha=` | F:Mortalidad | Bajas del día. |
| PUT | `/produccion/{id}` | F:ProduccionHuevos | Editar recogida del día abierto. |
| DELETE | `/produccion/{id}` | F:ProduccionHuevos | Desactivar recogida del día abierto. |
| PUT | `/mortalidad/{id}` | F:Mortalidad | Editar baja y ajustar inventario. |
| DELETE | `/mortalidad/{id}` | F:Mortalidad | Desactivar baja y revertir inventario. |

## 6. Vacunación

| Método | Ruta | Acceso | Propósito |
|---|---|---|---|
| GET | `/vacunacion/programas` | Catálogo V | Listar publicados activos. |
| GET | `/vacunacion/programas/{id}` | Catálogo V | Detalle de un publicado activo; 404 en otro caso. |
| GET | `/programas-vacunacion-caisy` | CAISY Veterinario | Catálogo completo salvo borradores eliminados. |
| GET | `/programas-vacunacion-caisy/{id}` | CAISY Veterinario | Detalle con estado e ítems activos. |
| POST | `/programas-vacunacion-caisy` | CAISY Veterinario | Crear borrador. |
| PUT | `/programas-vacunacion-caisy/{id}` | CAISY Veterinario | Editar datos del borrador. |
| POST | `/programas-vacunacion-caisy/{id}/cronograma-excel` | CAISY Veterinario | Importar/reemplazar cronograma del borrador; errores por fila; 413 sobre 5 MB. |
| POST | `/programas-vacunacion-caisy/{id}/publicar` | CAISY Veterinario | Publicar (exige cronograma). |
| DELETE | `/programas-vacunacion-caisy/{id}` | CAISY Veterinario | Eliminar borrador (lógico). |
| POST | `/programas-vacunacion-caisy/{id}/desactivar` | CAISY Veterinario | Desactivar publicado y retirar pendientes. |
| POST | `/galpones/{galponId}/plan-vacunacion` | F:Galpones | Asignar/reasignar y materializar tareas. |
| DELETE | `/galpones/{galponId}/plan-vacunacion` | F:Galpones | Quitar plan y desactivar pendientes. |
| GET | `/galpones/{galponId}/vacunacion/tareas` | F:Vacunacion | Historial del galpón. |
| GET | `/vacunacion/tareas` | F:Vacunacion | Vencidas/hoy y próximas 7 días. |
| POST | `/vacunacion/tareas/{id}/completar` | F:Vacunacion | Completar con fecha/aves/observaciones. |
| POST | `/vacunacion/tareas/{id}/cancelar` | Cliente + F:Vacunacion | Cancelar con motivo opcional. |

## 7. Precios de alimento — CAISY

Todas exigen CAISY Alimento. El grupo es `/precios-alimentos`.

| Método | Ruta | Propósito |
|---|---|---|
| POST | `/importar` | Importar PDF o Excel a borrador. PDF: 20 MiB máximo. |
| GET | `/` | Historial de publicaciones. |
| GET | `/vigente?fecha=` | Publicación aplicable a fecha/hoy. |
| GET | `/{id}` | Detalle y precios. |
| PUT | `/{id}` | Actualizar borrador. |
| POST | `/{id}/publicar` | Publicar con vigencia única. |
| POST | `/{id}/anular` | Anular solo publicación futura. |
| DELETE | `/{id}` | Descartar borrador lógicamente. |
| GET | `/{id}/documento-original` | Descargar original privado. |

## 8. Pedidos de alimento — tenant

Todas exigen F:PedidoAlimento. Grupo `/pedidos-alimento`.

| Método | Ruta | Propósito |
|---|---|---|
| POST | `/` | Crear borrador con granja activa. |
| GET | `/` | Listado paginado con filtros. |
| GET | `/precios-vigentes` | Precios para preparar el pedido. |
| GET | `/cupo` | Enviados/máximo y semana ISO actual. |
| GET | `/{id}` | Detalle, historial, entrega y recepción. |
| PUT | `/{id}` | Editar borrador. |
| DELETE | `/{id}` | Desactivar borrador. |
| POST | `/{id}/enviar` | Cupo, congelado, transición y aviso. |
| POST | `/{id}/recibir` | Recibir por línea + foto multipart obligatoria. |
| GET | `/{id}/nota/documentos/{documentoId}/vista` | Vista inline autorizada. |
| GET | `/{id}/nota/documentos/{documentoId}/original` | Original como adjunto. |
| GET | `/notificaciones?since=` | Bandeja tenant, contador y ETag/304. |
| POST | `/notificaciones/{id}/marcar-leida` | Marcar aviso del tenant. |

## 9. Pedidos de alimento — CAISY

Todas exigen CAISY Alimento. Grupo `/pedidos-alimento-caisy`.

| Método | Ruta | Propósito |
|---|---|---|
| GET | `/` | Bandeja global sin borradores. |
| GET | `/{id}` | Detalle no Borrador. |
| POST | `/{id}/devolver` | Volver a Borrador con motivo. |
| POST | `/{id}/rechazar` | Rechazo terminal con motivo. |
| POST | `/{id}/aceptar` | Aceptar con entrega estimada. |
| POST | `/{id}/entrega-estimada` | Cambiar fecha en Aceptado. |
| POST | `/{id}/despachar` | Registrar nota y entrega única. |
| GET | `/{id}/recibo.pdf` | Recibo de despacho. |
| GET | `/{id}/nota/documentos/{documentoId}/vista` | Vista del respaldo receptor. |
| GET | `/{id}/nota/documentos/{documentoId}/original` | Original autorizado. |
| GET | `/notificaciones?since=` | Bandeja CAISY, contador y ETag/304. |
| POST | `/notificaciones/{id}/marcar-leida` | Marcar aviso CAISY. |
| GET | `/{id}/credito` | **Histórico cerrado:** el handler exige rol Cliente, incompatible con esta ruta CAISY. No usar. |

## 10. Balance de alimento

| Método | Ruta | Acceso | Propósito |
|---|---|---|---|
| GET | `/balance-alimentos?desde=&hasta=` | F:PedidoAlimento | Gasto real por tipo y rango de fecha de pedido. |

## 11. Precios de huevo — CAISY

Todas exigen CAISY Huevo. Grupo `/precios-huevo-caisy`.

| Método | Ruta | Propósito |
|---|---|---|
| POST | `/importar` | Importar Excel a borrador; 5 MiB máximo. |
| GET | `/` | Historial. |
| GET | `/vigente?fecha=` | Publicación aplicable a fecha/hoy. |
| GET | `/corregir/previsualizar?erronea=&correctiva=` | Ajustes y tamaños sin precio correctivo. |
| POST | `/corregir` | Publicar correctiva, cerrar errónea y ajustar crédito. |
| GET | `/{id}` | Detalle. |
| PUT | `/{id}` | Editar borrador. |
| POST | `/{id}/publicar` | Publicar con vigencia única. |
| POST | `/{id}/anular` | Anular futura. |
| DELETE | `/{id}` | Descartar borrador. |
| GET | `/{id}/documento-original` | Descargar Excel original. |

## 12. Despachos de huevo — tenant

Todas exigen F:DespachoHuevo. Grupo `/despachos-huevo`.

| Método | Ruta | Propósito |
|---|---|---|
| POST | `/` | Crear borrador por tamaños. |
| GET | `/` | Listado paginado con filtros. |
| GET | `/precios-vigentes` | Precios unitarios para preparar. |
| GET | `/{id}` | Detalle. |
| PUT | `/{id}` | Editar borrador. |
| DELETE | `/{id}` | Desactivar borrador. |
| POST | `/{id}/despachar` | Foto + congelado + transición. |
| GET | `/credito` | Saldo privado, reciente y ajustes; handler solo Cliente. |
| GET | `/notificaciones?since=` | Bandeja tenant, contador y ETag/304. |
| POST | `/notificaciones/{id}/marcar-leida` | Marcar aviso del tenant. |

La policy del grupo permite la funcionalidad, pero el cálculo de crédito vuelve a
exigir rol Cliente: un Trabajador no obtiene el saldo.

## 13. Despachos de huevo — CAISY

Todas exigen CAISY Huevo. Grupo `/despachos-huevo-caisy`.

| Método | Ruta | Propósito |
|---|---|---|
| GET | `/` | Bandeja global sin borradores. |
| GET | `/{id}` | Detalle no Borrador. |
| POST | `/{id}/confirmar-recepcion` | Reconciliar, cerrar y notificar. |
| GET | `/{id}/recibo.pdf` | Recibo posterior a recepción. |
| GET | `/notificaciones?since=` | Bandeja CAISY, contador y ETag/304. |
| POST | `/notificaciones/{id}/marcar-leida` | Marcar aviso CAISY. |

## 14. Control de acceso — administración

Todas exigen Cliente CA salvo indicación.

| Método | Ruta | Propósito |
|---|---|---|
| GET | `/control-acceso/trabajadores/acceso` | Configuración de acceso del personal. |
| PUT | `/control-acceso/trabajadores/{trabajadorId}/habilitacion` | Habilitar/deshabilitar. |
| POST | `/control-acceso/trabajadores/{trabajadorId}/enrolamiento` | Enrolar con muestra e idempotencia. |
| POST | `/control-acceso/trabajadores/{trabajadorId}/revocacion` | Revocar rostro. |
| GET | `/control-acceso/jornadas` | Listar con filtros/paginación. |
| GET | `/control-acceso/jornadas/{id}` | Detalle y valores efectivos. |
| POST | `/control-acceso/jornadas/{id}/correcciones` | Revisión auditada con versión. |
| POST | `/control-acceso/marcaciones/manuales` | Alta manual con motivo e idempotencia. |
| GET | `/control-acceso/incidencias` | Listar incidencias. |
| GET | `/control-acceso/incidencias/{id}` | Detalle. |
| POST | `/control-acceso/incidencias/{id}/resolucion` | Resolver con marcación manual. |
| POST | `/control-acceso/incidencias/{id}/descarte` | Descartar con motivo. |
| GET | `/control-acceso/notificaciones?since=` | Avisos de incidencias y contador. |
| POST | `/control-acceso/notificaciones/{id}/marcar-leida` | Marcar aviso. |

## 15. Kiosco

| Método | Ruta | Acceso | Propósito |
|---|---|---|---|
| POST | `/control-acceso/kiosco/sesion` | Antiforgery + rate limit | Activar con credenciales Cliente. |
| GET | `/control-acceso/kiosco/sesion` | Kiosco | Recuperar expiración de sesión vigente. |
| DELETE | `/control-acceso/kiosco/sesion` | Kiosco | Revocar con reautenticación. |
| POST | `/control-acceso/kiosco/marcaciones` | Kiosco | Identificar/registrar/rechazar captura. |
| POST | `/control-acceso/kiosco/marcaciones/{propuestaId}/confirmar` | Kiosco | Confirmar salida propuesta. |

## 16. Sondeos solo Development/Testing

No forman parte del contrato productivo: `Program.cs` solo los registra en esos
entornos.

| Método | Ruta | Acceso |
|---|---|---|
| GET | `/clientes/sondeo/funcionalidad/granjas` | F:Granjas |
| GET | `/clientes/sondeo/funcionalidad/vacunacion` | F:Vacunacion |
| GET | `/clientes/sondeo/funcionalidad/produccionhuevos` | F:ProduccionHuevos |
| GET | `/clientes/sondeo/funcionalidad/mortalidad` | F:Mortalidad |
| GET | `/clientes/sondeo/funcionalidad-caisy/gestorpedidoalimento` | CAISY Alimento |
| GET | `/control-acceso/sondeo/administrar` | Cliente CA |
| GET | `/control-acceso/sondeo/marcar` | Trabajador elegible para marcar |

## 17. Fuentes

- `Icarus/src/Host/Icarus.Host/Program.cs`
- `Icarus/src/Host/Icarus.Host/Endpoints/`
- Reglas de negocio enlazadas desde [el índice](README.md).
