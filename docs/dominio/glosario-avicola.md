# Glosario del dominio avícola

Vocabulario y reglas de negocio de Trajano-Icarus. Es conocimiento del negocio,
no convención de estilo: las convenciones viven en `AGENTS.md` y en la puerta de
calidad.

Consultar este documento **antes de nombrar una entidad o inventar una regla**.
Los identificadores de dominio van en español, igual que el resto del proyecto.

## Módulos

| Módulo | Alcance |
|---|---|
| Control de acceso | Trabajadores, zonas, registros biométricos, entradas y salidas |
| Gestión avícola | Granjas, galpones, producción de huevos, mortalidad, vacunación, alimentación, despachos, precios |

## Unidades

| Término | Definición |
|---|---|
| Maple | Unidad estándar de empaque de huevos. **Un maple son 30 huevos.** |
| Unidades incompletas | Huevos sueltos que no completan un maple. Siempre menos de 30. |

Cálculo del total, sin excepciones:

```
Total Huevos = (CantidadMaples * 30) + UnidadesIncompletas
```

La constante 30 pertenece al dominio y se declara una sola vez. Nunca repetirla
como número suelto en el código.

## Reglas transversales

1. **Soft delete en todas las entidades.** Nunca se hace un borrado físico: se
   marca `EstaActivo = false`. Las consultas normales filtran por `EstaActivo`.
   El motivo es trazabilidad: registros de acceso y de producción no se borran.
2. **Ninguna fecha del dominio admite futuro.** Una producción, una mortalidad,
   una vacunación o un registro de acceso ocurren en el pasado o en el presente.
   La validación es de dominio, no de interfaz.
3. **Los datos biométricos y los registros nominales de acceso son sensibles.**
   Nunca aparecen en logs, mensajes de error ni trazas. Ver la regla anti-PII en
   `AGENTS.md`.

## Pendiente

Las entidades, sus atributos y las relaciones entre módulos se definen al migrar
cada bounded context, en los subproyectos 5 y siguientes. Este documento se
amplía ahí; no se anticipa acá.
