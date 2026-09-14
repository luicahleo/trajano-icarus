# Badge de novedades sin recargar la página

Diseño validado con el usuario el 2026-09-14. Hoy las novedades de la sección
de comunicación solo aparecen si el usuario presiona F5. El objetivo es que un
badge se actualice solo, en la PWA del tenant y en la aplicación MVC de CAISY.

## Lo que ya existe

La mitad del trabajo está hecha y nadie la usa. El endpoint de notificaciones
(`PedidosAlimentoEndpoints.cs:239`, replicado por `MapNotificaciones` en el
grupo del tenant y en el de CAISY) ya calcula un ETag sobre la bandeja
completa y responde **304 Not Modified** cuando nada cambió.

Es decir: el backend se construyó para ser sondeado. Falta que alguien lo
sondee.

- La PWA consulta la query una sola vez al montar, sin `refetchInterval`
  (`PedidosAlimentoPage.tsx:90` y `DespachosHuevoPage.tsx:82`).
- El MVC arma las notificaciones al renderizar la vista y su
  `wwwroot/js/aplicacion.js` no tiene `setInterval`, `EventSource` ni SignalR.

## Mecanismo: sondeo con ETag

Se eligió sondeo sobre SSE y SignalR. La razón principal es que el 304 ya está
implementado y probado: el sondeo aprovecha infraestructura existente en vez
de agregar una capa nueva.

Las alternativas se descartaron por costo, no por capacidad:

- **SSE** da latencia inmediata sin librerías, pero exige una conexión HTTP
  abierta por usuario, es sensible al buffering del gateway y obliga a manejar
  la reconexión a mano — delicado en una PWA que se desconecta a propósito.
- **SignalR** es push real y bidireccional, pero suma una dependencia, un
  cliente JS en los dos frontends y un backplane si algún día hay más de una
  instancia. Demasiada maquinaria para pintar un badge.

**Intervalo: 30 segundos.** Una novedad de pedido o despacho no es urgente al
segundo; media hora sí sería demasiado. Con el 304, un sondeo sin novedades
cuesta una respuesta vacía.

## Qué se actualiza

Badge **y** lista. Cuando el ETag cambia, además del contador se recarga la
bandeja, para que el registro que acaba de cambiar de estado aparezca solo.
Esa es la expectativa de quien ve un badge: que la pantalla ya esté al día.

La consulta extra de la bandeja se paga **solo cuando el ETag cambió**, no en
cada sondeo.

## PWA

En las dos queries de notificaciones:

- `refetchInterval: 30000`
- `refetchIntervalInBackground: false` — sin esto, una pestaña oculta sigue
  sondeando indefinidamente.

Cuando llega contenido nuevo, se invalida la query de la bandeja
correspondiente.

**El sondeo se apaga sin conexión.** El `QueryClient` usa
`networkMode: 'offlineFirst'` (`web/src/app/queryClient.ts:13`), elegido para
que las lecturas caigan a IndexedDB en vez de quedar pausadas. Ese mismo
ajuste hace que un `refetchInterval` siga intentando la red estando offline y
acumule fallos cada treinta segundos. El sondeo debe condicionarse al estado
de conexión.

## MVC

Un endpoint JSON nuevo en el controlador que devuelve **solo el contador**, y
un `setInterval` en `aplicacion.js` que actualiza el badge. El renderizado
server-side actual no cambia: la página sigue pintando las notificaciones al
cargar, y el badge se limita a avisar que hay algo nuevo.

No se agrega service worker, caché offline ni IndexedDB al MVC: la aplicación
de oficina es deliberadamente online.

## El detalle que decide si esto sale barato o caro

El ahorro del 304 depende de que el navegador reenvíe `If-None-Match`, y hoy
el endpoint **no envía `Cache-Control`**. Sin esa cabecera el comportamiento
queda a criterio del navegador, con dos finales malos:

1. Sirve del caché sin revalidar, y el badge no cambia nunca.
2. Ignora el ETag, y cada sondeo paga la respuesta completa.

El endpoint debe responder `Cache-Control: no-cache`, que no significa «no
guardar» sino «guardar, pero revalidar siempre». Es justo lo que hace falta
para que el 304 funcione.

Hay que verificar además que el service worker de la PWA no intercepte la ruta
de notificaciones con una estrategia de caché: si la sirve él, la revalidación
nunca llega al servidor.

## Alcance

**Entra:** el `Cache-Control` en el endpoint compartido; el sondeo en las dos
páginas de la PWA con su apagado sin conexión; el endpoint de contador y el
badge del MVC.

**No entra:** notificaciones push del navegador; sondeo en pantallas que no
sean las dos bandejas; cambiar el cálculo del ETag o el modelo de
notificaciones; ninguna forma de push real.

## Riesgo asumido

El badge puede tardar hasta treinta segundos en aparecer. Es aceptable para
este dominio y es el precio de no mantener conexiones persistentes. Si alguna
vez se necesita latencia inmediata, SSE se puede agregar después sobre el
mismo endpoint sin descartar este trabajo: el sondeo queda como respaldo
cuando la conexión persistente no está disponible.
