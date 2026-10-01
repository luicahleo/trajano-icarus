# Control de acceso — contrato v2 con ARGOS (A0)

Fecha: 2026-09-30. Responsable: agenteLocal de Trajano-Icarus. Estado: especificación
para implementación; depende de respuestas del agenteVPS y de ensayos en tablet real.

Fuentes consultadas:

- Trajano-Icarus: `AGENTS.md`, `docs/ai/HANDOFF.md`,
  `docs/superpowers/specs/2026-09-25-control-acceso-fase1-design.md`,
  `docs/superpowers/specs/2026-09-29-control-acceso-incidencias-design.md`,
  `docs/superpowers/plans/2026-09-25-control-acceso-fase1.md`,
  `docs/superpowers/plans/2026-09-29-control-acceso-incidencias.md` y
  `docs/operacion/control-acceso-kiosco.md`.
- ARGOS local: `C:/Users/lrcahuana/source/repos/dev/ARGOS`, rama `develop`, commit
  `80ef1cf`, árbol limpio.
- Correspondencia con agenteVPS:
  `C:/Users/lrcahuana/source/repos/dev/DocumentacionProyectos/preguntasrespuestasCaseritoApp_AgenteLocal_AgenteVPS`
  (docs 15, 26, 28, 32, 34, 45, 48, 49, 50 y 52).

## Alcance

Este documento concreta el contrato v2 entre Trajano-Icarus (Control de acceso) y
ARGOS para que el ejecutor de ARGOS pueda implementar `A0` y el ejecutor de
Trajano-Icarus pueda construir el adaptador real `T6`. No incluye implementación de
código ni despliegue en esta sesión. T13 (piloto/tablet física) sigue bloqueado por
A0 y por la disponibilidad del hardware.

## Decisiones ya tomadas y reafirmadas

- Trajano-Icarus custodia las plantillas faciales cifradas; ARGOS solo procesa cada
  petición, sin base de datos, perfiles persistentes ni caché biométrica entre
  peticiones en el nuevo flujo.
- El nuevo flujo no consulta ICARUS legacy ni usa el cliente HTTP saliente de ARGOS
  (`ARGOS/api_client.py`) para este módulo.
- Enrolamiento desde el dispositivo del cliente; marcación desde una única tablet
  Android dedicada.
- PAD pasiva sobre una foto tomada tras pulsar «Iniciar captura» y mostrar 3–2–1;
  sin gestos.
- Identificación entre candidatos del cliente enviados por el backend. Objetivo
  visible de 5 segundos desde la foto; a los 10 segundos, resultado incierto y
  consulta de la operación.
- No romper `/api/verify` ni el flujo KYC de Caserito.

## Capacidades reales de ARGOS (demostradas vs. supuestas)

### Demostradas en código local y batería VPS del 2026-08-06

| Capacidad | Evidencia | Alcance demostrado |
|---|---|---|
| Comparación 1:1 | `/api/verify` | Compara dos imágenes con DeepFace/ArcFace. Caserito lo consume. |
| Extracción de embedding | `/api/extract-embedding` | Devuelve un vector al consumidor; no crea perfil durable en ARGOS. |
| Identificación 1:N | `/api/identify` | Compara contra candidatos de la petición o consultados en ICARUS legacy. |
| Comparación de embeddings | `/api/compare-embeddings` | Compara dos vectores suministrados. |
| Fix A de detección facial | `ARGOS/decorators.py`, doc 34 | Mapea fallos de detección a 422 `code=face-not-detected` con `image`. |
| Pin de dependencias | `requirements.txt` | `deepface==0.0.100`, `opencv-python>=4.8,<5`; restableció la detección tras regresión de OpenCV 5.0. |

### Supuestas o no demostradas

