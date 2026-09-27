# ARGOS compartido con Caserito — evidencia para Control de acceso

Fecha: 2026-09-26. Revisión de código local y respuestas escritas del agenteVPS.
No se accedió a la VPS, no se enviaron fotos, no se ejecutó reconocimiento y
no se modificó ni desplegó ARGOS o Caserito. El estado remoto se atribuye a
las fechas de los documentos, no a una comprobación en vivo de esta sesión.

Esta revisión complementa el [brainstorming](2026-09-25-control-acceso-fase1-brainstorm.md)
y condiciona A0 del [plan](../plans/2026-09-25-control-acceso-fase1.md).

## Fuentes y alcance

- ARGOS local: `C:/Users/lrcahuana/source/repos/dev/ARGOS`, rama develop,
  commit `80ef1cf`, árbol limpio al revisar.
- Caserito local: `C:/Users/lrcahuana/source/repos/dev_Caserito`, rama develop,
  commit `ff5e689`, árbol limpio al revisar.
- Correspondencia: `C:/Users/lrcahuana/source/repos/dev/DocumentacionProyectos/preguntasrespuestasCaseritoApp_AgenteLocal_AgenteVPS`.
- Respuestas VPS 15, 26, 28, 32, 34 y 45; peticiones 14 y 44 como contexto.
  La respuesta 48 acredita reloj/zonas de Trajano-Icarus, no capacidades faciales.

## Qué existe

| Capacidad | Evidencia local | Alcance demostrado |
|---|---|---|
| Comparación 1:1 | `ARGOS/views.py`, `/api/verify` | Compara dos imágenes con DeepFace/ArcFace. Caserito consume este endpoint. |
| Extracción de plantilla | `/api/extract-embedding` | Devuelve embedding al consumidor; no crea un perfil durable en ARGOS. |
| Identificación 1:N | `/api/identify` | Compara contra candidatos de la petición o consultados en ICARUS legacy. Existe, pero requiere adaptación para el nuevo módulo. |
| Comparación de plantillas | `/api/compare-embeddings` | Compara dos vectores suministrados, sin gestionar trabajadores. |
| Caché | `get_cached_embeddings`, `ARGOS/api_client.py` | Caché en memoria por cliente durante 300 s; la fuente durable es ICARUS legacy. No es un almacén biométrico independiente. |
| Prueba de vida/PAD | Llamadas a DeepFace en `ARGOS/views.py` | No habilitan anti-spoofing ni existe un flujo PAD propio en el código revisado. `enforce_detection=True` exige detectar un rostro, no acredita una persona viva. |
| Registro/revocación de perfiles | Rutas de `ARGOS/views.py` | No se encontraron endpoints actuales de alta/sustitución/revocación. Las referencias históricas a `register_face` no prueban su existencia. |

`ARGOS/__init__.py` fija ArcFace, distancia coseno, detector OpenCV y umbral de
distancia 0.68. `requirements.txt` fija DeepFace 0.0.100 y OpenCV <5. Esos valores
describen el código, no una precisión validada para trabajadores del kiosco.

## Qué hace Caserito

Adaptador revisado:
`CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/VerificadorIdentidadArgosHttp.cs`.

Envía documento como `image1` y selfie como `image2` a `/api/verify`. Consume
`success`, `verified` y `similarity_percent`; distingue `422 face-not-detected`
y conserva compatibilidad con algunos errores 500 antiguos. `X-Service-Key`
se añade solo si está configurada. No utiliza registro de perfiles ni 1:N.

Las reglas de aprobación/revisión de KYC están en Caserito, no en ARGOS.
`OpcionesArgos.UmbralAutoAprobacion` tiene valor inicial 60. No reutilizar ese
valor como umbral del kiosco: el problema, conjunto de candidatos y evidencia
son distintos. El porcentaje se calcula desde una distancia; no es una
probabilidad de identidad ni una medida de prueba de vida.

## Qué está acreditado por agenteVPS

| Documento | Fecha y conclusión |
|---|---|
| 15 | Servicio compartido en red Docker `trajano-shared-network`, accesible internamente como `argos:5000`; health no prueba reconocimiento. |
| 26 | 2026-08-06: `/api/verify` funcional; sin autenticación exigida por el código; problemas de errores sin rostro y consumo de memoria a considerar. |
| 32 | 2026-08-06: un rebuild introdujo OpenCV 5 y rompió la detección aunque el contenedor aparecía healthy. |
| 34 | 2026-08-06: pin de dependencias corrigió la regresión; cinco casos de la batería pasaron, incluidos 422 sin rostro y 200 de coincidencia. Tiempos de los casos faciales aproximadamente 4–5 s. |
| 45 | 2026-09-21: muestra KYC insuficiente para calibrar umbrales empíricamente. No demuestra precisión 1:N ni PAD. |

