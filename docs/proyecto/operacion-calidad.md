# Operación y calidad

## 1. Configuración por entorno

La configuración se resuelve con `appsettings`, variables de entorno y secretos
del despliegue. Nunca se copian valores reales al repositorio ni a logs.

| Área | Claves funcionales, sin valores |
|---|---|
| SQL | `ConnectionStrings:Icarus` |
| JWT | `Jwt:Emisor`, `Audiencia`, `Clave`, minutos access y días refresh |
| Migraciones | `Migraciones:EjecutarAlArranque` |
| Pedidos | `PedidosAlimento:MaximoPorSemana` |
| Documentos | `AlmacenDocumentosPedido:Ruta`, tamaño y dimensiones máximas |
| GestorCaisy | `ApiIcarus:BaseUrl` |
| Kiosco | `ControlAcceso:Kiosco:*`, orígenes, vigencia y rate limit |
| Biometría | `ControlAcceso:Biometria:*`, `ArgosControlAcceso:*` |
| Plantillas | clave de cifrado fuera de SQL/git |
| Serilog | sinks nombrados, URL/API key de Seq y `Release` |
| Bootstrap | `SeedSettings:*` para cuentas globales iniciales |

La plantilla pública es `.env.example`; el archivo real `.env` no debe leerse ni
documentarse. Development tiene semillas de demostración; Production solo el
bootstrap explícito de cuentas globales.

## 2. Migraciones y cuentas iniciales

En Development/Testing se migran los cuatro contexts y se siembran datos. En
Production, con la bandera de migraciones activa y `SeedSettings` completo, el
bootstrap idempotente alinea exactamente:

- Administrador configurado por secreto;
- Gestor CAISY de recepción de huevos;
- Gestor CAISY de pedidos de alimento.

Si falta un secreto se omite el conjunto, sin altas parciales. Las cuentas
existentes conservan contraseña; rol y funcionalidad se corrigen. No se crean
tenants ni datos avícolas productivos por semilla.

Guía detallada: [bootstrap de cuentas](../operacion/bootstrap-cuentas-produccion.md).

## 3. Observabilidad y anti-PII

### Identificadores

- `ErrorId`: incidente visible para soporte.
- `SessionId`: pestaña frontend.
- `CorrelationId`: petición HTTP.
- `DownstreamCorrelationId`: salto MVC→API.
- `TraceId`: ejecución distribuida ASP.NET.
- `Release`: build/despliegue.

### Eventos

`http.request.completed`, `http.client.send`, `backend.error`,
`backend.business_warning`, `frontend.error`, `frontend.flow`,
`operation.started/decision/completed/rejected/failed`,
`persistence.save_changes.*` y `transaction.*`.

Las mutaciones registrables narran decisiones con campos declarados y no
sensibles. Un evento `operation.decision` no prueba persistencia: hay que llegar
al `SaveChanges`/commit correspondiente. Una compensación lógica no es rollback
físico.

### Regla de privacidad

No registrar credenciales, tokens, cookies, NIT, nombres, documentos, biometría,
registros nominales de acceso, cuerpos, query strings, SQL con parámetros ni
mensajes de excepción que puedan contener entrada. Los logs usan patrones de ruta,
no paths concretos. `backend.error` devuelve/referencia un id y evita stack crudo
en la respuesta.

Guía completa: [observabilidad](../operacion/observabilidad.md) y
[registro de vuelo](../operacion/registro-vuelo.md).

### Seq y consola

Seq es opcional para la funcionalidad: consola JSON compacta es el fallback. La
consola asíncrona tiene cola acotada y no bloquea peticiones; Seq envía por lotes.
No hay entrega exactamente una vez ni buffer durable: una caída prolongada puede
perder eventos. El Seq central tiene autenticación, API keys por aplicación, UI
solo loopback/VPN y retención operativa.

## 4. Documentos privados y respaldo

Los binarios viven fuera del web root en `/app/documentos-pedidos`; SQL guarda
claves UUID, MIME, tamaño, SHA-256 y nombre seguro. Originales y vistas son
inmutables. La vista se sirve inline y el original como adjunto autorizado.

Límites iniciales documentados: 512 KiB por archivo y 8000 px por lado, sujetos a
configuración. Los endpoints también limitan el cuerpo temprano.

El volumen no es un backup. La unidad coherente de restauración es **dump SQL +
volumen de la misma fecha**. Se recomienda copia diaria externa, verificación de
hash y prueba periódica de restauración/descarga. Claves DataProtection y secretos
de cifrado requieren respaldo y procedimiento propios.

Guía: [respaldos de notas](../operacion/respaldos-notas.md).

## 5. Kiosco en operación

El kiosco es online, sin service worker ni IndexedDB. Cookie host-only HttpOnly,
Secure y Strict; sesión con hash en SQL, expiración y revocación. Mutaciones con
`X-Icarus-Kiosco`, `Origin` permitido y `no-store`. La activación tiene rate limit
por IP y salida con reautenticación.

ARGOS se consume solo desde backend por red interna. Las plantillas cifradas se
custodian en Icarus y su clave fuera de SQL/git. El contrato PAD/latencia en tablet
real debe validarse operativamente antes de dar por superada esa verificación de
hardware.

Guía: [control de acceso — kiosco](../operacion/control-acceso-kiosco.md).

## 6. Puerta de calidad

