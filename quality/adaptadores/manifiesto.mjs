// Tabla harness -> archivo -> contenido. Es la única fuente de los adaptadores.
// Agregar un harness es agregar una entrada acá y correr el generador. La
// convención de archivo de cada herramienta se verifica contra su documentación
// en el momento de agregarla; no se adivina.
//
// Codex y Kimi CLI no aparecen: descubren AGENTS.md de forma nativa y
// jerárquica, así que no necesitan adaptador. DeepSeek tampoco: es un modelo,
// no un harness, y hereda el archivo del harness que lo hospeda.

export const AVISO =
  'Archivo generado por quality/generar-adaptadores.mjs. No editar a mano: editar AGENTS.md.';

const IGNORADOS = `# ${AVISO}
# Rutas que ningún agente necesita leer: ruido de build, dependencias y
# secretos. Mantener el contenido idéntico en los tres archivos es justamente
# lo que este generador garantiza.

.git/
node_modules/
bin/
obj/
artifacts/
dist/
coverage/

.env
.env.*
*.pfx
*.p12
*.key

.vs/
.idea/
graphify-out/
.superpowers/
`;

export const ADAPTADORES = [
  {
    harness: 'Claude Code',
    ruta: 'CLAUDE.md',
    contenido: `<!-- ${AVISO} -->

@AGENTS.md
`,
  },
  {
    harness: 'Gemini CLI',
    ruta: 'GEMINI.md',
    contenido: `<!-- ${AVISO} -->

@./AGENTS.md
`,
  },
  {
    harness: 'Copilot',
    ruta: '.github/copilot-instructions.md',
    contenido: `<!-- ${AVISO} -->

Las instrucciones de este proyecto viven en \`AGENTS.md\`, en la raíz del
repositorio. Leelo completo antes de proponer cambios.

Copilot no soporta imports: este archivo es solo un puntero y no debe acumular
reglas propias, que divergirían del núcleo.
`,
  },
  { harness: 'Cline', ruta: '.clineignore', contenido: IGNORADOS },
  { harness: 'Cursor', ruta: '.cursorignore', contenido: IGNORADOS },
  { harness: 'Gemini CLI', ruta: '.geminiignore', contenido: IGNORADOS },
];
