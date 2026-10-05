# Notificaciones del navegador en Trajano.GestorCaisy

## Objetivo

Hoy, un Gestor CAISY solo se entera de una novedad de pedidos de alimento o de
recepción de huevo si está mirando esa página en particular: cada una sondea su
propio contador, pero ninguna señal llega si está en otra pantalla de la
aplicación (por ejemplo, revisando precios). Este spec agrega dos canales
complementarios, visibles en toda la aplicación:

1. Una campanita en el header con badge y desplegable — el canal confiable,
   no depende de ningún permiso del navegador.
2. Un aviso nativo del navegador (Web Notifications API) cuando el gestor
   concede el permiso — más cómodo, pero reversible solo manualmente una vez
   negado.

## Contexto y problema actual

- `Views/Pedidos/Index.cshtml` y `Views/RecepcionesHuevo/Index.cshtml` ya
  muestran, cada una en su propia página, una bandeja de novedades
  (`Model.Notificaciones.Items`) con un contador sondeado cada 30 s por
  `wwwroot/js/aplicacion.js` contra `GET {Controlador}/Notificaciones/Contador`
  — hoy ese endpoint responde solo `{ "contador": n }`.
- Los datos ya existen del lado de la API (`IApiIcarusClient.ListarNotificacionesPedidoAsync`
  / `ListarNotificacionesDespachoHuevoAsync`, contrato `BandejaNotificacionesApi`
  / `BandejaNotificacionesDespachoHuevoApi` con `Items` + `Contador`); el
  problema es de superficie (dónde se muestra), no de disponibilidad de datos.
- `Views/Pedidos/Index.cshtml` traduce cada `Tipo` a texto legible con un
  helper privado `EtiquetaNotificacion`/`EtiquetaChip` dentro de su propio
  `@functions`; `Views/RecepcionesHuevo/Index.cshtml` tiene el mismo nombre de
  método pero como stub (`tipo => tipo`) porque la corrección del 2026-09-14
  retiró el único tipo (`CreditoInsuficiente`) que esa bandeja emitía — hoy esa
  bandeja normalmente está vacía. Este spec no reintroduce ningún tipo nuevo de
  notificación, solo expone las que ya existen fuera de su página actual.
- `_Layout.cshtml` ya gatea el menú lateral por `ReclamosCaisy.TieneGestorPedidoAlimento`
  / `TieneGestorRecepcionHuevos`; los componentes nuevos usan la misma
  comprobación para que un gestor con una sola funcionalidad solo vea y sondee
  lo que le corresponde.
- `Trajano.GestorCaisy` es deliberadamente server-rendered sin Service Worker
  (regla ya existente del proyecto). La Web Notifications API no lo requiere:
  `new Notification(...)` funciona desde cualquier página mientras la pestaña
  siga abierta (minimizada o en segundo plano), así que este spec no necesita
  ni agrega un Service Worker.

## Decisiones

### Backend (solo `Trajano.GestorCaisy`; la API de Host no cambia)

- `EtiquetaNotificacion` y `EtiquetaChip` se extraen de los `@functions` de
  cada vista a métodos estáticos públicos en `Models/PedidosVistas.cs` y
  `Models/RecepcionesHuevoVistas.cs` respectivamente (donde ya viven otros
  helpers de esas vistas). Las vistas pasan a llamarlos desde ahí; el texto
  no cambia.
- `PedidosController.ContadorNotificaciones` y
  `RecepcionesHuevoController.ContadorNotificaciones` dejan de devolver
  `Json(new { contador = notificaciones.Contador })` y devuelven un DTO
  enriquecido (ver «Contrato de datos» más abajo) con el mismo `contador` más
  una lista `items` ya traducida a texto. Es un cambio aditivo: el sondeo por
  página que ya existe en `aplicacion.js` sigue leyendo solo `.contador` y
  sigue funcionando sin tocarlo.
- No se crea ningún endpoint nuevo ni se toca `Icarus.Host`: ambos
  controladores ya tenían la bandeja completa en memoria para pintar su
  propia página; solo se reusa en la respuesta JSON.

### Frontend (un script global en `aplicacion.js` + markup en `_Layout.cshtml`; sin archivos nuevos)

- **Campanita global**: un ícono con badge en la barra superior (`header.barra`
  de `_Layout.cshtml`), visible si el usuario autenticado tiene
  `TieneGestorPedidoAlimento` y/o `TieneGestorRecepcionHuevos`. El badge
  muestra la suma de los contadores que apliquen. Un clic despliega un menú
  con los items combinados de ambas fuentes (orden por `fechaUtc` descendente,
  tope de 5, igual límite que `CampanaNotificaciones.tsx` del PWA), cada uno
  con su texto ya traducido por el servidor y un enlace a su página de
  listado.
- **Sondeo único**: cada 30 s (pausado si `document.hidden`, mismo criterio
  que el sondeo por página existente), un único script en `aplicacion.js`
  hace `fetch` a los endpoints de contador que correspondan según las
  funcionalidades del usuario (uno, otro, o ambos). Ese mismo resultado:
  - actualiza el badge y el desplegable de la campanita, y
  - decide si dispara una `Notification` del navegador (solo si
    `Notification.permission === "granted"`).

  No hay doble fetch: una sola llamada por endpoint alimenta los dos canales.
