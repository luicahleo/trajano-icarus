# Plan: validación de altas y contraseñas

1. Añadir pruebas de integración rojas para NIT no numérico y para alta de
   trabajador inválida, comprobando que las listas no cambian. Verificar con
   `dotnet test Icarus/tests/Icarus.IntegrationTests --filter ...`.
2. Implementar la regla local de NIT en dominio y aplicación; conservar el
   TODO de `verificaNit`. Verificar con pruebas unitarias y de integración.
3. Transportar errores de validación por campo de forma segura y mostrarlos en
   cliente/trabajador. Añadir pruebas rojas y ejecutar las pruebas web dirigidas.
4. Añadir confirmación y alternador visible/oculto de contraseña en login,
   cliente y trabajador; confirmar que la confirmación no se manda a la API.
5. Ejecutar `./verify.ps1`, revisar el diff y actualizar este plan antes del
   commit.
