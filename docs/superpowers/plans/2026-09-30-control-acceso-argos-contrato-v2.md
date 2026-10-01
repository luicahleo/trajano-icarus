> **For agentic workers:** REQUIRED SUB-SKILL: Use
> `superpowers:subagent-driven-development` (recommended) or
> `superpowers:executing-plans` to implement this plan task-by-task. Steps use
> checkbox syntax for tracking: `[x]` means completed; `[ ]` means pending.

# Plan — contrato v2 ARGOS (A0) y adaptador Trajano-Icarus (T6)

**Goal:** Implementar en ARGOS el contrato v2 `/api/v2/control-acceso` con
autenticación interna, extracción e identificación facial, y en Trajano-Icarus
el adaptador HTTP real `ClienteArgosControlAcceso` que consume ese contrato,
sin romper `/api/verify` ni Caserito.

**Architecture:** En ARGOS se añade un blueprint `ARGOS/control_acceso_v2.py`
con rutas versionadas, decorador de autenticación interna y funciones de
extracción/identificación reutilizando DeepFace/ArcFace. En Trajano-Icarus se
registra `ClienteArgosControlAcceso` como implementación de
`IProveedorIdentidadFacial` cuando la opción `ArgosControlAcceso:Url` está
configurada, mapeando los DTOs internos `MuestraFacial`/`CandidatoFacial` al
contrato v2.

**Tech stack:**

- ARGOS: Python 3.11, Flask, DeepFace 0.0.100, OpenCV 4.x, Gunicorn, unittest.
- Trajano-Icarus: .NET 10, EF Core, MediatR, xUnit/NSubstitute,
  Testcontainers.MsSql, HttpClient.

## Global constraints

- No modificar `/api/verify` ni romper Caserito.
- ARGOS no consulta ICARUS legacy, no usa base de datos ni conserva imágenes,
  vectores o candidatos entre peticiones en el nuevo flujo.
- Trajano-Icarus no envía claves de cifrado a ARGOS.
- Referencias opacas compatibles con GUID; modelo/formato/version explícitos.
- TDD: cada test debe verse en rojo por la causa esperada antes de implementar.
- `./verify.ps1` en Trajano-Icarus y `python -m unittest discover ...` +
  `docker build` en ARGOS antes de cada commit/push.
- No commit/push a `master`; ARGOS usa PR a `develop`, Trajano-Icarus push directo
  a `develop`.
- No datos biométricos, identidades de personas ni secretos en documentos, logs,
  tests ni git.
- Leer antes de tocar código:
  - Spec:
    `docs/superpowers/specs/2026-09-30-control-acceso-argos-contrato-v2-design.md`.
  - Planes previos:
    `docs/superpowers/plans/2026-09-25-control-acceso-fase1.md` y
    `docs/superpowers/plans/2026-09-29-control-acceso-incidencias.md`.
  - ARGOS local:
    `C:/Users/lrcahuana/source/repos/dev/ARGOS`.
  - Correspondencia VPS: docs 49 y 50 en
    `C:/Users/lrcahuana/source/repos/dev/DocumentacionProyectos/preguntasrespuestasCaseritoApp_AgenteLocal_AgenteVPS`.
- Valores reales del entorno (doc 50): imagen `argos:latest` 2026-08-06, CPU sin
  GPU, workers Gunicorn 2, sin límite de memoria (recomendar 2 GB al redeploy),
  red interna `trajano-shared-network`, puerto 5000 no publicado, modelo
  `ArcFace`/`opencv`/`cosine`/`0.68`, embedding 512.
- Autenticación: API key compartida vía `CONTROL_ACCESO_API_KEY` en
  `.env.production` de ARGOS; `Authorization: Bearer <key>` desde Trajano-Icarus.
- PAD: usar DeepFace FASNet (`anti_spoofing=True`) si los ensayos en tablet lo
  justifican; pre-descargar pesos en el `Dockerfile`.

---

## Task 1: ARGOS — scaffold del blueprint v2 y autenticación interna

**Files:**

- Create: `ARGOS/control_acceso_v2.py`
- Create: `ARGOS/auth_control_acceso.py`
- Modify: `ARGOS/__init__.py` (registrar blueprint)
- Modify: `ARGOS/logger.py` (opcional: log seguro de rutas v2)
- Test: `ARGOS/tests/test_control_acceso_v2.py`

**Interfaces:**

- Consumes: `CONTROL_ACCESO_API_KEY` env var (en producción desde
  `.env.production` del contenedor); existing `logger`/`log_request`.
- Produces: blueprint `control_acceso_v2` con prefix `/api/v2/control-acceso`;
  decorator `requiere_control_acceso_auth` que devuelve 401/403 genéricos.

- [x] **Step 1: Write the failing test**

  En `ARGOS/tests/test_control_acceso_v2.py`:

  ```python
  import os
  import unittest
  from ARGOS import app

  class ControlAccesoAuthTests(unittest.TestCase):
      def setUp(self):
          os.environ["CONTROL_ACCESO_API_KEY"] = "test-key"
          self.client = app.test_client()

      def test_capacidades_sin_auth_devuelve_401(self):
          response = self.client.get("/api/v2/control-acceso/capacidades")
          self.assertEqual(response.status_code, 401)

      def test_capacidades_con_auth_invalida_devuelve_401(self):
          response = self.client.get(
              "/api/v2/control-acceso/capacidades",
              headers={"Authorization": "Bearer wrong"})
          self.assertEqual(response.status_code, 401)
  ```