- **Detección de novedad para la `Notification`**: se compara el `contador`
  recibido contra el último valor visto, guardado en `localStorage` con una
  clave que incluye el correo de la sesión (`data-correo` ya disponible en
  `barra__cuenta`), para no mezclar el estado de dos cuentas distintas que
  compartan navegador/máquina de oficina. La primera corrida para una cuenta
  solo fija la base — no dispara notificación por el backlog que ya existía
  antes de este cambio.
- **Banner de permiso**: se renderiza oculto por CSS en `_Layout.cshtml`,
  gateado por las mismas comprobaciones de `ReclamosCaisy`. Al cargar, un
  script comprueba `Notification.permission`: si ya es `"granted"` o
  `"denied"`, el banner nunca se muestra. Si es `"default"`, se muestra con un
  botón «Activar notificaciones». El clic dispara
  `Notification.requestPermission()`; sea cual sea el resultado, el banner se
  oculta para siempre vía una bandera en `localStorage` — nunca vuelve a
  insistir solo (coherente con que el navegador tampoco deja volver a
  preguntar una vez negado).
- **Disparo de la `Notification`**: título genérico por dominio («Tienes
  nuevas novedades en Pedidos de alimento» / «... en Recepción de huevo»),
  sin precios, montos ni datos nominales — cumple la regla anti-PII del
  proyecto igual que el resto de la interfaz. `tag: 'pedidos'` o
  `'recepciones-huevo'` para que notificaciones repetidas del mismo dominio se
  reemplacen en vez de apilarse.
- **Clic**: tanto en un item de la campanita como en la `Notification` del
  navegador, navega a la página de listado del dominio (`/Pedidos` o
  `/RecepcionesHuevo`), nunca al detalle puntual — reutiliza la bandeja que
  ya existe en cada página, incluida la acción «Marcar como leída» que ya
  tienen. Este spec no agrega marcar-como-leída desde la campanita ni desde
  la notificación.

## Contrato de datos

`GET Pedidos/Notificaciones/Contador` (y su equivalente de
`RecepcionesHuevo`) pasa de:

```json
{ "contador": 3 }
```

a:

```json
{
  "contador": 3,
  "items": [
    {
      "id": "3f2d...",
      "mensaje": "Devolución",
      "chip": "borrador",
      "fechaUtc": "2026-10-05T14:30:00Z",
      "pedidoId": "9a1c..."
    }
  ]
}
```

`items` es la misma lista que ya se pinta en la página (`Model.Notificaciones.Items`),
mapeada con los mismos helpers `EtiquetaNotificacion`/`EtiquetaChip` ya
extraídos a los modelos. Para `RecepcionesHuevo` el campo de id de referencia
es `despachoHuevoId` en vez de `pedidoId`, igual que ya distingue
`NotificacionDespachoHuevoApi` de `NotificacionPedidoApi`.

## Fuera de alcance

- Web Push real (Service Worker + suscripción VAPID) para notificar con el
  navegador completamente cerrado. Explícitamente descartado: contradice la
  regla del proyecto de que `Trajano.GestorCaisy` no tiene Service Worker, y
  el público cerrado de este spec no lo justifica (ver discusión previa sobre
  el enforcement de Chrome con audiencias pequeñas y conocidas).
- Marcar como leída desde la campanita o desde la `Notification`; esa acción
  sigue viviendo solo en `/Pedidos` y `/RecepcionesHuevo`, sin cambios.
- Reintroducir el tipo `CreditoInsuficiente` ni ningún tipo nuevo de
  notificación de huevo: la bandeja de `RecepcionesHuevo` sigue mostrando lo
  que ya emite hoy (que puede ser nada).
- Navegación de la `Notification`/campanita al detalle puntual de un pedido o
  despacho; ambas siempre llevan a la página de listado.
- Cambios en el PWA (`web/`) o en la API de `Icarus.Host`: todo el trabajo es
  dentro de `Trajano.GestorCaisy`.
- Persistir la preferencia de notificación en el servidor; el estado del
  banner y el último contador visto viven solo en `localStorage` del
  navegador del gestor.
- Pruebas automatizadas del comportamiento real de `Notification` o del
  flujo de permiso: el proyecto no tiene arnés de pruebas para JS puro. Se
  cubre con pruebas de integración de la superficie server-rendered (markup,
  atributos de datos, contrato JSON enriquecido) y verificación manual en
  navegador para el resto.

## Criterios de aceptación

1. Un gestor con `GestorPedidoAlimento` ve la campanita con el contador de
   pedidos; uno con `GestorRecepcionHuevos` ve el de recepciones; uno con
   ambas funcionalidades ve la suma y los items combinados; uno sin ninguna
   no ve la campanita ni el banner.
2. El sondeo por página existente (`/Pedidos`, `/RecepcionesHuevo`) sigue
   funcionando sin modificaciones de comportamiento visible.
3. Con el permiso en `"default"`, al entrar aparece el banner; al aceptar o
   rechazar, desaparece y no vuelve a aparecer en sesiones siguientes del
   mismo navegador.
4. Con el permiso concedido, un incremento del contador dispara una
   `Notification` del navegador con texto genérico (sin precios ni montos) y
   el clic navega a la página de listado correspondiente.
5. Sin permiso (negado o nunca pedido), la campanita sigue mostrando el
   badge y el desplegable igual que con permiso concedido — nunca depende de
   la `Notification` para ser útil.
6. Dos cuentas de Gestor CAISY distintas en el mismo navegador no se
   contaminan la una a la otra el estado de «último contador visto».
