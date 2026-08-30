import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const dev = readFileSync(new URL('../../docker-compose.dev.yml', import.meta.url), 'utf8');
const prodlocal = readFileSync(new URL('../../docker-compose.prodlocal.yml', import.meta.url), 'utf8');
const perfiles = new Map(
  ['pc1', 'pc2', 'pc3'].map((perfil) => [
    perfil,
    readFileSync(new URL(`../../docker-compose.${perfil}.yml`, import.meta.url), 'utf8'),
  ]),
);

// Los servicios están a dos espacios y sus propiedades a cuatro; no hay mapas
// anidados a nivel de servicio en este archivo.
function servicios(yaml) {
  const bloques = new Map();
  let actual = null;
  let lineas = [];
  for (const linea of yaml.split('\n')) {
    const inicio = /^  ([a-zA-Z0-9_-]+):\s*$/.exec(linea);
    if (inicio) {
      if (actual) bloques.set(actual, lineas.join('\n'));
      actual = inicio[1];
      lineas = [linea];
    } else if (actual) {
      lineas.push(linea);
    }
  }
  if (actual) bloques.set(actual, lineas.join('\n'));
  return bloques;
}

test('api y sqlserver reinician solos ante un crash transitorio de arranque', () => {
  const serviciosDev = servicios(dev);
  for (const servicio of ['api', 'sqlserver']) {
    assert.ok(serviciosDev.get(servicio), `el servicio ${servicio} debe existir en docker-compose.dev.yml`);
    assert.match(serviciosDev.get(servicio), /^\s{4}restart: unless-stopped$/m);
  }
});

test('ningún perfil de PC redefine api ni sqlserver sin la política de reinicio', () => {
  for (const [perfil, contenido] of perfiles) {
    assert.doesNotMatch(contenido, /^\s{2}api:/m, `perfil ${perfil}`);
    assert.doesNotMatch(contenido, /^\s{2}sqlserver:/m, `perfil ${perfil}`);
  }
});

test('el modo prod-local arma el stack completo sin el api dev', () => {
  const serviciosProd = servicios(prodlocal);
  assert.ok(serviciosProd.get('seq'), 'seq debe existir');
  assert.ok(serviciosProd.get('sqlserver'), 'sqlserver debe existir');
  assert.ok(serviciosProd.get('web'), 'web debe existir');
  assert.doesNotMatch(prodlocal, /^\s{2}api:/m, 'api no debe estar en prod-local');
  assert.match(serviciosProd.get('sqlserver'), /^\s{4}restart: unless-stopped$/m);
  assert.match(serviciosProd.get('web'), /^\s{4}restart: unless-stopped$/m);
  assert.match(serviciosProd.get('web'), /Dockerfile\.web/);
  assert.match(serviciosProd.get('web'), /ASPNETCORE_HTTP_PORTS: "8080"/);
  assert.match(serviciosProd.get('web'), /Seq__Url: "http:\/\/seq:80"/);
});