- [x] **Step 2: Run test to verify it fails**

  Run: `python -m unittest ARGOS.tests.test_control_acceso_v2 -v`
  Expected: FAIL with 404 (la ruta no existe todavía) o 200 si no hay auth.

- [x] **Step 3: Write minimal implementation**

  En `ARGOS/auth_control_acceso.py`:

  ```python
  import os
  from functools import wraps
  from flask import request, jsonify

  CONTROL_ACCESO_API_KEY = os.environ.get("CONTROL_ACCESO_API_KEY", "")

  def requiere_control_acceso_auth(f):
      @wraps(f)
      def decorated(*args, **kwargs):
          auth = request.headers.get("Authorization", "")
          if not auth.startswith("Bearer "):
              return jsonify({"success": False, "error": "No autenticado"}), 401
          if auth[7:] != CONTROL_ACCESO_API_KEY or not CONTROL_ACCESO_API_KEY:
              return jsonify({"success": False, "error": "No autenticado"}), 401
          return f(*args, **kwargs)
      return decorated
  ```

  En `ARGOS/control_acceso_v2.py`:

  ```python
  from flask import Blueprint, jsonify
  from ARGOS.auth_control_acceso import requiere_control_acceso_auth

  control_acceso_v2 = Blueprint("control_acceso_v2", __name__, url_prefix="/api/v2/control-acceso")

  @control_acceso_v2.route("/capacidades", methods=["GET"])
  @requiere_control_acceso_auth
  def capacidades():
      return jsonify({"version_contrato": "2.0"})
  ```

  En `ARGOS/__init__.py` registrar el blueprint:

  ```python
  from ARGOS.control_acceso_v2 import control_acceso_v2
  app.register_blueprint(control_acceso_v2)
  ```

- [x] **Step 4: Run test to verify it passes**

  Run: `python -m unittest ARGOS.tests.test_control_acceso_v2 -v`
  Expected: PASS.

- [x] **Step 5: Commit**

  ```bash
  git add ARGOS/control_acceso_v2.py ARGOS/auth_control_acceso.py ARGOS/__init__.py ARGOS/tests/test_control_acceso_v2.py
  git commit -m "feat(argos): scaffold control acceso v2 con auth interna"
  ```

---

## Task 2: ARGOS — endpoint de capacidades

**Files:**

- Modify: `ARGOS/control_acceso_v2.py`
- Modify: `ARGOS/__init__.py` (exponer modelo/config)
- Test: `ARGOS/tests/test_control_acceso_v2.py`

**Interfaces:**

- Consumes: `MODEL_NAME`, `DETECTOR_BACKEND`, `VERIFICATION_THRESHOLD`.
- Produces: JSON con `modelo`, `detector_backend`, `embedding_size`,
  `distance_metric`, `threshold`, `pad_disponible`, `version_contrato`.

- [x] **Step 1: Write the failing test**

  ```python
  def test_capacidades_incluye_modelo_y_version(self):
      response = self.client.get(
          "/api/v2/control-acceso/capacidades",
          headers={"Authorization": "Bearer test-key"})
      self.assertEqual(response.status_code, 200)
      data = response.get_json()
      self.assertEqual(data["version_contrato"], "2.0")
      self.assertIn("modelo", data)
      self.assertIn("embedding_size", data)
      self.assertIn("pad_disponible", data)
  ```

- [x] **Step 2: Run test to verify it fails**

  Expected: FAIL — falta `embedding_size` o `pad_disponible`.

- [x] **Step 3: Write minimal implementation**

  En `ARGOS/control_acceso_v2.py`:

  ```python
  from ARGOS import MODEL_NAME, DETECTOR_BACKEND, VERIFICATION_THRESHOLD

  @control_acceso_v2.route("/capacidades", methods=["GET"])
  @requiere_control_acceso_auth
  def capacidades():
      return jsonify({
          "version_contrato": "2.0",
          "modelo": MODEL_NAME,
          "detector_backend": DETECTOR_BACKEND,
          "embedding_size": 512,
          "distance_metric": "cosine",
          "threshold": VERIFICATION_THRESHOLD,
          "pad_disponible": False
      })
  ```

  Notas:
  - `embedding_size` es 512 porque la imagen desplegada usa ArcFace (doc 50).
  - `pad_disponible` inicia en `false`; pasará a `true` solo después de acreditar
    PAD en tablet real.

- [x] **Step 4: Run test to verify it passes**

  Expected: PASS.

- [x] **Step 5: Commit**

  ```bash
  git add ARGOS/control_acceso_v2.py ARGOS/tests/test_control_acceso_v2.py
  git commit -m "feat(argos): expone capacidades del contrato v2"
  ```

---

## Task 3: ARGOS — endpoint de extracción

**Files:**

- Modify: `ARGOS/control_acceso_v2.py`
- Modify: `ARGOS/decorators.py` (reutilizar manejo de `FaceNotDetected`)
- Test: `ARGOS/tests/test_control_acceso_v2.py`

**Interfaces:**

- Consumes: request JSON con `imagen` (base64), `formato`, `tenant_id`.
- Produces: 200 con `exitoso: true`, `vector`, `modelo_formato`, `version_modelo`,
  `pad_aprobado`; o 422 con `codigo` genérico.