| Capacidad | Estado | Riesgo |
|---|---|---|
| Prueba de vida (PAD) pasiva | No existe en el código revisado. | Cualquier modelo elegido requiere ensayo en tablet real contra fotos/pantallas. |
| Identificación 1:N con 20 candidatos y latencia < 5 s | No medido en el hardware objetivo. | El objetivo de 5 s es de experiencia, no límite contractual. |
| Autenticación de llamadas entrantes | No existe en el código revisado. | El nuevo contrato v2 debe agregarla sin romper `/api/verify`. |
| Compatibilidad de GUID como referencias opacas | `identify_face` usa `int(match_id)`/`int(trabajador_id)`. | Hay que adaptar el contrato v2 para aceptar y devolver strings/GUID. |

### Estado real de ARGOS en producción (doc 50)

| Aspecto | Valor observado | Implicación para v2 |
|---|---|---|
| Imagen desplegada | `argos:latest` (`sha256:12f6b1e7dcf8…`), construida 2026-08-06; contenedor en marcha desde entonces. | Es la misma imagen de la batería del doc 34; el contrato v2 requiere un nuevo build. |
| Modelo/métrica/umbral | `ArcFace`, `cosine`, `0.68`. | Coincide con la propuesta del spec; `modelo_formato` puede fijarse como `arcface-cosine-512`. |
| Detector | `opencv` (OpenCV 4.14.0.94). | Se mantiene; cambiarlo requeriría recalibrar umbral y mediciones. |
| Hardware | CPU; sin GPU. | Latencia depende de CPU; el objetivo de 5 s debe medirse en VPS. |
| Memoria | Sin límite (`Memory: 0`); host con 8 GB (~3,5 GB disponibles). | Se recomienda fijar `mem_limit=2g` en el nuevo despliegue para proteger al resto. |
| Workers Gunicorn | 2. | Suficiente para arrancar; medir concurrencia KYC+kiosco antes de aumentar. |
| Red | `trajano-shared-network`; puerto 5000 no publicado. | Trajano-Icarus puede llamar a `http://argos:5000` internamente. |
| Autenticación | No hay mTLS; se puede agregar `CONTROL_ACCESO_API_KEY` en `.env.production`. | Opción A (API key) confirmada como viable. |
| Carga actual `/api/verify` | 20 llamadas totales (baterías de agosto), cero tráfico de producción desde entonces. | El kiosco parte de una base desocupada; el cuello de botella será inferencia CPU. |
| Logs | `/app/logs/`, sin retención (`access.log` 81 MB). | Al redesplegar v2 se debe añadir rotación y `max-size` del log del contenedor. |
| PAD | Ningún modelo instalado. DeepFace 0.0.100 soporta `anti_spoofing=True` (FASNet), pero descarga pesos en primer uso. | Hay que pre-hornear los pesos en la imagen Docker y ensayar en tablet real. |
| API key | `CONTROL_ACCESO_API_KEY` ya añadida a `.env.production` (chmod 600). | El valor se transfiere por canal seguro; tomará efecto al redeployar v2. |
| Memoria | `mem_limit=2g` confirmado. | Aplicar en `deploy-production.sh` del repo ARGOS (`--memory 2g`). |
| Logs | `logrotate` configurado en VPS; `log-opt` pendiente en `deploy-production.sh`. | Añadir `--log-opt max-size=10m --log-opt max-file=5` al `docker run`. |
| Health check | Se recomienda quitar el chequeo `icarus_api` legacy de `/health`. | Modificar `ARGOS/views.py` en v2 para no depender de `icarus-api:5090`. |
| Staging | No existe entorno de staging. | Usar contenedor candidato aislado (`argos-v2-candidate`) en la misma red antes del swap. |
| Pesos PAD | Host de build tiene internet. | Pre-hornear pesos FASNet durante el build del Dockerfile. |

## Contrato v2

### Autenticación interna

- **Opción elegida (A):** API key compartida. Trajano-Icarus envía
  `Authorization: Bearer <service-key>` en cada llamada v2. ARGOS valida la clave
  mediante la variable de entorno `CONTROL_ACCESO_API_KEY` (configurada en
  `.env.production` del contenedor) y un decorador exclusivo de las rutas v2.
