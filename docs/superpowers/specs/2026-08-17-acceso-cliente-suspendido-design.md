# Acceso de clientes suspendidos

## Problema

Suspender un cliente actualiza `clientes.EstaActivo`, pero las cuentas asociadas
siguen pudiendo iniciar sesión, renovar refresh tokens y usar tokens ya emitidos.

## Decisiones

- El estado activo del cliente se consulta en la base de datos, nunca se copia
  al JWT.
- El login y la renovación rechazan cuentas con `ClienteId` cuyo cliente esté
  suspendido, con el mismo error genérico que unas credenciales inválidas.
- Un access token emitido antes de la suspensión queda bloqueado por middleware
  antes de `/identidad/me` y de cualquier endpoint autenticado de cliente.
- Las cuentas de plataforma sin `ClienteId` no se ven afectadas.
- La prueba de extremo a extremo usa la `IdentityFactory`, que levanta SQL
  Server con Testcontainers y ejecuta migraciones y datos reales.

## Fuera de alcance

- No se cambia la expiración ni la revocación individual de JWT.
- No se registran correos, tokens, documentos ni datos nominales.