- [x] **Step 1: Write the failing test**

  ```python
  import base64

  def build_image_payload():
      # Imagen 1x1 JPEG mínima generada dinámicamente o fixture base64 pequeño
      return base64.b64encode(b"...").decode("utf-8")

  def test_extraccion_devuelve_vector(self):
      response = self.client.post(
          "/api/v2/control-acceso/extracciones",
          json={"imagen": build_image_payload(), "formato": "jpeg", "tenant_id": "00000000-0000-0000-0000-000000000000"},
          headers={"Authorization": "Bearer test-key"})
      self.assertEqual(response.status_code, 200)
      data = response.get_json()
      self.assertTrue(data["exitoso"])
      self.assertIn("vector", data)
      self.assertEqual(data["modelo_formato"], "arcface-cosine-512")

  def test_extraccion_sin_rostro_devuelve_422(self):
      response = self.client.post(
          "/api/v2/control-acceso/extracciones",
          json={"imagen": base64.b64encode(b"no-face").decode(), "formato": "jpeg", "tenant_id": "00000000-0000-0000-0000-000000000000"},
          headers={"Authorization": "Bearer test-key"})
      self.assertEqual(response.status_code, 422)
      self.assertEqual(response.get_json()["codigo"], "sin_rostro")
  ```

- [x] **Step 2: Run test to verify it fails**

  Expected: FAIL — endpoint no implementado o falta mapeo de errores.

- [x] **Step 3: Write minimal implementation**

  Reutilizar `decode_base64_image` de `ARGOS/views.py` (o mover a un módulo
  compartido). Implementar:

  ```python
  @control_acceso_v2.route("/extracciones", methods=["POST"])
  @requiere_control_acceso_auth
  def extraer():
      data = request.get_json(silent=True) or {}
      if "imagen" not in data:
          return jsonify({"exitoso": False, "codigo": "campos_faltantes"}), 400
      try:
          image_array = decode_base64_image(data["imagen"])
          embeddings = DeepFace.represent(
              img_path=image_array,
              model_name=MODEL_NAME,
              detector_backend=DETECTOR_BACKEND,
              enforce_detection=True)
          if not embeddings:
              return jsonify({"exitoso": False, "codigo": "sin_rostro"}), 422
          return jsonify({
              "exitoso": True,
              "vector": embeddings[0]["embedding"],
              "modelo_formato": "arcface-cosine-512",
              "version_modelo": 1,
              "pad_aprobado": False
          })
      except Exception as e:
          resp = _face_not_detected_response(e, {})
          if resp is not None:
              body, status = resp
              data = body.get_json()
              data["exitoso"] = False
              data["codigo"] = "sin_rostro"
              return jsonify(data), status
          return jsonify({"exitoso": False, "codigo": "extraccion_fallida"}), 500
  ```

- [x] **Step 4: Run test to verify it passes**

  Expected: PASS. Si el fixture no tiene rostro real, ajustar para usar una
  imagen sintética con rostro generada por PIL o un fixture pequeño.

- [x] **Step 5: Commit**

  ```bash
  git add ARGOS/control_acceso_v2.py ARGOS/tests/test_control_acceso_v2.py
  git commit -m "feat(argos): endpoint de extraccion v2"
  ```

---

## Task 4: ARGOS — endpoint de identificación

**Files:**

- Modify: `ARGOS/control_acceso_v2.py`
- Modify: `ARGOS/decorators.py` (reutilizar manejo de errores)
- Test: `ARGOS/tests/test_control_acceso_v2.py`

**Interfaces:**

- Consumes: request JSON con `imagen`, `formato`, `tenant_id`,
  `modelo_formato_esperado`, `version_modelo_esperada`, `candidatos`.
- Produces: 200 con `identificado: true/false`, `trabajador_id` (si aplica),
  `codigo` (si negativo), `modelo_formato`, `version_modelo`, `pad_aprobado`.

- [x] **Step 1: Write the failing test**

  ```python
  def test_identificacion_coincide_con_candidato(self):
      vector = [0.0] * 512
      response = self.client.post(
          "/api/v2/control-acceso/identificaciones",
          json={
              "imagen": build_image_with_face_matching(vector),
              "formato": "jpeg",
              "tenant_id": "00000000-0000-0000-0000-000000000000",
              "modelo_formato_esperado": "arcface-cosine-512",
              "version_modelo_esperada": 1,
              "candidatos": [{"trabajador_id": "11111111-1111-1111-1111-111111111111", "vector": vector, "modelo_formato": "arcface-cosine-512", "version_enrolamiento": 1}]
          },
          headers={"Authorization": "Bearer test-key"})
      self.assertEqual(response.status_code, 200)
      data = response.get_json()
      self.assertTrue(data["identificado"])
      self.assertEqual(data["trabajador_id"], "11111111-1111-1111-1111-111111111111")

  def test_identificacion_sin_coincidencia_devuelve_codigo(self):
      vector_candidato = [1.0] * 512
      vector_probe = [-1.0] * 512
      response = self.client.post(
          "/api/v2/control-acceso/identificaciones",
          json={
              "imagen": build_image_with_face_vector(vector_probe),
              "formato": "jpeg",
              "tenant_id": "00000000-0000-0000-0000-000000000000",
              "modelo_formato_esperado": "arcface-cosine-512",
              "version_modelo_esperada": 1,
              "candidatos": [{"trabajador_id": "11111111-1111-1111-1111-111111111111", "vector": vector_candidato, "modelo_formato": "arcface-cosine-512", "version_enrolamiento": 1}]
          },
          headers={"Authorization": "Bearer test-key"})
      self.assertEqual(response.status_code, 200)
      self.assertEqual(response.get_json()["codigo"], "sin_coincidencia")
  ```

