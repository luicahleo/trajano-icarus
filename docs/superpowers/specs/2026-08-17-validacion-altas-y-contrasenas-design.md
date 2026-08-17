# Validación de altas y contraseñas visibles — diseño

## Objetivo

Evitar persistencias cuando un alta de cliente o trabajador tenga errores de
validación, mostrar el campo y el motivo en la interfaz, y mejorar la captura
de contraseñas con confirmación y visibilidad opcional.

## Decisiones

- El producto se configura para Bolivia: la etiqueta de cliente será `NIT`.
- Hasta integrar el servicio oficial `verificaNit` del SIN, el NIT se valida de
  forma local: valor no vacío, numérico y de hasta 15 dígitos. La unicidad se
  comprueba contra la base de datos. Verificar que exista en el padrón queda
  como TODO explícito, no se simula con una regla inventada.
- El backend sigue siendo la autoridad: el agregado y el validador del comando
  aplican la misma regla local. Un `ValidationException` ocurre antes de que
  el handler guarde el cliente o trabajador y antes de crear su cuenta.
- Los `ProblemDetails` de validación se transportan al frontend solo como
  mensajes de campos emitidos por el servidor; no se exponen cuerpos de
  peticiones, NIT, documentos, credenciales, tokens ni otros datos de entrada.
- Cliente y trabajador añaden `confirmacionContrasena`, que se compara solo en
  el navegador y no viaja a la API. Login conserva un único campo porque no
  crea ni cambia contraseñas.
- Los tres formularios con contraseña tendrán un botón accesible para alternar
  entre ocultarla y mostrarla.

## Fuera de alcance

- Consultar o almacenar la validez fiscal real en el SIN.
- Cambiar el esquema físico de `IdentificadorFiscal`; el contrato interno se
  conserva y la etiqueta de la interfaz pasa a `NIT`.
- Cambio de contraseña o recuperación de cuenta.
