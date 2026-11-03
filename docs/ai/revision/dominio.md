# Checklist de revisión de dominio

> Propio de Trajano-Icarus. Complementa al [checklist común](comun.md) en la
> revisión profunda. Otro proyecto escribe el suyo.

## Cuándo la revisión es profunda

Es obligatoria si el diff toca alguna de estas zonas, o si el orquestador la
pide con una nota al avanzar a `REVISION`:

- `quality/`, `verify.ps1`, `verify.sh` y `.github/workflows/`.
- Migraciones: `Icarus/src/*/*.Infrastructure/Migrations/`.
- Identidad y autorización: `Icarus/src/Identity/`, políticas y atributos de
  autorización, entitlement por módulo o funcionalidad, y la sesión de
  `Icarus/src/Apps/Trajano.GestorCaisy/`.
- Aislamiento de tenant: filtros de consulta, `IgnoreQueryFilters`,
  `IClienteActivo` e `ICurrentUser`.
- Control de acceso y biometría: `Icarus/src/ControlAcceso/` y la integración
  con ARGOS.
- Máquinas de estados y reglas de negocio delicadas: pedidos, despachos,
  recepciones, crédito, precios congelados, cupo semanal y vacunación.

## Qué compruebas

- **Tenant:** toda consulta de datos de un cliente pasa por el filtro del
  cliente activo; `IgnoreQueryFilters` solo en cuentas globales y con su motivo.
  El Cliente es el tenant ([decisión 003](../../decisiones/003-multi-tenant-por-filas-y-claims.md)).
- **Autorización:** cada endpoint exige rol y, donde corresponde, módulo o
  funcionalidad. En GestorCaisy, rol `GestorCaisy` más la funcionalidad de la
  sección.
- **Anti-PII:** ningún dato biométrico, documento de identidad, registro
  nominal de acceso de trabajadores, credencial ni token en logs, errores,
  bitácoras o fixtures.
- **Migraciones:** reversibles, seguras con los datos existentes y con la
  concurrencia prevista.
- **Reglas de negocio:** contra el
  [glosario avícola](../../dominio/glosario-avicola.md), sin suponer ninguna.
- **Máquinas de estados:** cada transición contra
  [máquinas de estados](../../proyecto/maquinas-estados.md).
- **Fechas:** fecha de negocio de Bolivia e instantes técnicos en UTC
  ([decisión 005](../../decisiones/005-fechas-de-negocio-bolivia-y-utc.md)).
- **Offline:** solo recogida y mortalidad, con cola idempotente; pedidos y
  despachos online
  ([decisión 006](../../decisiones/006-online-deliberado-pedidos-y-despachos.md)).
- **GestorCaisy:** solo HTTP contra la API, sin DbContext ni SQL; tokens en la
  cookie protegida HttpOnly, nunca en localStorage; sin service worker, caché
  offline ni IndexedDB.
- **Errores:** excepciones de dominio
  ([decisión 002](../../decisiones/002-errores-por-excepciones-de-dominio.md)).