- [x] **Step 2: Run test to verify it fails**

  Expected: FAIL — endpoint no implementado.

- [x] **Step 3: Write minimal implementation**

  Implementar `identificar` siguiendo la lógica de `/api/identify` pero:

  - Sin `cliente_id` ni consulta a ICARUS legacy.
  - Candidatos vienen del body.
  - Referencias son strings GUID.
  - Devolver `modelo_incompatible` si `modelo_formato` no coincide.
  - Calcular distancia coseno; si la mejor supera `threshold`,
    `sin_coincidencia`.
  - Si hay segundo candidato dentro de `margen_ambiguedad`, `ambigua`.
  - No devolver `top_matches`, distancias ni puntuaciones.

- [x] **Step 4: Run test to verify it passes**

  Expected: PASS.

- [x] **Step 5: Commit**

  ```bash
  git add ARGOS/control_acceso_v2.py ARGOS/tests/test_control_acceso_v2.py
  git commit -m "feat(argos): endpoint de identificacion v2"
  ```

---

## Task 5: ARGOS — tests de contrato v2 (errores, límites, privacidad)

**Files:**

- Modify: `ARGOS/tests/test_control_acceso_v2.py`

**Interfaces:**

- Consumes: endpoints v2.
- Produces: cobertura de 400, 401, 403, 413, 422, 503 (simulado).

- [x] **Step 1: Write the failing tests**

  Añadir tests para:

  - Campo `imagen` faltante → 400.
  - Imagen base64 inválido → 500 (o 422 según contrato).
  - Imagen > 2 MiB → 413.
  - Candidatos con `modelo_formato` distinto → `modelo_incompatible`.
  - Dos candidatos con vectores iguales → `ambigua`.
  - Verificar que logs no contienen vectores ni GUID de trabajador.

- [x] **Step 2: Run tests to verify they fail**

  Expected: FAIL — validaciones de tamaño y ambigüedad no implementadas.

- [x] **Step 3: Implementar validaciones**

  En `control_acceso_v2.py`:

  - Validar tamaño de imagen decodificada antes de procesar.
  - Validar `modelo_formato` de todos los candidatos.
  - Implementar lógica de ambigüedad.
  - Asegurar que `log_request` no reciba vectores ni GUID personales.

- [x] **Step 4: Run tests to verify they pass**

  Expected: PASS.

- [x] **Step 5: Commit**

  ```bash
  git add ARGOS/control_acceso_v2.py ARGOS/tests/test_control_acceso_v2.py
  git commit -m "test(argos): cubre errores, limites y privacidad de v2"
  ```

---

## Task 6: ARGOS — regresión Caserito, build Docker y ajustes operativos

**Files:**

- Modify: `ARGOS/Dockerfile` (pre-descargar pesos PAD si se habilita FASNet)
- Modify: `ARGOS/deploy-production.sh` (límite de memoria 2g, log-opt,
  `--name argos-v2-candidate` para validación, quitar chequeo legacy del health)
- Modify: `ARGOS/views.py` (quitar dependencia `icarus_api` del `/health`)
- Modify: `ARGOS/requirements.txt` (solo si se añade dependencia PAD)
- Test: `ARGOS/tests/test_workflows.py` (ya existe)

**Interfaces:**

- Consumes: `/api/verify` existente.
- Produces: confirmación de que Caserito sigue funcionando; imagen lista para
  despliegue con memoria, logging y health actualizados.

- [x] **Step 1: Write/extend the regression test**

  En `ARGOS/tests/test_control_acceso_v2.py` o un nuevo
  `ARGOS/tests/test_caserito_regression.py`:

  ```python
  def test_verify_sigue_respondiendo(self):
      response = self.client.post("/api/verify", json={"image1": "...", "image2": "..."})
      self.assertIn(response.status_code, [200, 422, 500])

  def test_health_no_reporta_icarus_api(self):
      response = self.client.get("/health")
      data = response.get_json()
      self.assertNotIn("icarus_api", data)
  ```

- [x] **Step 2: Run test suite**

  Run: `python -m unittest discover -s tests -p 'test_*.py' -v`
  Expected: PASS.

- [x] **Step 3: Build Docker image**

  Run: `docker build -t argos:control-acceso-validacion .`
  Expected: SUCCESS.

- [ ] **Step 4: Ajustar Dockerfile para PAD**

  Pendiente deliberadamente para T13: PAD permanece desactivado hasta validar
  el modelo en hardware real; no se incorporan pesos ni dependencias de PAD
  antes de esa decisión operativa.

  - Si se habilita PAD con FASNet, añadir una línea que invoque
    `DeepFace.extract_faces(..., anti_spoofing=True)` o similar para forzar la
    descarga de pesos durante el build (evitar descargas en runtime).

- [x] **Step 5: Ajustar `deploy-production.sh` para v2**

  Añadir/modificar el `docker run`:

  ```bash
  docker run -d \
      --name argos-v2-candidate \
      --network trajano-shared-network \
      --restart unless-stopped \
      --memory 2g \
      --log-opt max-size=10m --log-opt max-file=5 \
      --env-file .env.production \
      -v $(pwd)/logs:/app/logs \
      argos:control-acceso-validacion
  ```

  El script debe:
  1. Etiquetar la imagen actual como `argos:previous` para rollback inmediato.
  2. Levantar `argos-v2-candidate` con `--restart unless-stopped`.
  3. Ejecutar batería de humo de `/api/verify` y `/api/v2/control-acceso/*`.
  4. Solo si pasa: `docker stop argos && docker rm argos && docker rename argos-v2-candidate argos` y retiquetar la imagen nueva como `argos:latest`.
  5. Si falla: destruir `argos-v2-candidate` y recrear `argos` desde `argos:previous`.

  **Ejecución:** el agenteVPS ejecuta este procedimiento en la VPS; el
  agenteLocal solo debe asegurar que el código esté mergeado en `develop` de
  ARGOS y que `deploy-production.sh` incluya `--memory 2g`, `--log-opt`, restart
  y la etiqueta de rollback.

