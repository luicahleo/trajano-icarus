# El crédito de huevo nace al recibir — los catorce días son referencia, no corte

Diseño validado con el usuario el 2026-09-14, como **segunda corrección** del
bloque 9. Se apoya en
`2026-09-14-credito-huevo-privado-del-cliente-design.md` y no lo contradice:
aquel documento definió *quién* ve el saldo y que el saldo no valida nada;
este define *cuándo* el huevo entregado se convierte en saldo.

## Por qué existe este documento

El bloque 9 se construyó con una tercera premisa que también resultó
equivocada:

> **«El crédito está disponible recién catorce días después de la
> recepción.»**

Es falsa. El usuario lo corrigió al revisar el caso 1 de las pruebas
manuales:

> *«realmente el credito si entra en vigor al momento de recepcionar CAISY,
> los 14 dias son una fecha corte de referencia, no siempre son 14 dias,
> pero es lo ideal, que podemos hacer en este caso?, porque esos 14 dias no
> es algo rigido»*

El crédito nace cuando CAISY recibe el huevo. Los catorce días describen el
ritmo habitual con que CAISY liquida, que en la práctica varía. Nunca fueron
una condición para que el dinero exista.

## El defecto concreto

`RepositorioBalanceCreditoHuevo.ObtenerSaldoDisponibleAsync` filtra los
ingresos con `FechaRecepcion <= hoy - 14 días`. Consecuencia: **el saldo
oculta dinero que ya es del cliente**.

En la semilla de desarrollo se ve vivo. El tenant `ConMovimiento`
(`c3@icarus.test`) tiene un despacho recibido hace cinco días por **2088,00**
que no aparece en su saldo. La PWA le muestra 6885,00 cuando en realidad
tiene 8973,00.

El defecto no se limita a una cifra baja: es una explicación que el cliente no
puede reconstruir. Ve un despacho confirmado como recibido y no lo encuentra
en su saldo, sin ningún elemento en pantalla que le diga por qué.

Agrava el caso que, por la corrección anterior, **el saldo no valida nada**:
es información pura para el Cliente. Un recorte que no protege ninguna regla
solo produce una cifra incorrecta.

## Decisión

| Pregunta | Decisión |
|---|---|
| ¿El corte de catorce días sigue restando del saldo? | **No.** El saldo suma todo el huevo recibido, sin esperar. |
| ¿Los catorce días desaparecen del producto? | No. Se conservan como **referencia informativa**. |
| ¿Cómo se muestra esa referencia? | Junto al saldo, una línea secundaria: cuánto de ese monto se recibió en los últimos catorce días. |
| ¿Quién la ve? | Solo el Cliente, igual que el saldo. Sin cambios de visibilidad. |
| ¿Cambia algo para el Trabajador o para CAISY? | Nada. Siguen sin ver el crédito. |
| ¿El número de días sigue siendo 14? | Sí, pero deja de llamarse «disponibilidad»: es referencia. |

Opción elegida por el usuario entre tres: *«Quitar el corte, dejar referencia
visible»*, con esta vista previa aprobada:

```
Crédito por despachos de huevo: 9.990,00
  de los cuales 1.530,00 se recibieron
  en los últimos 14 días
```

Se descartó «quitar el corte sin más» porque el ritmo de liquidación de CAISY
es información que al cliente le sirve para decidir cuánto pedir, aunque no
condicione nada. Se descartó «dejarlo como está» porque el número mostrado
sería incorrecto.

## La fórmula

Antes:

```
saldo = ingresos(recibidos hace más de 14 días) - recibidoReal + ajustes
```

Después:

```
saldo            = ingresos(todo lo recibido) - recibidoReal + ajustes
recibidoReciente = ingresos(recibidos en los últimos 14 días)
```

`recibidoReciente` es un dato **derivado y adicional**, no un término de la
resta. Sale de la misma tabla que `ingresos` con el filtro de fecha invertido,
y por construcción `0 <= recibidoReciente <= ingresos`.

Los otros dos términos no se tocan y la asimetría es deliberada: la deuda por
alimento descuenta desde que el alimento llega, y los ajustes compensan algo
que ya ocurrió. Solo los ingresos tenían un plazo, y ese plazo era el error.

## Efecto en los saldos de la semilla

| Tenant | Saldo antes | Saldo después | Reciente |
|---|---|---|---|
| `Holgado` (`cliente@`) | 9990 | **9990** | 0 |
| `Negativo` (`c1@`) | −5985 | **−5985** | 0 |
| `Insuficiente` (`c2@`) | 3915 | **3915** | 0 |
| `ConMovimiento` (`c3@`) | 6885 | **8973** | **2088** |
| `Vacio` (`c4@`) | 0 | **0** | 0 |

Solo cambia `ConMovimiento`, que es justamente el tenant que la semilla creó
para tener un despacho dentro de la ventana. Ese tenant pasa a ser el
escenario de prueba de la línea de referencia: es el único donde la segunda
línea se renderiza.

Que `Insuficiente` no se mueva importa: su marca «Enviado con crédito
insuficiente.» sigue siendo verdad (3915 < 4500), así que el test que verifica
que la marca no miente se mantiene en verde sin tocarlo.

## Nombres

`ReglasCreditoHuevo.DiasDisponibilidadCredito` pasa a llamarse
`DiasReferenciaCredito`. No es cosmética: el nombre viejo afirma en el código
una regla que el negocio acaba de negar, y un nombre que miente sobrevive a
cualquier comentario que lo corrija.

El valor sigue siendo 14 y sigue siendo una constante, no configuración. El
usuario dijo que «no siempre son 14 días, pero es lo ideal»: es una
referencia orientativa, no un parámetro que alguien deba ajustar por cliente.
Si algún día hace falta por tenant, será su propia decisión de diseño.

## Alcance

**Entra:**

- El cálculo del saldo deja de filtrar los ingresos por fecha.
- Un dato nuevo, `RecibidoReciente`, viaja al Cliente junto al saldo.
- Los dos formularios donde el Cliente ve su crédito muestran la línea de
  referencia cuando ese monto es mayor que cero.
- El script de auditoría `consultasPruebasSql/` refleja la fórmula nueva.

**No entra:**

- Visibilidad: nadie gana ni pierde acceso al crédito.
- Validaciones: el saldo sigue sin bloquear, advertir ni notificar nada.
- El camino de CAISY (`ObtenerCreditoHuevoDePedidoCaisyHandler`), inerte
  desde la corrección anterior. Consume el saldo y por eso hereda la fórmula
  nueva, pero no se modifica ni se reactiva.
- Configurar los días por cliente.

## Deuda registrada

`CreditoHuevoDePedidoCaisy.SaldoSinEstePedido` sigue calculándose como
`saldo + monto`, coherente con una fórmula que ya no descuenta los pedidos en
tránsito — herencia de la corrección anterior, no de esta. Sigue inerte
detrás del gate de rol `Cliente`, así que nadie lo lee. Queda anotado acá
para que quien lo reviva sepa que su aritmética quedó desactualizada dos
veces.