- **Custodia de la clave:** el valor no sale de la VPS. El agenteVPS lo mantiene
  en `/var/apps/icarus/microservicios/argos/.env.production` (chmod 600) y, al
  desplegar el adaptador `T6`, añade `ArgosControlAcceso__ApiKey` con el mismo
  valor a `/var/apps/trajano-icarus/.env`. La clave no viaja por git, logs,
  documentos ni chat. Para desarrollo local, el operador con acceso root puede
  leerla directamente en la VPS; no se pide al agenteVPS que la exponga.
- **Opción descartada (B):** mTLS entre servicios. El agenteVPS confirmó que no
  hay plan ni implementación de mTLS en la red compartida; introducirlo sería
  trabajo nuevo que bloquearía A0 sin ganar proporcional.
- `/api/verify` y los endpoints existentes de Caserito no se modifican ni exigen
  la API key de Control de acceso.

### Namespace y tenant

- Prefijo de ruta: `/api/v2/control-acceso`.
- Los cuerpos incluyen:
  - `aplicacion`: `"trajano-icarus-control-acceso"`.
  - `tenant_id`: GUID del cliente. ARGOS lo utiliza solo como contexto de trazas
    (sin identidades ni puntuaciones) y lo refleja en la respuesta; no lo valida
    contra base de datos ni deriva candidatos de él.
- ARGOS no selecciona candidatos: recibe el conjunto completo en la petición de
  identificación y devuelve la referencia del candidato del conjunto.

### Formato de plantilla/modelo

- Trajano-Icarus almacena `ModeloFormato` (string) y `VersionModelo` (int). El
  valor inicial acordado es `arcface-cosine-512` y `version_modelo: 1`. El contrato
  v2 los intercambia explícitamente para detectar incompatibilidades.
- El vector se transmite como array de `float` (no base64 ni bytes opacos). ArcFace
  en la imagen desplegada produce vectores de 512 dimensiones.
- `GET /api/v2/control-acceso/capacidades` anuncia:
  - `modelo`: `ArcFace`.
  - `detector_backend`: `opencv`.
  - `embedding_size`: `512`.
  - `distance_metric`: `cosine`.
  - `threshold`: `0.68`.
  - `pad_disponible`: `false` hasta acreditarlo en tablet real.
  - `version_contrato`: `2.0`.

### Extracción

`POST /api/v2/control-acceso/extracciones`

Request:

```json
{
  "aplicacion": "trajano-icarus-control-acceso",
  "tenant_id": "guid-del-cliente",
  "imagen": "base64...",
  "formato": "jpeg"
}
```

Response exitosa (200):

```json
{
  "exitoso": true,
  "vector": [0.1, -0.2, ...],
  "modelo_formato": "arcface-cosine-512",
  "version_modelo": 1,
  "pad_aprobado": true
}
```

Response rechazada (422):

```json
{
  "exitoso": false,
  "codigo": "sin_rostro|varios_rostros|pad_fallido|extraccion_fallida|formato_invalido|imagen_muy_grande",
  "error": "Mensaje genérico"
}
```

ARGOS no almacena la imagen ni el vector.

### Identificación

`POST /api/v2/control-acceso/identificaciones`

Request:

```json
{
  "aplicacion": "trajano-icarus-control-acceso",
  "tenant_id": "guid-del-cliente",
  "imagen": "base64...",
  "formato": "jpeg",
  "modelo_formato_esperado": "arcface-cosine-512",
  "version_modelo_esperada": 1,
  "candidatos": [
    {
      "trabajador_id": "guid-1",
      "vector": [0.1, -0.2, ...],
      "modelo_formato": "arcface-cosine-512",
      "version_enrolamiento": 3
    }
  ]
}
```

Response exitosa (200):

```json
{
  "identificado": true,
  "trabajador_id": "guid-1",
  "modelo_formato": "arcface-cosine-512",
  "version_modelo": 1,
  "pad_aprobado": true
}
```