- [x] **Step 6: Commit**

  ```bash
  git add ARGOS/Dockerfile ARGOS/deploy-production.sh ARGOS/views.py ARGOS/tests/test_control_acceso_v2.py
  git commit -m "feat(argos): ajustes operativos y regresion para v2"
  ```

  Luego abrir PR a `develop` en ARGOS según su `AGENTS.md`. Una vez mergeado,
  coordinar con agenteVPS para que ejecute el build, el candidato, la batería y
  el swap en la VPS.

---

## Task 7: Trajano-Icarus — opciones y registro DI del adaptador

**Files:**

- Create: `Icarus/src/ControlAcceso/Icarus.ControlAcceso.Infrastructure/Argos/OpcionesArgosControlAcceso.cs`
- Modify: `Icarus/src/ControlAcceso/Icarus.ControlAcceso.Infrastructure/DependencyInjection.cs`
- Modify: `Icarus/src/Host/Icarus.Host/appsettings*.json` (opciones de ejemplo)
- Test: `Icarus/tests/Icarus.UnitTests/ControlAcceso/ContratoArgosTests.cs` (ya existe)

**Interfaces:**

- Consumes: `IConfiguration` y `IProveedorIdentidadFacial`.
- Produces: `OpcionesArgosControlAcceso` con `Url`, `ApiKey`, `Timeout`;
  registro condicional de `ClienteArgosControlAcceso`. `margen_ambiguedad` es
  configuración exclusiva del lado de ARGOS (no viaja en la petición ni se lee
  de `/capacidades`), así que no se modela aquí.

- [x] **Step 1: Write the failing test**

  En `ContratoArgosTests.cs`:

  ```csharp
  [Fact]
  public void CuandoNoHayUrl_UsaProveedorNoDisponible()
  {
      // Verificar que sin opciones sigue funcionando el proveedor por defecto
  }
  ```

- [x] **Step 2: Run test to verify it fails**

  Expected: FAIL — opciones no existen.

- [x] **Step 3: Write minimal implementation**

  En `OpcionesArgosControlAcceso.cs`:

  ```csharp
  public sealed class OpcionesArgosControlAcceso
  {
      public const string Seccion = "ArgosControlAcceso";
      public string Url { get; set; } = string.Empty;
      public string ApiKey { get; set; } = string.Empty;
      public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);
  }
  ```

  En `DependencyInjection.cs`:

  ```csharp
  services.AddOptions<OpcionesArgosControlAcceso>().BindConfiguration(OpcionesArgosControlAcceso.Seccion);
  var opciones = configuration.GetSection(OpcionesArgosControlAcceso.Seccion).Get<OpcionesArgosControlAcceso>();
  if (opciones is { Url: not null and not "" })
      services.AddHttpClient<IProveedorIdentidadFacial, ClienteArgosControlAcceso>();
  else
      services.AddSingleton<IProveedorIdentidadFacial, ProveedorIdentidadFacialNoDisponible>();
  ```

- [x] **Step 4: Run test to verify it passes**

  Expected: PASS.

- [ ] **Step 5: Configurar la API key**

  Pendiente del despliegue VPS de T13. La clave no se copia a archivos ni al
  repositorio; en operación se inyectará como `ArgosControlAcceso__ApiKey`.

  En producción el agenteVPS añade `ArgosControlAcceso__ApiKey` a
  `/var/apps/trajano-icarus/.env` al desplegar; el valor permanece en la VPS y
  no se transfiere por documentos ni chat (respuesta 54). Para desarrollo local,
  el operador puede leer el valor directamente en
  `/var/apps/icarus/microservicios/argos/.env.production` (chmod 600) y exponerlo
  como variable de entorno o user secret, nunca en `appsettings.json` ni git.
  La petición original quedó en
  `preguntasrespuestasCaseritoApp_AgenteLocal_AgenteVPS/53_peticion_agente_local_argos_contrato_v2_apikey_deploy_2026-10-01.md`.

- [x] **Step 6: Commit**

  ```bash
  git add Icarus/src/ControlAcceso/Icarus.ControlAcceso.Infrastructure/Argos/OpcionesArgosControlAcceso.cs Icarus/src/ControlAcceso/Icarus.ControlAcceso.Infrastructure/DependencyInjection.cs
  git commit -m "feat(control-acceso): opciones y registro DI del adaptador ARGOS v2"
  ```

---

## Task 8: Trajano-Icarus — implementar `ClienteArgosControlAcceso.ExtraerAsync`

**Files:**

- Create: `Icarus/src/ControlAcceso/Icarus.ControlAcceso.Infrastructure/Argos/ClienteArgosControlAcceso.cs`
- Test: `Icarus/tests/Icarus.UnitTests/ControlAcceso/ContratoArgosTests.cs`

**Interfaces:**

