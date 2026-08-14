# Entitlement por funcionalidad y roles simplificados — Diseño

Fecha: 2026-08-14
Estado: aprobado en brainstorming (sesión de la misma fecha)

## Contexto

El plan 3 dejó el módulo Clientes con entitlement a nivel de **módulo**: el
`Administrador` habilita módulos al cliente y un endpoint de negocio exige que el
cliente tenga ese módulo. La revisión del dominio reveló que eso es insuficiente:
dentro de un módulo hay **funcionalidades** y el cliente quiere repartir a sus
trabajadores *algunas*, no todas. A la vez se simplifican los roles y el alta de
cuentas.

Este spec corrige el modelo para ajustarlo a la realidad del negocio. Es un
cambio de modelo de dominio que toca Identity y Clientes; por eso tiene spec
antes de código.

## Decisiones tomadas en el brainstorming

1. **Tres roles, no cuatro.** Se elimina `SoporteTecnico`. Quedan
   `Administrador`, `Cliente` y `Trabajador`. El alta de cuentas por rol
   arbitraría, pero con 3 roles la cuenta nace del caso de uso que la necesita
   (ver 4 y 5), no de un CRUD de usuarios.
2. **Entitlement en dos niveles.**
   - Nivel **módulo** (contractual): el `Administrador` habilita módulos al
     `Cliente`. Un módulo es un **conjunto de funcionalidades**.
   - Nivel **funcionalidad** (operativo): el `Cliente` reparte a cada
     `Trabajador` las funcionalidades que quiera, **solo de módulos que su
     cliente tenga habilitados**.
3. **El rol `Cliente` tiene todas las funcionalidades de sus módulos.** No se le
   asigna granularidad: el dueño de la empresa entra a todo lo que su cliente
   posee. La granularidad aplica solo a `Trabajador`.
4. **El `Administrador` solo gestiona clientes.** CRUD de clientes y asignación
   de módulos. Nada de trabajadores ni de cuentas. Su cuenta sale de seed
   (sistema cerrado).
5. **El `Cliente` crea las cuentas de sus trabajadores.** No existe más el CRUD
   de usuarios del Administrador (`POST /identidad/usuarios`). El alta del
   cliente incluye su propia cuenta (rol `Cliente`): `POST /clientes` recibe
   `email` y `contrasena`.
6. **El `Trabajador` solo opera.** No crea nada: ni clientes, ni trabajadores,
   ni cuentas. Accede únicamente a las funcionalidades que su cliente le asignó.
7. **Aislamiento de módulos se mantiene.** Clientes no referencia a Identity y
   viceversa. La orquestación de creación de cuentas (cliente y trabajador) vive
   en el Host: valida el dominio en Clientes y luego registra la cuenta vía
   Identity.
8. **Un solo módulo concreto por ahora: `GestionAvicola`** con 8 funcionalidades
   (granjas, galpones, producción de huevos, mortalidad, vacunación, alimentación,
   despachos, precios — glosario). `ControlAcceso` queda declarado como
   **previsto**, sin funcionalidades ni endpoints, para no volver a tocar el enum
   ni la migración cuando se implemente.

## Modelo de dominio

### Roles (Identity)

`Rol` queda: `Administrador`, `Cliente`, `Trabajador`. `ReglasRol.RequiereCliente`
aplica a `Cliente` y `Trabajador` (ambos operan sobre una empresa).

### Funcionalidades (Clientes)

Enum `Funcionalidades` con flags, una por funcionalidad de negocio, con sus
valores numéricos estables (se persisten como entero). Relación módulo →
funcionalidades: declarativa en código (un `Modulos` sigue siendo el contrato del
cliente; cada funcionalidad pertenece a exactamente un módulo).

Se elimina el enum `Modulos` como flags del cliente: el cliente pasa a poseer
**módulos** (la asignación contractual) y los trabajadores poseen
**funcionalidades**. `ControlAcceso` se declara previsto, sin funcionalidades
hoy.

### Cliente

Igual que hoy (razón social, identificador fiscal, activo/suspendido) más los
módulos habilitados. La asignación de módulos la hace el `Administrador`
(`DefinirModulosClienteCommand`, solo Administrador). Al crearlo se crea también
su cuenta de rol `Cliente` (ver creación de cuentas).

