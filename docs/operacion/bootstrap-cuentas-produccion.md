# Bootstrap de cuentas mínimas de producción

En producción la API aplica las migraciones de esquema solo si el despliegue
activa `Migraciones__EjecutarAlArranque=true`. Con esa bandera y `SeedSettings`
completo, al arrancar se ejecuta un bootstrap idempotente de exactamente tres
cuentas globales:

| Cuenta | Rol | Funcionalidad |
|---|---|---|
| Correo en `SeedSettings__AdminEmail` | `Administrador` | Ninguna |
| `grh@icarus.online` | `GestorCaisy` | `GestorRecepcionHuevos` |
| `gpa@icarus.online` | `GestorCaisy` | `GestorPedidoAlimento` |

Los correos de CAISY son constantes del código; el resto de los datos llegan
solo por secretos del despliegue. Las semillas de clientes, trabajadores,
granjas, galpones, pedidos y escenarios avícolas siguen siendo exclusivas de
`Development` y `Testing` y **no** corren en producción.

## Secretos

Cuatro variables (variables de entorno o almacén de secretos del despliegue),
sin valores por defecto ni en git:

| Variable | Contenido |
|---|---|
| `SeedSettings__AdminEmail` | Correo del administrador de plataforma |
| `SeedSettings__AdminPassword` | Contraseña del administrador |
| `SeedSettings__GestorRecepcionHuevosPassword` | Contraseña de `grh@icarus.online` |
| `SeedSettings__GestorPedidoAlimentoPassword` | Contraseña de `gpa@icarus.online` |

El `docker-compose.yml` de producción recibe estas variables por `env_file:
.env`; la plantilla está en `.env.example`. Nunca se registran en logs ni se
escriben en archivos versionados.

## Comportamiento

- **Falta un secreto**: la ejecución se omite íntegramente. No se crea un
  conjunto parcial de cuentas.
- **Cuenta nueva**: se crea con su rol y funcionalidad exactos y la contraseña
  configurada.
- **Cuenta existente**: se corrige rol y funcionalidad al estado definido, sin
  cambiar su contraseña.
- **Idempotencia**: repetir el arranque no duplica cuentas ni reescribe las que
  ya están alineadas.
- **Alcance**: solo toca estas tres cuentas. Nunca crea ni borra clientes,
  trabajadores, granjas, galpones, pedidos, publicaciones ni documentos.

Los logs solo indican el tipo de cuenta procesada (`administrador`,
`gestor-recepcion-huevos`, `gestor-pedido-alimento`) y códigos técnicos de
Identity en caso de error; jamás correos ni credenciales.

## Cuentas de CAISY fuera de producción

Los scripts `crear-usuario-caisy.ps1` y
`crear-usuario-gestor-recepcion-huevos.ps1` siguen siendo la vía para entornos
donde el bootstrap productivo no corre (por ejemplo, un entorno sin
`Migraciones__EjecutarAlArranque`). En la VPS, con el bootstrap activo, ya no
hace falta ejecutarlos.