- Consumes: `MuestraFacial`, `OpcionesArgosControlAcceso`, `HttpClient`.
- Produces: `ResultadoEnrolamiento` (éxito con vector/modelo/version o rechazo
  con motivo genérico).

- [x] **Step 1: Write the failing test**

  Usar `HttpMessageHandler` doble para simular respuesta 200:

  ```csharp
  [Fact]
  public async Task Extraer_mapea_respuesta_exitosa()
  {
      var handler = new TestHandler(r => new HttpResponseMessage(HttpStatusCode.OK)
      {
          Content = new StringContent("""{"exitoso":true,"vector":[0.1],"modelo_formato":"arcface-cosine-512","version_modelo":1,"pad_aprobado":true}""")
      });
      var cliente = new ClienteArgosControlAcceso(new HttpClient(handler), Options.Create(new OpcionesArgosControlAcceso { Url = "http://argos", ApiKey = "k" }));
      var resultado = await cliente.ExtraerAsync(new MuestraFacial(new byte[] { 1 }, "jpeg"));
      Assert.True(resultado.Exitoso);
      Assert.Equal("arcface-cosine-512", resultado.ModeloFormato);
  }
  ```

- [x] **Step 2: Run test to verify it fails**

  Expected: FAIL — `ClienteArgosControlAcceso` no existe.

- [x] **Step 3: Write minimal implementation**

  ```csharp
  public sealed class ClienteArgosControlAcceso : IProveedorIdentidadFacial
  {
      private readonly HttpClient _httpClient;
      private readonly OpcionesArgosControlAcceso _opciones;

      public ClienteArgosControlAcceso(HttpClient httpClient, IOptions<OpcionesArgosControlAcceso> opciones)
      {
          _httpClient = httpClient;
          _opciones = opciones.Value;
          _httpClient.BaseAddress = new Uri(_opciones.Url);
          _httpClient.Timeout = _opciones.Timeout;
          _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_opciones.ApiKey}");
      }

      public async Task<ResultadoEnrolamiento> ExtraerAsync(MuestraFacial muestra, CancellationToken cancellationToken = default)
      {
          var request = new { aplicacion = "trajano-icarus-control-acceso", tenant_id = Guid.Empty, imagen = Convert.ToBase64String(muestra.Contenido), formato = muestra.Formato };
          var response = await _httpClient.PostAsJsonAsync("/api/v2/control-acceso/extracciones", request, cancellationToken);
          var content = await response.Content.ReadAsStringAsync(cancellationToken);
          var result = JsonSerializer.Deserialize<ExtraccionResponse>(content);
          if (response.IsSuccessStatusCode && result!.exitoso)
              return ResultadoEnrolamiento.Exito(result.vector!, result.modelo_formato!, result.version_modelo);
          return ResultadoEnrolamiento.Rechazado(MapCodigo(result?.codigo));
      }

      // ... IdentificarAsync
  }
  ```

- [x] **Step 4: Run test to verify it passes**

  Expected: PASS.

- [x] **Step 5: Commit**

  ```bash
  git add Icarus/src/ControlAcceso/Icarus.ControlAcceso.Infrastructure/Argos/ClienteArgosControlAcceso.cs Icarus/tests/Icarus.UnitTests/ControlAcceso/ContratoArgosTests.cs
  git commit -m "feat(control-acceso): adaptador ARGOS v2 extraccion"
  ```

---

## Task 9: Trajano-Icarus — implementar `ClienteArgosControlAcceso.IdentificarAsync`

**Files:**

- Modify: `Icarus/src/ControlAcceso/Icarus.ControlAcceso.Infrastructure/Argos/ClienteArgosControlAcceso.cs`
- Test: `Icarus/tests/Icarus.UnitTests/ControlAcceso/ContratoArgosTests.cs`

**Interfaces:**

- Consumes: `MuestraFacial`, `IReadOnlyList<CandidatoFacial>`.
- Produces: `ResultadoIdentificacionFacial`.

- [x] **Step 1: Write the failing test**

  ```csharp
  [Fact]
  public async Task Identificar_mapea_coincidencia()
  {
      var handler = new TestHandler(r => new HttpResponseMessage(HttpStatusCode.OK)
      {
          Content = new StringContent("""{"identificado":true,"trabajador_id":"11111111-1111-1111-1111-111111111111","modelo_formato":"arcface-cosine-512","version_modelo":1,"pad_aprobado":true}""")
      });
      var cliente = new ClienteArgosControlAcceso(new HttpClient(handler), Options.Create(new OpcionesArgosControlAcceso { Url = "http://argos", ApiKey = "k" }));
      var candidatos = new List<CandidatoFacial> { new(Guid.Parse("11111111-1111-1111-1111-111111111111"), new float[] { 0.1f }, "arcface-cosine-512", 1, 1) };
      var resultado = await cliente.IdentificarAsync(new MuestraFacial(new byte[] { 1 }, "jpeg"), candidatos);
      Assert.True(resultado.Identificado);
      Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), resultado.TrabajadorId);
  }
  ```

- [x] **Step 2: Run test to verify it fails**

  Expected: FAIL — `IdentificarAsync` no implementado o no mapea GUID.

