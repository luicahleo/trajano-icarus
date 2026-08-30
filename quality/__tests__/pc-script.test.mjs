import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const core = readFileSync(new URL('../../iniciar-pc.ps1', import.meta.url), 'utf8');
const pc1 = readFileSync(new URL('../../iniciar-pc1.ps1', import.meta.url), 'utf8');
const pc1SinWifi = readFileSync(new URL('../../iniciar-pc1-sin-wifi.ps1', import.meta.url), 'utf8');
const pc2 = readFileSync(new URL('../../iniciar-pc2.ps1', import.meta.url), 'utf8');
const pc3 = readFileSync(new URL('../../iniciar-pc3.ps1', import.meta.url), 'utf8');

test('el core acepta el perfil pc1|pc2|pc3 y arma el compose del perfil', () => {
  assert.match(core, /\[ValidateSet\('pc1', 'pc2', 'pc3'\)\]/);
  assert.match(core, /"docker-compose\.\$Perfil\.yml"/);
  assert.match(core, /"\.local\/\$Perfil\/caddy-data"/);
});

test('las advertencias de docker info no detienen el inicio', () => {
  assert.match(core, /\$ErrorActionPreference = 'Continue'[\s\S]*docker info \*> \$null/);
  assert.match(core, /\$codigoDocker = \$LASTEXITCODE/);
  assert.match(core, /finally[\s\S]*\$ErrorActionPreference = \$preferenciaErrores/);
  assert.match(core, /if \(\$codigoDocker -ne 0\)/);
});

test('el up y el down de compose toleran el stderr de docker', () => {
  assert.match(
    core,
    /\$ErrorActionPreference = 'Continue'[\s\S]*docker compose @archivosCompose up -d --build[\s\S]*\$codigoUp = \$LASTEXITCODE[\s\S]*finally[\s\S]*\$ErrorActionPreference = \$preferenciaErrores/,
  );
  assert.match(
    core,
    /\$ErrorActionPreference = 'Continue'[\s\S]*docker compose @archivosCompose down --volumes[\s\S]*\$codigoDown = \$LASTEXITCODE[\s\S]*finally[\s\S]*\$ErrorActionPreference = \$preferenciaErrores/,
  );
});

test('los fallos transitorios de curl reintentan el sondeo HTTPS', () => {
  assert.match(
    core,
    /for \(\$intento = 0; \$intento -lt 30[\s\S]*\$ErrorActionPreference = 'Continue'[\s\S]*curl\.exe/,
  );
  assert.match(core, /\$codigoSalud = \$LASTEXITCODE/);
  assert.match(core, /\$saludable = \$codigoSalud -eq 0/);
  assert.match(core, /if \(-not \$saludable\) \{ Start-Sleep -Seconds 1 \}/);
});

test('la recreación de datos exige confirmación y elimina volúmenes', () => {
  assert.match(core, /\[switch\]\$RecrearDatos/);
  assert.match(core, /\[switch\]\$ConfirmarBorradoDatos/);
  assert.match(core, /if \(\$RecrearDatos -and -not \$ConfirmarBorradoDatos\)[\s\S]*throw/);
  assert.match(
    core,
    /if \(\$RecrearDatos\)[\s\S]*docker compose @archivosCompose down --volumes[\s\S]*docker compose @archivosCompose up/,
  );
});

test('el entorno Icarus usa nombres propios y no depende de ARGOS', () => {
  assert.match(core, /ICARUS_LAN_IP/);
  assert.match(core, /ICARUS_LAN_HOST/);
  assert.doesNotMatch(core, /argos/i);
});

test('el sondeo de salud atraviesa el proxy de la web bajo /api', () => {
  assert.match(core, /\/api\/health/);
  assert.match(core, /--resolve/);
});

test('los tres wrappers delegan al core con su perfil', () => {
  assert.match(pc1, /Perfil = 'pc1'/);
  assert.match(pc2, /Perfil = 'pc2'/);
  assert.match(pc3, /Perfil = 'pc3'/);
  for (const script of [pc1, pc2, pc3]) {
    assert.match(script, /iniciar-pc\.ps1/);
  }
});

test('el core y los wrappers aceptan el modo dev|prod', () => {
  assert.match(core, /\[ValidateSet\('dev', 'prod'\)\]/);
  assert.match(core, /docker-compose\.prodlocal\.yml/);
  assert.match(core, /Construir-ContenidoProduccion/);
  assert.match(core, /WEB_UPSTREAM/);
  for (const script of [pc1, pc2, pc3, pc1SinWifi]) {
    assert.match(script, /Modo = \$Modo/);
  }
});

test('el wrapper de PC1 sin WiFi inicia el entorno solo en localhost', () => {
  assert.match(core, /\[switch\]\$SoloLocal/);
  assert.match(core, /if \(\$SoloLocal\)[\s\S]*'127\.0\.0\.1'/);
  assert.match(core, /if \(\$SoloLocal\)[\s\S]*'localhost'/);
  assert.match(pc1SinWifi, /Perfil = 'pc1'/);
  assert.match(pc1SinWifi, /SoloLocal = \$true/);
  assert.match(pc1SinWifi, /iniciar-pc\.ps1/);
});