La última batería funcional explícita encontrada es la 34. La respuesta 45 es
más reciente, pero trata de calibración KYC; no es otra batería del servicio.
No inferir que el tag `latest` o la versión estática `1.0.0` identifica el
commit desplegado hoy. No reproducir aquí filas personales ni scores nominales.

## Adaptaciones que sí están justificadas

- Preservar `/api/verify`, campos, códigos esperados y comportamiento KYC.
  No cambiar globalmente detector/umbral ni activar PAD sobre la foto del
  documento de Caserito como consecuencia de añadir Control de acceso.
- IDs opacos compatibles con GUID: el `int(match_id)`/`int(trabajador_id)` de
  `identify_face` no sirve directamente para las referencias del nuevo módulo.
- Aislamiento: el backend selecciona los candidatos de su tenant; no aceptar
  que el navegador elija tenant ni candidatos. ARGOS autentica al consumidor
  y procesa únicamente los candidatos recibidos, sin catálogo propio.
- Exigir coincidencia única y prueba de vida en el nuevo flujo. No exponer
  `top_matches`, IDs ajenos ni puntuaciones al kiosco.
- Revisar autenticación interna del nuevo contrato: el header de servicio
  usado por `api_client.py` es saliente hacia ICARUS, no validación de llamadas
  entrantes a ARGOS. La protección de v2 no debe romper Caserito por sorpresa.
- Revisar privacidad de punta a punta. `log_request` emite claves de campos,
  no los valores completos, pero se registran scores y ciertos IDs; `log_error`
  y el 500 reflejan texto de excepciones. No afirmar que todo payload se vuelca
  ni que el logging actual cumple ya el contrato privado del kiosco.
- Compartir servicio implica carga compartida. Medir latencia/memoria y
  concurrencia KYC+kiosco antes de fijar timeouts o afirmar capacidad suficiente.

## Decisión arquitectónica confirmada: Trajano custodia, ARGOS procesa

El borrador proponía custodia en ARGOS sin decisión previa del usuario. Tras
explicar las alternativas, el usuario confirmó custodia cifrada en
Trajano-Icarus y procesamiento sin persistencia en ARGOS (2026-09-26).

| Opción | Consecuencia |
|---|---|
| Plantillas cifradas en Trajano-Icarus; ARGOS procesa | Elegida. Trajano gestiona claves, versiones y revocación, y entrega candidatos autorizados al motor por petición. Nunca biometría en logs ni plantillas en el navegador. |
| Plantillas bajo custodia de ARGOS | Descartada. No se le añade capa de datos, catálogo persistente ni conexión directa a base de datos. |

La caché en memoria de candidatos legacy en ARGOS evitaba consultas repetidas
al servidor antiguo; no es la caché del dispositivo IMCA usada offline. El
nuevo flujo no usa ninguna de ellas: candidatos de cada petición, sin
persistencia en ARGOS, sin plantillas en Android ni fotos de marcación guardadas.
No se reutilizan datos KYC de Caserito como catálogo de trabajadores.

Se actualizan esquema previsto, contrato y tareas 4/6/7; las reglas funcionales
no cambian. A0 sigue pendiente para PAD, formato/versiones y capacidad medida,
no para decidir la custodia. Ninguna implementación se realiza en esta sesión.

## Pruebas y operación para A0

El CI actual ejecuta `python -m unittest discover -s tests -p 'test_*.py' -v`,
validación sintáctica y build Docker. El único archivo bajo `tests/` revisado
contiene dos pruebas del workflow; no cubre contratos faciales. No se presupone
pytest instalado ni cobertura biométrica por el mero éxito del CI.

El workflow actual es despliegue manual desde master con confirmación y CI
verde; esto prevalece sobre instrucciones históricas de despliegue automático.
El workflow recrea el servicio, por lo que afecta a Caserito durante el cambio.
Al ampliar ARGOS, planificar regresión de `/api/verify`, ensayos de 1:N/PAD y
validación en VPS. El plan solo documenta estos requisitos, no pide desplegar.
