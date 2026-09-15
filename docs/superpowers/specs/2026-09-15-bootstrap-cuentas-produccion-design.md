# Bootstrap de cuentas mínimas de producción

## Contexto

Las semillas de clientes, trabajadores, granjas, galpones y escenarios
avícolas son exclusivas de `Development` y `Testing`. En producción la API
aplica las migraciones de esquema de manera opt-in y hoy solo puede crear el
administrador inicial. Faltan las dos cuentas globales de CAISY necesarias para
operar la oficina.

## Decisión

Al arrancar en producción con `Migraciones:EjecutarAlArranque=true`, la API
ejecutará un bootstrap idempotente de exactamente tres cuentas globales:

| Cuenta | Rol | Funcionalidad |
|---|---|---|
| Administrador configurado | `Administrador` | Ninguna |
| `grh@icarus.online` | `GestorCaisy` | `GestorRecepcionHuevos` |
| `gpa@icarus.online` | `GestorCaisy` | `GestorPedidoAlimento` |

Los correos de CAISY son constantes del bootstrap; las tres contraseñas y el
correo del administrador llegan exclusivamente por `SeedSettings` mediante
variables de entorno o el almacén de secretos del despliegue. La ejecución se
omite íntegramente si falta cualquiera de esos cuatro secretos: no se crea un
conjunto parcial de cuentas.

Si las cuentas ya existen, el bootstrap corrige su rol y funcionalidades al
estado definido, pero no cambia sus contraseñas. Nunca crea ni elimina clientes,
trabajadores, granjas, galpones, pedidos, publicaciones, documentos ni otros
datos de dominio.

## Seguridad y operación

- Las contraseñas no se escriben en archivos versionados ni en logs.
- Los logs solo indican el tipo de cuenta procesada y códigos técnicos de
  errores de Identity; no incluyen correos ni credenciales.
- La operación es idempotente y se limita al arranque productivo opt-in.
- No es una migración EF de datos: las migraciones de esquema permanecen
  separadas de las cuentas iniciales, cuyo hash requiere `UserManager`.

## Fuera de alcance

- Borrar o alterar datos ya existentes en una base productiva.
- Cambiar las semillas de `Development` o `Testing`.
- Versionar secretos o contraseñas por defecto.
