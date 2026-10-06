# Entitlement con múltiples módulos y limpieza de vistas de precios en GestorCaisy

## Objetivo

Este spec agrupa tres correcciones descubiertas en la misma sesión de trabajo,
todas pequeñas y acotadas, para que un solo agente económico las ejecute en
bloque:

1. **Bug de autorización en producción**: un trabajador con funcionalidades
   asignadas recibe 403 en endpoints a los que sí debería tener acceso,
   siempre que su Cliente tenga más de un módulo habilitado.
2. **Quitar la advertencia "Precio actual esperado"** de las vistas de
   Detalles de precios de alimento y de huevo en Trajano.GestorCaisy, de punta
   a punta (vista, contrato de API y backend).
3. **Destacar visualmente la publicación vigente** en el historial de precios
   de alimento y de huevo en Trajano.GestorCaisy, para que el Gestor CAISY
   sepa a simple vista cuál rige hoy.

## 1. Bug de entitlement con múltiples módulos

### Diagnóstico

`FuncionalidadesModulos.FuncionalidadesDelModulo(Modulos modulo)`
(`Icarus/src/Clientes/Icarus.Clientes.Domain/FuncionalidadesModulos.cs:23`) es
un `switch` que solo reconoce el valor exacto `Modulos.GestionAvicola`:

```csharp
public static Funcionalidades FuncionalidadesDelModulo(Modulos modulo) => modulo switch
{
    Modulos.GestionAvicola => Funcionalidades.Granjas | Funcionalidades.Galpones | ...,
    _ => Funcionalidades.Ninguno,
};
```

Dos consumidores lo usan de forma distinta:

- `ConsultaPermisosActuales.FuncionalidadesDe` (la que alimenta
  `GET /identidad/me` y por lo tanto el menú del frontend) **itera** cada
  valor de `Modulos` y hace OR de los resultados — por eso el menú siempre
  muestra correctamente qué puede ver un trabajador.
- `VerificadorEntitlement.TieneFuncionalidadAsync` (la que de verdad autoriza
  cada request) le pasa el bitmask **completo** de `ModulosHabilitados` del
  Cliente directo al switch, sin iterar. En cuanto ese Cliente tiene
  `GestionAvicola` combinado con cualquier otro módulo (hoy, `ControlAcceso`),
  el switch no matchea ningún caso, cae en `_` y devuelve `Funcionalidades.Ninguno`.
  La intersección con las funcionalidades del trabajador da siempre vacío:
  **403 en todo** para cualquier trabajador de un Cliente con más de un
  módulo activo, aunque el menú diga que sí tiene acceso.

Confirmado en producción (172.26.30.84): `GET /api/granjas` devuelve 403 para
un trabajador cuyo menú muestra "Pedidos de alimento" y "Gestión Avícola"
como habilitados.

### Corrección

Mover la iteración (que ya funciona en `ConsultaPermisosActuales`) a
`FuncionalidadesModulos` como implementación canónica, para que ambos
consumidores usen la misma lógica correcta y no haya dos copias del mismo
cálculo:

```csharp
// FuncionalidadesModulos.cs
public static Funcionalidades FuncionalidadesDelModulo(Modulos modulos)
{
    var acumulado = Funcionalidades.Ninguno;
    foreach (var modulo in Enum.GetValues<Modulos>())
        if (modulo != Modulos.Ninguno && modulos.HasFlag(modulo))
            acumulado |= FuncionalidadesDeUnModulo(modulo);
    return acumulado;
}

private static Funcionalidades FuncionalidadesDeUnModulo(Modulos modulo) => modulo switch
{
    Modulos.GestionAvicola => Funcionalidades.Granjas | Funcionalidades.Galpones
        | Funcionalidades.ProduccionHuevos | Funcionalidades.Mortalidad
        | Funcionalidades.Vacunacion | Funcionalidades.Alimentacion
        | Funcionalidades.Despachos | Funcionalidades.Precios
        | Funcionalidades.PedidoAlimento | Funcionalidades.DespachoHuevo,
    _ => Funcionalidades.Ninguno,
};
```

