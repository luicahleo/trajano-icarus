// Genera los iconos PWA placeholder (cuadrado sólido del color primario) sin
// dependencias: firma PNG + IHDR + IDAT (zlib) + IEND con CRC-32 a mano.
import { deflateSync } from 'node:zlib';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const raiz = dirname(fileURLToPath(import.meta.url));
const salida = join(raiz, '..', 'public', 'pwa');

const COLOR = [0x1b, 0x5e, 0x20, 0xff]; // #1B5E20 (verde pino)

function crc32(buf) {
  let crc = 0xffffffff;
  for (let i = 0; i < buf.length; i += 1) {
    crc ^= buf[i];
    for (let k = 0; k < 8; k += 1) crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function chunk(tipo, datos) {
  const largo = Buffer.alloc(4);
  largo.writeUInt32BE(datos.length);
  const t = Buffer.from(tipo, 'ascii');
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(Buffer.concat([t, datos])));
  return Buffer.concat([largo, t, datos, crc]);
}

function png(tam) {
  const firma = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(tam, 0);
  ihdr.writeUInt32BE(tam, 4);
  ihdr[8] = 8; // profundidad de bits
  ihdr[9] = 6; // color tipo RGBA
  const fila = Buffer.alloc(1 + tam * 4);
  fila[0] = 0; // filtro none
  for (let x = 0; x < tam; x += 1) {
    fila[1 + x * 4] = COLOR[0];
    fila[2 + x * 4] = COLOR[1];
    fila[3 + x * 4] = COLOR[2];
    fila[4 + x * 4] = COLOR[3];
  }
  const scanlines = Buffer.concat(Array.from({ length: tam }, () => fila));
  const idat = deflateSync(scanlines);
  return Buffer.concat([firma, chunk('IHDR', ihdr), chunk('IDAT', idat), chunk('IEND', Buffer.alloc(0))]);
}

mkdirSync(salida, { recursive: true });

const iconos = [
  ['pwa-192x192.png', 192],
  ['pwa-512x512.png', 512],
  ['pwa-maskable-192x192.png', 192],
  ['pwa-maskable-512x512.png', 512],
];

for (const [nombre, tam] of iconos) {
  writeFileSync(join(salida, nombre), png(tam));
}

console.log(`Iconos generados en ${salida}: ${iconos.length}`);
