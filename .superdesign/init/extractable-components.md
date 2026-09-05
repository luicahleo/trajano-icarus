# Componentes extraíbles

## AppShell

- Source: `web/src/app/AppLayout.tsx`
- Category: layout
- Description: barra superior, sesión, navegación lateral responsive y área de contenido.
- Extractable props: `titulo` (string), `activeItem` (string), `showNotifications` (boolean), `notificationCount` (number).
- Hardcoded: nombre Trajano Icarus, anchuras, iconos de tema/salida y estilos MUI.

## NavegacionPrincipal

- Source: `web/src/app/NavegacionPrincipal.tsx`
- Category: layout
- Description: lista vertical de navegación con elemento activo.
- Extractable props: `activeItem` (string).
- Hardcoded: estructura, radios, espaciado y tamaño de iconos.

## PaginaCabecera

- Source: `web/src/app/ui/PaginaCabecera.tsx`
- Category: basic
- Description: título, subtítulo y zona de acciones de una página.
- Extractable props: `titulo` (string), `subtitulo` (string), `showPrimaryAction` (boolean).
- Hardcoded: jerarquía, disposición responsive y estilos.

## TablaDatos

- Source: `web/src/app/ui/TablaDatos.tsx`
- Category: basic
- Description: tabla de datos genérica con cabecera y estado vacío.
- Extractable props: `hasRows` (boolean).
- Hardcoded: estructura semántica y estilos definidos por el tema.