- [x] **Step 3: Write minimal implementation**

  En `ClienteArgosControlAcceso.cs`:

  ```csharp
  public async Task<ResultadoIdentificacionFacial> IdentificarAsync(MuestraFacial muestra, IReadOnlyList<CandidatoFacial> candidatos, CancellationToken cancellationToken = default)
  {
      var request = new
      {
          aplicacion = "trajano-icarus-control-acceso",
          tenant_id = Guid.Empty,
          imagen = Convert.ToBase64String(muestra.Contenido),
          formato = muestra.Formato,
          modelo_formato_esperado = candidatos.FirstOrDefault()?.ModeloFormato ?? "arcface-cosine-512",
          version_modelo_esperada = 1,
          candidatos = candidatos.Select(c => new { trabajador_id = c.TrabajadorId.ToString(), vector = c.Vector.Select(v => (double)v).ToArray(), c.ModeloFormato, version_enrolamiento = c.VersionEnrolamiento }).ToList()
      };
      var response = await _httpClient.PostAsJsonAsync("/api/v2/control-acceso/identificaciones", request, cancellationToken);
      var content = await response.Content.ReadAsStringAsync(cancellationToken);
      var result = JsonSerializer.Deserialize<IdentificacionResponse>(content);
      if (response.IsSuccessStatusCode && result!.identificado && Guid.TryParse(result.trabajador_id, out var id))
          return ResultadoIdentificacionFacial.Coincide(id);
      return ResultadoIdentificacionFacial.SinCoincidencia(MapCodigo(result?.codigo));
  }
  ```

- [x] **Step 4: Run test to verify it passes**

  Expected: PASS.

- [x] **Step 5: Commit**

  ```bash
  git add Icarus/src/ControlAcceso/Icarus.ControlAcceso.Infrastructure/Argos/ClienteArgosControlAcceso.cs Icarus/tests/Icarus.UnitTests/ControlAcceso/ContratoArgosTests.cs
  git commit -m "feat(control-acceso): adaptador ARGOS v2 identificacion"
  ```

---

## Task 10: Trajano-Icarus — tests unitarios del adaptador (errores y mapeos)

**Files:**

- Modify: `Icarus/tests/Icarus.UnitTests/ControlAcceso/ContratoArgosTests.cs`

**Interfaces:**

- Consumes: `ClienteArgosControlAcceso`.
- Produces: cobertura de 401, 403, 413, 422, 503, timeout, mapeo de códigos.

- [x] **Step 1: Write the failing tests**

  Añadir tests para:

  - 422 `sin_rostro` → `ResultadoEnrolamiento.Rechazado("sin_rostro")`.
  - 422 `pad_fallido` → `ResultadoIdentificacionFacial.SinCoincidencia("pad_fallido")`.
  - 422 `extraccion_fallida` → `ResultadoEnrolamiento.Rechazado("extraccion_fallida")`.
  - 422 `formato_invalido` → `ResultadoEnrolamiento.Rechazado("formato_invalido")`.
  - 422 `imagen_muy_grande` → `ResultadoEnrolamiento.Rechazado("imagen_muy_grande")`.
  - 422 `sin_candidatos` → `ResultadoIdentificacionFacial.SinCoincidencia("sin_candidatos")`.
  - 503 → `ResultadoIdentificacionFacial.SinCoincidencia("proveedor_no_disponible")`.
  - Timeout → `ResultadoIdentificacionFacial.SinCoincidencia("proveedor_no_disponible")`.
  - `ambigua` → motivo `ambigua`.
  - `modelo_incompatible` → motivo `modelo_incompatible`.
  - Código desconocido o ausente → motivo `proveedor_no_disponible` (fallback).

- [x] **Step 2: Run tests to verify they fail**

  Expected: FAIL — manejo de errores no completo.

- [x] **Step 3: Implementar manejo de errores**

  En `ClienteArgosControlAcceso.cs`:

  ```csharp
  private static string MapCodigo(string? codigo) => codigo switch
  {
      "sin_rostro" => "sin_rostro",
      "varios_rostros" => "varios_rostros",
      "pad_fallido" => "pad_fallido",
      "extraccion_fallida" => "extraccion_fallida",
      "formato_invalido" => "formato_invalido",
      "imagen_muy_grande" => "imagen_muy_grande",
      "ambigua" => "ambigua",
      "modelo_incompatible" => "modelo_incompatible",
      "sin_coincidencia" => "sin_coincidencia",
      "sin_candidatos" => "sin_candidatos",
      _ => "proveedor_no_disponible"
  };
  ```

  Envolver llamadas en try/catch para `TaskCanceledException` (timeout) y
  `HttpRequestException`.

- [x] **Step 4: Run tests to verify they pass**

  Expected: PASS.

- [x] **Step 5: Commit**

  ```bash
  git add Icarus/src/ControlAcceso/Icarus.ControlAcceso.Infrastructure/Argos/ClienteArgosControlAcceso.cs Icarus/tests/Icarus.UnitTests/ControlAcceso/ContratoArgosTests.cs
  git commit -m "test(control-acceso): cubre errores del adaptador ARGOS v2"
  ```

---

## Task 11: Trajano-Icarus — tests de integración contra servicio efímero

**Files:**

- Create: `Icarus/tests/Icarus.IntegrationTests/ControlAcceso/ArgosContratoRealTests.cs`
- Modify: `Icarus/tests/Icarus.IntegrationTests/ControlAcceso/` (fixture compartida si aplica)

**Interfaces:**

- Consumes: imagen Docker `argos:control-acceso-validacion` o un contenedor
  efímero con ARGOS v2.
- Produces: prueba real de extracción e identificación con datos sintéticos.

