# Semilla del escenario de crédito de huevo en desarrollo

Fecha: 2026-09-12. Estado: aprobado en brainstorming, pendiente de implementar.

## Problema

`SemillaGestionAvicola` siembra una granja, dos galpones y unos registros de
producción y mortalidad. No siembra ningún precio. Desde el arranque en local,
eso deja la cadena de crédito de huevo (SP8/SP9) imposible de probar a mano:

- Sin `PublicacionPrecioHuevo` vigente, `GET /despachos-huevo/precios-vigentes`
  responde 404 y no se puede despachar huevo.
- Sin `NotificacionPreciosAlimentos` publicada, `EnviarPedidoAlimentoHandler`
  falla con «No hay una publicación de precios vigente».
- Sin despachos recibidos, el crédito de huevo siempre da 0.
- Sin notificaciones, la bandeja de SP9F no se renderiza.

Tiene que ser semilla y no un script contra la API: `FechasNegocio.Hoy()` lee
`DateTime.UtcNow` y no hay abstracción de reloj, así que por HTTP no se pueden
retroceder fechas. Sin retroceder, el crédito siempre da 0 porque
`ReglasCreditoHuevo.DiasDisponibilidadCredito` exige `FechaRecepcion <= hoy - 14`.

Los agregados sí reciben la fecha como parámetro (`despacho.Despachar(fecha, …)`,
`despacho.ConfirmarRecepcion(fecha, …)`), así que desde la semilla se puede
retroceder respetando los invariantes del dominio.

## Decisión de fondo: base común más capa de desarrollo

`Program.cs` define `esDesarrollo = IsDevelopment() || IsEnvironment("Testing")`,
y bajo esa condición corren las tres semillas. `IdentityFactory` arranca ese
mismo `Program` con `UseEnvironment("Testing")` e inyecta
`Semilla:ContrasenaPrueba`: las 143 pruebas de integración se autentican con las
cuentas semilla. Hay 101 referencias a esos correos en 21 archivos de prueba.

Development y Testing quieren cosas opuestas del mismo archivo. Testing necesita
una semilla mínima y predecible: cada dato de más puede romper una aserción, una
publicación de más puede pasar a ser «la vigente» y el índice único filtrado
sobre `FechaVigencia` puede colisionar. Desarrollo necesita lo contrario: muchos
tenants, fechas retrocedidas, estados raros y saldos de todos los signos.

Se descartó borrar la semilla actual y migrar las pruebas a crear sus cuentas:
es trabajo de días con riesgo alto de perder cobertura sin notarlo, y es otra
tarea. Se descartó también tener dos semillas paralelas e independientes: el
entorno local dejaría de parecerse a lo que las pruebas verifican.

La estructura elegida es **base común más capa de desarrollo**:

- **Base común** — lo que existe hoy: admin, `cliente@`, `trabajador@`, `c1@`,
  `t1@`, con su granja y sus galpones. Sigue corriendo en Development y en
  Testing. No se toca, salvo la contraseña.
- **Capa de desarrollo** — solo bajo `IsDevelopment()`: los tenants nuevos
  `c2@`, `c3@`, `c4@` con sus trabajadores, y toda la cadena de crédito.

Así Testing ve exactamente lo que ve hoy, y desarrollo queda con los cinco casos
comparables desde el primer arranque.

## Reparto de papeles por tenant

Cinco tenants, cada uno un caso distinto, para que la vista de CAISY del crédito
por cliente sea de verdad comparable. Los ítems citados son los de
`docs/ai/HANDOFF.md`; los ítems 1 a 5 están implementados y son los que la
semilla debe hacer navegables. Los ítems 6 a 8 todavía no existen.

| Tenant | Saldo | Qué ejercita |
|---|---|---|
| `cliente@` (demo) | Positivo holgado | Flujo sano de punta a punta |
| `c1@` | Negativo | Ítems 4 y 5: chip «Negativo», color accesible, notificación |
| `c2@` | Positivo pero insuficiente | Ítem 1: la confirmación de SP9E al enviar el pedido |
| `c3@` | Positivo con mucho movimiento | Ítem 2: desglose con despachos dentro y fuera de la ventana, más el ajuste por corrección |
| `c4@` | Cero, sin movimiento | Estados vacíos de las pantallas |

El signo se controla con la magnitud, no con estados distintos: las rutas de UI
son idénticas y solo cambia el número. El saldo es
`ingresos - recibidoReal - comprometidoPendiente + ajustes`
(`RepositorioBalanceCreditoHuevo.ObtenerSaldoDisponibleAsync`).

## Alcance

Dentro:

- Recursos globales: una `PublicacionPrecioHuevo` publicada vigente, un par
  errónea/correctiva ya corregido que justifica el ajuste, y una
  `NotificacionPreciosAlimentos` publicada.
- Por tenant: granja, galpones, producción y mortalidad para los tenants nuevos;
  despachos de huevo dentro y fuera de la ventana de 14 días, más uno despachado
  sin recibir y uno en borrador; pedidos de alimento cubriendo la máquina de
  estados; el ajuste de crédito; y notificaciones internas sin leer.