Response negativa (200 con `identificado: false`):

```json
{
  "identificado": false,
  "codigo": "sin_coincidencia|ambigua|pad_fallido|sin_rostro|varios_rostros|modelo_incompatible|sin_candidatos",
  "error": "Mensaje genérico"
}
```

- La referencia devuelta pertenece siempre al conjunto recibido.
- No se devuelven puntuaciones, distancias ni candidatos alternativos.
- Si hay más de un candidato dentro de un margen de ambigüedad configurable del
  mejor, se responde `ambigua`.

### Decisiones PAD, ambigüedad y rechazo

- **PAD pasiva:** se evalúa sobre la imagen enviada. Si el modelo no puede
  determinar que es una persona real, la respuesta es `pad_fallido`.
- **Ambigüedad:** configurable por `margen_ambiguedad` (default: 0.05 en distancia
  coseno). Si dos o más candidatos están dentro del margen, `ambigua`.
- **Rechazo:** si la mejor distancia supera el umbral, `sin_coincidencia`.
- **Sin rostro / varios rostros:** se detectan antes de comparar y devuelven
  `sin_rostro` / `varios_rostros`.

### Errores

| HTTP | Código interno | Significado |
|---|---|---|
| 400 | `campos_faltantes` | Falta campo obligatorio. |
| 401 | `no_autenticado` | Falta o es inválida la API key. |
| 403 | `aplicacion_no_permitida` | Aplicación o namespace no autorizado. |
| 413 | `payload_muy_grande` | Imagen o lista de candidatos excede límite. |
| 422 | `sin_rostro`, `varios_rostros`, `pad_fallido`, `extraccion_fallida`, `sin_coincidencia`, `ambigua`, `modelo_incompatible` | Negocio facial negativo. |
| 503 | `proveedor_no_disponible` | ARGOS no puede atender la petición. |
| 500 | `error_interno` | No mapeado; sin detalles nominales. |

### Límites

- Imagen: JPEG/PNG, máximo 2 MiB, dimensiones máximas 1920×1920 tras decodificar.
- Una sola cara por imagen; si hay varias, `varios_rostros`.
- Candidatos: no truncar silenciosamente. Si excede un límite configurable
  (propuesto: 50), devolver 413.
- Timeout interno de ARGOS para extracción/identificación: propuesto 15 s, sujeto
  a medición en VPS.

### Privacidad

- ARGOS no conserva imágenes, vectores ni candidatos entre peticiones.
- No escribe identidades, puntuaciones completas ni motivos biométricos detallados
  en logs. Solo se registran códigos genéricos, duración y cantidad de candidatos.
- Trajano-Icarus no envía claves de cifrado a ARGOS.
- Los vectores solo circulan descifrados entre Trajano-Icarus y ARGOS por red
  interna autenticada.

### Compatibilidad con Caserito

- `/api/verify` conserva su contrato, campos, códigos y semántica.
- No se activa PAD sobre la foto del documento de KYC como consecuencia del
  nuevo flujo.
- No se cambian umbrales ni detector globales.
- El nuevo contrato v2 y `/api/verify` no comparten estado ni caché.
- Se ensaya regresión de Caserito antes de cualquier despliegue compartido.
- El despliegue de v2 se hará con un contenedor candidato aislado
  (`argos-v2-candidate`) en la misma red interna; solo tras la batería de humo
  se realiza el swap, sin interrumpir Caserito antes de tiempo.

## Modelo PAD pasivo y licencia

### Opciones evaluadas

1. **DeepFace FASNet (`anti_spoofing=True`)**:
   - Ventaja: ya disponible en `deepface 0.0.100` (soporte añadido en ≥0.0.87);
     menor fricción de dependencias.
   - Riesgo: descarga pesos en el primer uso, por lo que hay que pre-hornearlos en
     la imagen Docker; sin evidencia de eficacia en tablet real.
2. **Silent-Face-Anti-Spoofing** u otro paquete dedicado:
   - Ventaja: modelos específicos para ataque con foto/pantalla.
   - Riesgo: añade dependencia, licencia y tiempo de evaluación en hardware real.