`VerificadorEntitlement.TieneFuncionalidadAsync` no cambia (ya llama a
`FuncionalidadesModulos.FuncionalidadesDelModulo(contexto.ModulosHabilitados)`
tal cual); el fix vive enteramente en `FuncionalidadesModulos`.
`ConsultaPermisosActuales.FuncionalidadesDe` se elimina y sus dos llamadas
pasan a usar `FuncionalidadesModulos.FuncionalidadesDelModulo(...)`
directamente, quitando la lógica duplicada.

### Fuera de alcance

- No se toca `RequisitoCatalogoVacunacion` ni `ManejadorCatalogoVacunacion`:
  consumen `IVerificadorEntitlement` por interfaz y se benefician del fix sin
  cambios propios.
- No se agregan módulos ni funcionalidades nuevas.

## 2. Quitar la advertencia "Precio actual esperado"

### Contexto

El spec 2026-09-15 agregó `PrecioAnteriorEsperado` (el precio vigente a la
fecha del documento, para el mismo tipo/tamaño) como dato puramente
informativo en las vistas de Detalles de precios de alimento y de huevo en
Trajano.GestorCaisy, con un banner de advertencia y una etiqueta "Diferencia"
cuando no coincide con el precio del documento importado. El Gestor CAISY
indica que esa columna no aporta valor y pide quitarla. Nadie más consume
`PrecioAnteriorEsperado` (ni el PWA, ni Recepciones): se elimina de punta a
punta en vez de dejar código muerto.

### Cambios

- **Backend** (`Icarus.GestionAvicola.Application`):
  - `ComandosPreciosAlimentos.cs`: `DetallePrecioResumen` pierde el campo
    `PrecioAnteriorEsperado`. `ObtenerNotificacionPreciosHandler` y
    `ObtenerPrecioVigenteHandler` dejan de calcular `anterior` y de pasarlo a
    `MapeadorPrecios.Mapear`. `MapeadorPrecios.Mapear` pierde el parámetro
    `anterior` y el diccionario `preciosAnteriores`.
  - `ComandosPreciosHuevo.cs`: mismo cambio simétrico sobre
    `DetallePrecioHuevoResumen`, `ObtenerPublicacionPrecioHuevoHandler`,
    `ObtenerPrecioHuevoVigenteHandler` y `MapeadorPreciosHuevo.Mapear`.
  - No se toca `CorregirPublicacionPrecioHuevoVigenteHandler`: su propia
    llamada a `ObtenerVigenteAsync(hoy)` para validar que se corrige la
    publicación realmente vigente es independiente de este cálculo.
- **Contrato del MVC** (`Trajano.GestorCaisy/Servicios/ContratosApi.cs`):
  `DetallePrecioApi` y `DetallePrecioHuevoApi` pierden el parámetro
  `PrecioAnteriorEsperado`.
- **Vistas**: `Views/Precios/Detalles.cshtml` y
  `Views/PreciosHuevo/Detalles.cshtml` pierden la columna "Precio actual
  esperado" / "Precio anterior esperado", la función local `EsAdvertencia`,
  la variable `hayAdvertencias`, el banner de advertencia y el `<span
  class="advertencia-precio">`.
- **CSS** (`wwwroot/css/estilos.css`): se eliminan `.fila--advertencia` y
  `.advertencia-precio` (líneas 422-428); nadie más las usa tras este cambio.
- **Datos de prueba**: `ApiIcarusFalsa.CrearDetalle` y `CrearDetalleHuevo`
  pierden el último argumento posicional (el precio anterior esperado) de
  cada `DetallePrecioApi`/`DetallePrecioHuevoApi`.

La regla de que "Precio actual" ya no bloquea publicar (spec 2026-09-15/
2026-10-04) no cambia: esto solo quita la advertencia visual, no ninguna
validación de publicación.