- Documentos reales en el almacén local.

Fuera:

- Vacunación. El catálogo global de programas de CAISY es otro recurso global
  con el mismo riesgo de colisión y no aporta nada al crédito.
- Los ítems 6 a 8 del handoff, que aún no están implementados.
- Cualquier cambio en la semilla que corra en Testing, salvo la contraseña.

## Decisiones

**D1. Alcance: la cadena de crédito completa, sin vacunación.** Lo mínimo para
la bandeja de SP9F deja el pedido de alimento igual de imposible que hoy y no
prueba la ventana de 14 días.

**D2. Signo del saldo: los cinco tenants cubren los dos signos y los casos
intermedios**, para no tener que editar datos a mano.

**D3. Gate: `IsDevelopment()` a secas** para todo lo nuevo. El índice único
filtrado `HasIndex(FechaVigencia).HasFilter("[Estado] = 1 AND [EstaActivo] = 1")`
sobre `PublicacionPrecioHuevo` —y su equivalente sobre `VigenteDesde` en
`NotificacionPreciosAlimentos`— hace probable que una publicación semilla tumbe
pruebas de precios o de pedidos si corriera en Testing.

**D4. Documentos: archivos reales en el almacén local.** `AlmacenDocumentosLocal`
y `AlmacenDocumentosPedidoLocal` escriben bytes en un archivo por Guid, así que
el costo es mínimo. Se usa un JPEG mínimo embebido, escrito a través del puerto
de aplicación y no a disco directo, con el SHA-256 calculado sobre los bytes
reales para que `DatosDocumentoNota` cuadre con el archivo. Aceptar la imagen
rota dejaría fallando justo las pantallas que se quieren mirar.

**D5. Cobertura: prueba de integración con base propia dentro del contenedor
compartido.** La suite comparte un único contenedor a propósito
(`IntegracionCollection` más `DisableTestParallelization`), y su comentario
advierte que nadie debe levantar otro SQL Server. La prueba crea una base nueva
en ese mismo servidor, arma un `ServiceProvider` mínimo e invoca el sembrado dos
veces para verificar la idempotencia. Sin esta prueba el escenario se pudriría
en silencio con el primer cambio de invariante.

**D7. Cuentas de oficina de CAISY sembradas en Development.** `gpa@icarus.test`
con `GestorPedidoAlimento` y `grh@icarus.test` con `GestorRecepcionHuevos`: las
mismas que `crear-usuario-caisy.ps1` y
`crear-usuario-gestor-recepcion-huevos.ps1` crean a mano. Sin ellas la mitad de
la cadena (publicar precios, confirmar recepción de despachos, gestionar
pedidos entrantes) no se puede recorrer tras arrancar con la base limpia. Los
scripts se conservan porque son la única vía en la VPS, donde la semilla de
desarrollo no corre; se les añadió una nota en la cabecera. El stack PC entra
en este caso: `docker-compose.prodlocal.yml` arranca la API con
`ASPNETCORE_ENVIRONMENT=Development` justamente para sembrar, así que
`.\iniciar-pc.ps1 -Perfil pcN -RecrearDatos -ConfirmarBorradoDatos` deja el
entorno completo sin correr ningún script.

**D6. Contraseña de desarrollo: `Admin123456!`** para todas las cuentas semilla.
Se cambia el valor de `Semilla:ContrasenaPrueba` en
`appsettings.Development.json`, el fallback de `Program.cs`, el valor por
defecto de `ICARUS_SEMILLA_PASSWORD` en `docker-compose.dev.yml` y
`docker-compose.prodlocal.yml`, y la documentación de `web/README.md`. Testing
no se ve afectado porque `IdentityFactory` inyecta la suya. Como las semillas
son idempotentes por existencia de la cuenta, la capa de desarrollo resetea la
contraseña de las cuentas semilla al valor configurado, para que las bases
locales ya creadas no queden con la contraseña vieja.

## Patrón de implementación

El mismo de `SemillaGestionAvicola`: Guids fijos con prefijo propio, idempotencia
por `AnyAsync(x => x.Id == GuidFijo)` y fechas relativas a `DateTime.UtcNow`
recalculadas en cada arranque. Precedente de construcción directa de un despacho
recibido con fechas explícitas:
`Icarus/tests/Icarus.IntegrationTests/CorreccionPrecioHuevoTests.cs`.

Los datos son ficticios. Ningún dato nominal real, por anti-PII.

## Riesgos

- El índice único filtrado sobre `FechaVigencia` y la ventana de 14 días son los
  dos invariantes que el sembrado puede romper. La prueba de D5 los cubre.
- Si armar el `ServiceProvider` mínimo de la prueba exige más cirugía de la
  esperada —por ejemplo que `ICurrentUser` arrastre medio Identity—, se para y se
  consulta antes de inventar una abstracción nueva.
