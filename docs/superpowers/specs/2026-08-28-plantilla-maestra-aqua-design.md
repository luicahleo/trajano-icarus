# Plantilla maestra aqua — diseño

## Objetivo

Convertir `AppLayout` en la plantilla maestra de las páginas autenticadas de Trajano-Icarus, con una navegación moderna, profesional, responsive y limitada a los módulos autorizados.

## Decisiones

- La identidad principal será azul aqua y se aplicará mediante `theme.ts`, sin colores locales por componente.
- En escritorio habrá navegación persistente de 248 px; por debajo de `md`, un `Drawer` temporal de 288 px.
- La marca visible será «Trajano Icarus».
- La navegación seguirá derivándose del rol y las funcionalidades actuales; no se inventan módulos ni datos de usuario.
- La ruta activa mostrará estado seleccionado y una cabecera contextual.
- `BannerSinConexion`, `Suspense` y `Outlet` permanecerán dentro del armazón estable.
- Las páginas conservarán sus `Container` actuales; la plantilla no añadirá una tarjeta ni padding global alrededor del contenido.
- Se añadirá un enlace «Saltar al contenido» y se usarán los comportamientos accesibles de MUI.
- El tema tendrá esquema claro y oscuro con tokens propios de marca y de tabla; arranca siguiendo
  la preferencia del sistema y un botón de la barra superior permite forzar uno de los dos,
  con la elección persistida por MUI en `localStorage`.

## Diseño visual

- Aqua oscuro para la banda de marca, aqua accesible para acciones y foco, aqua claro para selección.
- Superficies planas, bordes estructurales y sin sombras decorativas.
- Prompt para marca y títulos; Open Sans para navegación y contenido.
- Iconos Material de 22 px, siempre acompañados por texto.
- Terracota queda como acento secundario puntual.
- En el esquema oscuro el aqua se aclara para mantener el contraste sobre superficies profundas,
  y los componentes se pintan con variables CSS para no fijar colores de un solo esquema.

## Fuera de alcance

- Cambiar rutas, permisos o lógica de negocio.
- Añadir buscador, notificaciones, avatar, perfil o métricas ficticias.
- Rediseñar individualmente las páginas de cada módulo.
- Incorporar imágenes o nuevas dependencias.