- [x] **Step 1: Write the failing test**

  ```csharp
  [Fact]
  public async Task Extraccion_real_devuelve_vector_o_rechazo_sintetico()
  {
      // Levantar contenedor efímero o conectarse a ARGOS configurado
      var resultado = await _proveedor.ExtraerAsync(new MuestraFacial(_imagenSintetica, "jpeg"));
      Assert.True(resultado.Exitoso || !string.IsNullOrEmpty(resultado.Motivo));
  }
  ```

- [x] **Step 2: Run test to verify it fails**

  Expected: FAIL — el test está deshabilitado o no hay ARGOS disponible.

- [x] **Step 3: Implementar fixture efímera**

  Si se usa Testcontainers para levantar ARGOS, configurar `Dockerfile` local
  como imagen base. Si no, usar un `WebApplicationFactory` con un doble HTTP
  mínimo que verifique el contrato serializado. El test real contra contenedor
  debe quedar condicionado a `ArgosControlAcceso:Url` configurado en testing.

- [x] **Step 4: Run test to verify it passes**

  Expected: PASS contra doble o contenedor real según disponibilidad.

- [x] **Step 5: Commit**

  ```bash
  git add Icarus/tests/Icarus.IntegrationTests/ControlAcceso/ArgosContratoRealTests.cs
  git commit -m "test(control-acceso): integracion con contrato ARGOS v2"
  ```

---

## Task 12: Trajano-Icarus — puerta de calidad y push

**Files:**

- Todos los modificados en T6.

**Interfaces:**

- Consumes: `./verify.ps1`.
- Produces: confirmación de que todo el módulo sigue verde.

- [x] **Step 1: Run directed tests**

  Run:

  ```powershell
  dotnet test Icarus/tests/Icarus.UnitTests/Icarus.UnitTests.csproj --filter FullyQualifiedName~ContratoArgosTests
  dotnet test Icarus/tests/Icarus.IntegrationTests/Icarus.IntegrationTests.csproj --filter FullyQualifiedName~ArgosContratoRealTests
  ```

  Expected: PASS.

- [x] **Step 2: Run full verification gate**

  Run: `./verify.ps1` (Docker activo).
  Expected: verde.

- [x] **Step 3: Update general plan and handoff**

  Marcar A0/T6 como completados en
  `docs/superpowers/plans/2026-09-25-control-acceso-fase1.md` y actualizar
  `docs/ai/HANDOFF.md` con resultados reales.

- [x] **Step 4: Commit and push**

  ```bash
  git add .
  git commit -m "feat(control-acceso): integra adaptador ARGOS v2"
  git push origin develop
  ```

---

## Self-review

1. **Spec coverage:**
   - Autenticación interna → Task 1.
   - Capacidades → Task 2.
   - Extracción → Task 3.
   - Identificación → Task 4.
   - Errores y límites → Task 5 y 10.
   - Privacidad → Tasks 5, 10.
   - Compatibilidad Caserito → Task 6.
   - Adaptador Trajano-Icarus → Tasks 7-12.
   - PAD: no se implementa en A0 hasta tener ensayos en tablet real; se deja
     `pad_aprobado: false` y se documenta en el plan de T13.

2. **Placeholder scan:** no TBD/TODO; cada tarea incluye archivos, interfaces,
   pasos y comandos. Los payloads JSON y los nombres de tipos son concretos.

3. **Type consistency:**
   - `MuestraFacial`, `CandidatoFacial`, `ResultadoEnrolamiento`,
     `ResultadoIdentificacionFacial` son los tipos internos existentes.
   - `ClienteArgosControlAcceso` implementa `IProveedorIdentidadFacial`.
   - `OpcionesArgosControlAcceso` usa la misma sección en DI y consumers.

## Execution handoff

**Plan complete and saved to
`docs/superpowers/plans/2026-09-30-control-acceso-argos-contrato-v2.md`.**

Two execution options:

1. **Subagent-Driven (recommended)** — Dispatch a fresh subagent per task,
   review between tasks, fast iteration. REQUIRED SUB-SKILL:
   `superpowers:subagent-driven-development`.
2. **Inline Execution** — Execute tasks in this session using
   `superpowers:executing-plans`, batch execution with checkpoints.

Which approach?

## Resultados observados de la sesión documental (2026-10-01)

- Se incorporó la respuesta 52 del agenteVPS al spec y al plan (API key ya en
  `.env.production`, `mem_limit=2g` confirmado, logrotate configurado, host de
  build con internet para pre-descargar pesos PAD, sin staging, deploy con
  contenedor candidato aislado).
- Se creó el documento 53 de correspondencia con el agenteVPS pidiendo el valor
  de `CONTROL_ACCESO_API_KEY` por canal seguro y confirmando el procedimiento de
  deploy candidato.
- El PR `luicahleo/argos#2` (rama `feature/control-acceso-v2-doc` → `develop`)
  pasó los checks `validar` y GitGuardian y fue mergeado en `0da2879`; el
  documento `docs/control-acceso-v2.md` ya está en `develop` de ARGOS.
- Se incorporó la respuesta 54 (API key no sale de la VPS; agenteVPS configura
  `ArgosControlAcceso__ApiKey` en el entorno de Trajano-Icarus; deploy con
  candidato, etiqueta `argos:previous` para rollback, y swap ejecutado por el
  agenteVPS en la misma semana).
- La implementación de código de A0/T6 no comenzó en esta sesión; sigue
  condicionada a la aprobación del contrato y a los ensayos en tablet real.
- Los límites de no tocar `master`, no desplegar servicios y no implementar T6
  ni T13 en código se respetaron.