3. **Sin PAD en A0** (solo detección de rostro):
   - Ventaja: desbloquea mediciones de latencia/precisión antes de invertir en PAD.
   - Riesgo: acepta fotos/pantallas; no es productivo sin control físico del dispositivo.

### Decisión propuesta

- **Objetivo del piloto:** opción 1 (DeepFace FASNet). Pre-descargar los pesos en
  el `Dockerfile` de ARGOS para que el contenedor no dependa de internet en runtime.
- **Fallback:** opción 3 si los ensayos en tablet real no alcanzan un umbral mínimo
  aceptable, documentando que el piloto carece de PAD hasta la siguiente iteración.
- **No se afirma que FASNet funcione** sin ensayos en la tablet física contra fotos
  impresas y pantallas.

## Mediciones y pruebas necesarias

Todas las mediciones deben realizarse en el VPS y/o en la tablet real; no se
aceptan resultados de dobles de prueba como evidencia de producción.

1. **Latencia 1:N:** 20 candidatos, medir p95 y p99 de tiempo desde que ARGOS
   recibe la imagen hasta que responde. El log de Gunicorn actual no incluye
   duración; el plan de ARGOS debe añadir `%D` al formato de log de v2.
2. **Precisión:** tasa de falsos positivos, falsos negativos y ambigüedad con
   fotos reales de trabajadores (sintéticas en pruebas unitarias).
3. **PAD:** ataques con foto impresa y pantalla (móvil/tablet) contra FASNet.
4. **Carga compartida:** 1 petición KYC concurrente con N kioscos simultáneos en
   CPU con 2 workers y memoria limitada a 2 GB.
5. **Fila real:** 20 marcaciones secuenciales en la tablet, incluyendo interacción
   humana, cuenta 3–2–1, respuesta facial y retorno a pantalla lista.
6. **Regresión Caserito:** re-ejecutar la batería del doc 28 contra `/api/verify`
   tras cualquier cambio en ARGOS.
7. **Operación:** validar rotación de logs, límite de memoria y health check tras
   el redeploy de v2.

## Criterios de aceptación de A0

1. ARGOS expone `/api/v2/control-acceso/capacidades`, `/extracciones` e
   `/identificaciones` con autenticación interna.
2. El contrato v2 no modifica `/api/verify` ni rompe Caserito.
3. ARGOS no consulta ICARUS legacy, no usa base de datos ni conserva imágenes,
   vectores o candidatos entre peticiones en el nuevo flujo.
4. Las referencias son GUID opacos; la respuesta pertenece al conjunto enviado.
5. Se definen códigos de error estructurados para rechazo facial, ambigüedad y
   fallos PAD sin exponer puntuaciones ni candidatos alternativos.
6. Se documenta el modelo/versión PAD elegido y los ensayos realizados en tablet
   real; si no hay ensayos, se declara explícitamente que PAD no está acreditado.
7. Se miden latencia, precisión, ataques con foto/pantalla y carga compartida; los
   resultados agregados se registran en el plan sin muestras ni identidades.

## Dependencias

- [x] Respuestas del agenteVPS sobre imagen desplegada, recursos del VPS y
  configuración de red/secrets (docs 50 y 52).
- [x] Acuerdo con agenteVPS sobre `CONTROL_ACCESO_API_KEY`, límite de memoria y
  rotación de logs para el redeploy de v2.
- [x] Acuerdo sobre custodia de `CONTROL_ACCESO_API_KEY`: el valor permanece en
  la VPS; el agenteVPS configurará `ArgosControlAcceso__ApiKey` en el entorno de
  Trajano-Icarus al desplegar (doc 54).
- [ ] Ensayos en tablet Android real para PAD y latencia percibida.
- [x] Aprobación de este contrato por parte del usuario antes de implementar A0 en
  ARGOS y T6 en Trajano-Icarus (2026-10-01).
