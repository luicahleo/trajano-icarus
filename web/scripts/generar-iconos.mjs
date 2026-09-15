// Genera los iconos PWA y los favicon de fallback a partir del SVG gradiente
// de la marca Icarus (public/favicon.svg). Rasteriza con @resvg/resvg-js, la
// única dependencia de desarrollo de este generador; el build de la app no la
// usa. Uso: `node scripts/generar-iconos.mjs` desde web/.
import { Resvg } from '@resvg/resvg-js';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const scripts = dirname(fileURLToPath(import.meta.url));
const web = join(scripts, '..');
const repositorio = join(web, '..');

const svgGradiente = readFileSync(join(web, 'public', 'favicon.svg'), 'utf8');

// Fondo del manifest (vite.config.ts): los iconos opacos se ven iguales en
// cualquier lanzador.
const FONDO = '#F8F6F1';

function png(svg, tam) {
  const resvg = new Resvg(svg, {
    fitTo: { mode: 'width', value: tam },
    background: FONDO,
  });
  return resvg.render().asPng();
}

// Icono maskable: la marca reducida al 60 % y centrada dentro de la zona
// segura del recorte circular del lanzador.
function svgMaskable() {
  const interno = svgGradiente.replace(
    '<svg ',
    '<svg x="160" y="160" width="480" height="480" ',
  );
  return (
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 800 800">` +
    `<rect width="800" height="800" fill="${FONDO}"/>${interno}</svg>`
  );
}

// Contenedor ICO con una sola imagen PNG (formato Vista), suficiente para el
// fallback de navegadores que no usan SVG como favicon.
function ico(pngBytes) {
  const cabecera = Buffer.alloc(6);
  cabecera.writeUInt16LE(0, 0);
  cabecera.writeUInt16LE(1, 2);
  cabecera.writeUInt16LE(1, 4);
  const entrada = Buffer.alloc(16);
  entrada[0] = 32;
  entrada[1] = 32;
  entrada.writeUInt16LE(1, 4);
  entrada.writeUInt16LE(32, 6);
  entrada.writeUInt32LE(pngBytes.length, 8);
  entrada.writeUInt32LE(22, 12);
  return Buffer.concat([cabecera, entrada, pngBytes]);
}

const publico = join(web, 'public');
const wwwrootGestor = join(
  repositorio,
  'Icarus',
  'src',
  'Apps',
  'Trajano.GestorCaisy',
  'wwwroot',
);
mkdirSync(join(publico, 'pwa'), { recursive: true });
mkdirSync(wwwrootGestor, { recursive: true });

const iconos = [
  ['pwa-192x192.png', 192, false],
  ['pwa-512x512.png', 512, false],
  ['pwa-maskable-192x192.png', 192, true],
  ['pwa-maskable-512x512.png', 512, true],
];

for (const [nombre, tam, maskable] of iconos) {
  writeFileSync(join(publico, 'pwa', nombre), png(maskable ? svgMaskable() : svgGradiente, tam));
}

const faviconIco = ico(png(svgGradiente, 32));
writeFileSync(join(publico, 'favicon.ico'), faviconIco);
writeFileSync(join(wwwrootGestor, 'favicon.ico'), faviconIco);

console.log(`Iconos generados en ${join(publico, 'pwa')}: ${iconos.length}`);