Desde la raíz:

```powershell
./verify.ps1
```

En POSIX: `./verify.sh`. La puerta se detiene en el primer fallo e incluye:

1. tests de scripts `quality/`;
2. adaptadores generados;
3. mojibake;
4. enlaces Markdown relativos;
5. lint, build y tests frontend;
6. build backend con warnings como errores;
7. tests unitarios, arquitectura e integración backend.

Requisitos: .NET 10, Node 22, dependencias `web/` y Docker para
Testcontainers.MsSql. Está prohibido `--no-verify` y relajar baselines o
exclusiones para obtener verde.

Para un cambio exclusivamente documental, mínimo:

```bash
node quality/check-mojibake.mjs
node quality/check-enlaces.mjs
git diff --check
```

La puerta completa sigue recomendada y es obligatoria si se mezcla con código,
build, configuración o tests.

## 7. Pruebas y arquitectura

| Suite | Cobertura |
|---|---|
| `Icarus.UnitTests` | Dominio, Application, seguridad y utilidades. |
| `Icarus.ArchitectureTests` | Dependencias, fronteras modulares y prohibición SQL en MVC. |
| `Icarus.IntegrationTests` | API + SQL real con Testcontainers y contratos HTTP. |
| `Trajano.GestorCaisy.Tests` | MVC, cookie, cliente API, autorización y privacidad. |
| `web` Vitest | Componentes, rutas, HTTP, offline y utilidades. |
| `quality/__tests__` | Los propios gates. |

Los tests de integración no usan una base simulada: requieren Docker. La regla de
desarrollo es ver primero el test dirigido en rojo y la suite completa al integrar.

## 8. Desarrollo local y artefactos auxiliares

### Dos formas de ejecutar

| Entorno | Composición | Uso |
|---|---|---|
| `docker-compose.dev.yml` | SQL Server + API Development + Seq; Vite se ejecuta aparte con `npm run dev`. | Desarrollo diario del backend/frontend con proxy `/api`. |
| `docker-compose.prodlocal.yml` + `docker-compose.pcN.yml` | SQL + Seq + API/PWA build + GestorCaisy + Caddy HTTPS. | Stack PC1/PC2/PC3 con artefacto similar al desplegado y acceso móvil LAN. |

El stack PC se controla con `iniciar-pc.ps1 -Perfil pc1|pc2|pc3`, sus wrappers
`iniciar-pc1.ps1`, `iniciar-pc2.ps1`, `iniciar-pc3.ps1`, `iniciar-pc1-sin-wifi.ps1`,
`estado-pc.ps1` y `detener-pc.ps1`. El script publica Host/MVC, compila la PWA en
`.local/payload`, reconstruye imágenes, levanta servicios y verifica salud HTTPS.
`ICARUS_LAN_IP` e `ICARUS_LAN_HOST` configuran el gateway; `infra/pcN/Caddyfile`
publica la PWA/API y el subdominio `gestor` con TLS interno.

La recreación de volúmenes exige la doble confirmación `-RecrearDatos
-ConfirmarBorradoDatos`. Es destructiva y no forma parte de una ejecución normal.
El overlay `docker-compose.argos.yml` añade ARGOS real desde un repositorio hermano
para probar el contrato en local.

### Imágenes y consultas

`Dockerfile.web` y `Dockerfile.gestor` son runtime-only: parten de ASP.NET 10,
copian el payload ya publicado, instalan curl para healthcheck y ejecutan como
usuario `app` (uid 1654). No compilan dentro de la imagen de producción.

`consultasPruebasSql/saldo-credito-huevo-cliente-demo.sql` es una utilidad de
auditoría para Development: desglosa ingresos, alimento recibido y ajustes con la
misma fórmula del repositorio. No es una migración ni un proceso productivo. Los
scripts `crear-usuario-*.ps1` crean cuentas CAISY en entornos sin bootstrap.

## 9. CI y despliegue

- `develop` es la rama de trabajo; `master` producción y solo se promueve a
  petición explícita.
- CI ejecuta la puerta.
- Deploy manual exige rama `master`, input `PRODUCCION` y CI verde del SHA.
- Se publican Host y MVC, se compila PWA y se valida el payload.
- Las imágenes Linux pasan smoke antes de sincronizar.
- El despliegue crea releases por SHA, construye y prueba candidatos aislados,
  promueve y comprueba salud local/HTTPS; conserva imagen anterior para rollback.
- Los assets del release anterior se conservan para service workers en transición.
- Los secretos de cuentas CAISY se transfieren mediante archivo temporal 0600 y
  se eliminan.

Workflow: `.github/workflows/deploy.yml`. Scripts: `deploy-produccion.ps1` y
composes raíz. Nada de esto se ejecuta durante una tarea documental sin petición.

## 10. Verificación operativa tras despliegue

- `GET /api/health` responde correctamente a través del reverse proxy.
- La página `/Sesion/Acceder` de GestorCaisy responde.
- Login/refresh y autorización por rol siguen funcionando.
- Un flujo sintético de ARGOS confirma el enlace interno cuando el entorno lo
  contempla.
- Documentos persistentes, DataProtection y volúmenes sobreviven recreación.
- Seq o consola muestran release nuevo sin datos sensibles.
- Kiosco en Android real: cámara, PAD, latencia y recuperación de sesión son una
  verificación de hardware separada del smoke HTTP.
