# Agentes de Trajano-Icarus

Seis roles especializados, iguales en cualquier proveedor. Las reglas están en
[PROTOCOLO.md](PROTOCOLO.md), y el diseño en el
[spec del sistema de agentes](../../superpowers/specs/2026-10-30-sistema-agentes-portables-design.md).

## Dónde está cada cosa

| Ruta | Contenido |
|---|---|
| `docs/ai/agentes/roles/` | Fuente de cada rol. Se edita aquí |
| `.claude/agents/`, `.kimi-code/agents/` | Adaptadores generados. No se editan |
| `docs/ai/tareas/` | Una carpeta por tarea, con su bitácora |
| `quality/tarea.mjs` | CLI de tareas, común a todos los proveedores |
| `quality/guardia-comandos.mjs` | Hook que frena comandos peligrosos |

Después de editar un rol, regenera los adaptadores:

```powershell
node quality/generar-adaptadores.mjs
```

## Cómo empezar

Abre el orquestador con el proveedor que tengas disponible y descríbele la tarea.

```powershell
claude --agent orquestador
kimi --agent orquestador
```

Kimi no tiene modelo por agente. Para elegirlo al lanzar, usa `-m`:

```powershell
kimi --agent orquestador -m kimi-code/k3.1-pro
```

También puedes abrir directamente un rol, por ejemplo `kimi --agent desarrollador`.
Es lo más económico para una tarea ya encaminada, porque evita pagar un orquestador
además del rol.

## Tu papel

1. Pides el cambio.
2. Cuando la tarea llega a `PRUEBA_MANUAL`, pruebas con el entorno levantado.
3. Si encuentras un problema, cuéntaselo al orquestador con pasos, resultado actual
   y resultado esperado.
4. Si todo está bien, apruebas desde tu propia terminal, fuera del agente:

```powershell
node quality/tarea.mjs aprobar <id>
```

La orden te pide escribir el commit. Si el contenido cambió desde la verificación,
se niega.

## Cuando un proveedor se queda sin tokens

No hace falta salvar nada: la bitácora ya está en disco. Abre el otro proveedor y
pídele que continúe:

```text
Continúa la tarea activa. Ejecuta node quality/tarea.mjs resumen, contrástalo con
git y ejecuta relevo con tu proveedor antes de seguir.
```

## Proveedor sin adaptador

Cualquier agente con shell puede tomar un rol:

```text
Lee docs/ai/agentes/roles/desarrollador.md y actúa como ese rol.
```

## Guardia en Kimi

Claude Code la carga desde `.claude/settings.json`. Kimi solo admite hooks de
usuario, así que para activarla agrega esto a `~/.kimi-code/config.toml`, con la
ruta de tu repositorio:

```toml
[[hooks]]
event = "PreToolUse"
matcher = "Bash|Read"
command = "node C:/Users/lrcahuana/source/repos/Trajano-Icarus/quality/guardia-comandos.mjs"
timeout = 5
```

Fuera de este repositorio el hook solo bloquea `--no-verify`. Kimi frena por su
cuenta la lectura de archivos sensibles como `.env`, antes de llegar a este hook.