### Trabajador

Gana el conjunto de funcionalidades asignadas. Reglas de dominio:
- Solo puede tener funcionalidades de módulos que su `ClienteId` tenga
  habilitados.
- Se le puede quitar cualquier funcionalidad (reasignación).
- El cese o desactivación no libera las funcionalidades (trazabilidad, igual que
  el soft delete): al reactivarse conserva su configuración.

## Creación de cuentas

| Cuenta | Quién la crea | Cuándo |
|---|---|---|
| `Administrador` | Seed | Bootstrap (dev/test) |
| `Cliente` | Host al ejecutar `CrearCliente` | En el alta del cliente (recibe `email` y `contrasena`) |
| `Trabajador` | Host al ejecutar `CrearTrabajador` | En el alta del trabajador (recibe `email` y `contrasena`) |

Orquestación en el Host:
1. `CrearClienteHandler` crea el cliente → se obtiene `clienteId`.
2. El Host registra la cuenta rol `Cliente` con ese `clienteId` vía Identity
   (`IRegistradorUsuarios`), dentro de la misma operación (transacción lógica:
   si falla la cuenta, no queda cliente huérfano).

Análogo para trabajadores: `CrearTrabajadorHandler` crea el trabajador → el Host
registra la cuenta rol `Trabajador` con `clienteId` y `trabajadorId`. Solo el rol
`Cliente` puede crear trabajadores (sobre su propia empresa, tenant).

## Autorización

Tres capas, como hoy, pero el entitlement se recalcula por rol:
- **Rol** (`SoloAdministrador`, `GestionTrabajadores`): la política de
  `GestionTrabajadores` pasa a ser **solo Cliente** (sobre su empresa); el
  `Administrador` queda fuera de trabajadores.
- **Tenant**: filtros globales de EF Core con `ICurrentUser` (igual que hoy).
- **Entitlement** (`VerificadorEntitlement`): ahora distingue por rol —
  - rol `Cliente`: tiene todas las funcionalidades de los módulos de su cliente;
  - rol `Trabajador`: tiene solo sus funcionalidades asignadas.
  Un endpoint de negocio exigirá que el usuario tenga la **funcionalidad**
  correspondiente.

## Fuera de alcance

- Módulo `ControlAcceso` (funcionalidades, endpoints).
- Endpoints de los módulos de negocio de `GestionAvicola` (granjas, galpones,
  producción, mortalidad, vacunación, alimentación, despachos, precios): las
  funcionalidades existen como catálogo, pero ningún endpoint las exige todavía
  salvo el sondeo de entitlement existente.
- Gestión de cuentas: listar, editar, desactivar o revocar cuentas (no existen
  hoy; no se crean en este cambio salvo el alta embebida).
- Registro público de usuarios, login social.

## Impacto sobre el código actual

- Identity: eliminar `SoporteTecnico` de `Rol.cs` y su semilla/tests; eliminar
  el paquete `Usuarios/` (Command, Validator, Handler, `IRegistradorUsuarios` +
  `RegistradorUsuarios` se reubica como servicio interno del Host o se conserva
  para el alta embebida) y `POST /identidad/usuarios`.
- Clientes: nuevo enum `Funcionalidades`; `Trabajador` gana funcionalidades;
  `Cliente.ModulosHabilitados` se reinterpreta como módulos (contractual);
  `CrearClienteCommand` y `CrearTrabajadorCommand` reciben `email`/`contrasena`.
- Host: orquestar el alta de cuentas embebida (cliente y trabajador);
  `PoliticasAutorizacion.GestionTrabajadores` = solo rol `Cliente`.
- `VerificadorEntitlement`: recalcular por rol (Cliente vs Trabajador).
- Migraciones: renumerar/ajustar valores de `Rol` y persistencia de
  `Funcionalidades` en `Trabajador` (valores estables, revisar datos existentes
  en dev/test).
- Tests: actualizar `ReglasRolTests`, `CrearUsuarioHandlerTests`,
  `IdentityEndpointsTests`, `ClientesEndpointsTests`, `EntitlementTests` y los de
  arquitectura si aplica.
