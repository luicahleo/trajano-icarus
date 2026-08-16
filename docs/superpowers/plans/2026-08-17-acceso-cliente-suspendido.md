# Plan: bloquear acceso de clientes suspendidos

[x] Añadir regresiones de integración para login, renovación y access token
   emitido antes de suspender. Prueba roja esperada: login y renovación devuelven
   `200`, y el token antiguo llega a `/identidad/me`.
[x] Introducir una consulta compartida de estado activo y conectarla al login y
   a la renovación.
[x] Añadir middleware de request para bloquear tokens existentes asociados a un
   cliente suspendido.
[x] Ejecutar tests dirigidos, la suite completa y `./verify.ps1`.
[x] Revisar diff y cerrar el plan si todo queda verde.

Commit previsto: `fix(auth): bloquea acceso de clientes suspendidos`.