## 3. Destacar la publicación vigente en el historial

### Contexto

`Views/Precios/Index.cshtml` y `Views/PreciosHuevo/Index.cshtml` listan el
historial completo de notificaciones/publicaciones (`ListarHistorialAsync`
las devuelve ordenadas por vigencia descendente, luego por fecha de
documento/notificación descendente). Como una publicación `Publicada` sigue
apareciendo en el historial para siempre, el Gestor CAISY no tiene forma de
distinguir a simple vista cuál de todas las filas "Publicada" es la que rige
hoy.

### Diseño

La fila vigente es, por definición, la **primera** fila del historial (ya
viene ordenado) cuyo `Estado == "Publicada"` y cuya vigencia (`VigenteDesde`/
`FechaVigencia`) sea `<= hoy` — exactamente el mismo criterio que usa
`ObtenerVigenteAsync` en el backend. No hace falta ninguna llamada nueva a la
API: alcanza con un cálculo local sobre la lista que el Index ya recibe,
usando `FechasDeOficina.Hoy()` (ya usado en `Models/PreciosVistas.cs` y
`PreciosHuevoVistas.cs` para `EsPublicacionEfectiva`).

- Se agrega un wrapper de vista por cada historial, siguiendo el patrón ya
  existente de `VistaDetalles.Crear`/`VistaDetallesHuevo.Crear`:
  - `VistaHistorialPrecios(IReadOnlyList<NotificacionPreciosResumenApi> Notificaciones, Guid? VigenteId)`
    con un `Crear(IReadOnlyList<NotificacionPreciosResumenApi>)` estático en
    `Models/PreciosVistas.cs`.
  - `VistaHistorialPreciosHuevo(IReadOnlyList<PublicacionPrecioHuevoResumenApi> Publicaciones, Guid? VigenteId)`
    con su `Crear` en `Models/PreciosHuevoVistas.cs`.
- `PreciosController.Index` y `PreciosHuevoController.Index` devuelven ese
  wrapper en vez de la lista desnuda.
- En la tabla de `Index.cshtml` (ambas), la fila cuyo `Id == Model.VigenteId`
  gana la clase `fila--vigente` y, junto al chip de estado, un chip adicional
  "Vigente ahora".
- CSS nuevo en `estilos.css`: `.fila--vigente` (tinte con `--aqua-claro` y
  borde izquierdo `--aqua`) y `.chip--vigente-ahora` (fondo `--aqua`, texto
  blanco, para distinguirlo del chip de estado "Publicada" que ya usa
  `--aqua-claro`).
- Si ninguna fila cumple el criterio (no hay publicación vigente hoy —
  todas futuras, o no hay ninguna `Publicada`), `VigenteId` es `null` y
  ninguna fila se destaca.

### Fuera de alcance

- No se toca el PWA ni ningún listado de historial fuera de
  Trajano.GestorCaisy (decisión explícita: solo GestorCaisy).
- No se agrega ningún endpoint nuevo a la API ni al cliente HTTP del MVC.

## Criterios de aceptación

1. Un trabajador de un Cliente con dos o más módulos habilitados (por
   ejemplo `GestionAvicola` + `ControlAcceso`) y una funcionalidad asignada
   (por ejemplo `ProduccionHuevos`) recibe 200, no 403, al consultar un
   endpoint que exige esa funcionalidad.
2. `Views/Precios/Detalles.cshtml` y `Views/PreciosHuevo/Detalles.cshtml` ya
   no muestran la columna "Precio actual/anterior esperado" ni ningún banner
   de advertencia relacionado.
3. En `Views/Precios/Index.cshtml` y `Views/PreciosHuevo/Index.cshtml`, la
   fila de la publicación vigente hoy se distingue visualmente (tinte +
   chip "Vigente ahora") de todas las demás; si no hay ninguna vigente, no se
   destaca ninguna fila.
