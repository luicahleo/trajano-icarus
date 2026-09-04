# Sistema de diseño — Trajano

## Producto y usuarios

Trajano-Icarus gestiona operaciones avícolas para clientes y trabajadores. La
nueva aplicación de oficina Trajano-GestorCaisy comparte API e identidad y debe
sentirse parte del mismo producto. Su primera función es procesar pedidos de
alimento y publicar Notificaciones de Precios de Alimentos.

## Dirección visual

- Aplicación operativa, sobria, clara y de alta densidad informativa.
- Priorizar lectura rápida de estados, fechas, cantidades y excepciones.
- Mantener el shell con barra superior aqua oscuro y navegación lateral clara.
- No usar gradientes, tipografías decorativas, ilustraciones ni colores ajenos.
- Las acciones principales usan aqua; terracota queda para énfasis secundario.
- Estados se expresan con texto e icono además del color.

## Tokens obligatorios

- Cuerpo: Open Sans; títulos: Prompt.
- Primario: `#007C83`; primario oscuro: `#005A61`; primario tenue: `#D9F3F4`.
- Secundario: `#D75A2D`; secundario oscuro: `#AC3F1B`.
- Fondo: `#F4F8F8`; superficie: `#FFFFFF`; texto: `#12262A`; secundario: `#54666A`.
- Borde: `#D5E2E3`; tabla: `#B9D0D2`; cabecera de tabla: `#EAF5F5`.
- Radios: controles 12 px, tarjetas/tablas 16 px, diálogos 20 px.
- Espaciado base: múltiplos de 8 px; contenido desktop máximo 1440 px.

## Componentes y patrones

- Barra superior fija con producto, contexto de página, notificaciones, tema y sesión.
- Navegación izquierda de 248 px; elemento activo con fondo tenue y texto aqua.
- Cabecera de página con título y acciones a la derecha.
- Bandejas operativas con filtros visibles, tabla compacta y panel de detalle.
- Chips de estado legibles; alertas para bloqueos; confirmaciones para acciones terminales.
- Formularios estructurados por secciones, ayuda breve junto al campo y resumen antes de publicar/enviar.

## Accesibilidad y movimiento

- Contraste WCAG AA, foco visible, navegación por teclado y etiquetas explícitas.
- Áreas táctiles mínimas de 40 px aunque el objetivo principal sea escritorio.
- Transiciones de 150 ms; respetar `prefers-reduced-motion`.

## Requisitos específicos

- El GestorPedidoAlimento trabaja siempre en línea; no mostrar cola ni banner offline.
- La bandeja debe distinguir `Solicitado`, `Aceptado`, `Despachado`, devuelto,
  rechazado y recepción final sin depender solo de colores.
- Mantener visible el cliente/granja, fecha del pedido, presentación, total y
  entrega estimada; no exhibir datos personales innecesarios.
- La publicación de precios parte de un PDF extraído a borrador editable y exige
  revisión explícita antes de publicar.
