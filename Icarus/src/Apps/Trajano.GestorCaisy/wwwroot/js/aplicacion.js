/* Trajano GestorCaisy: comportamiento mínimo, sin dependencias.
   Confirmaciones para acciones sensibles, bloqueo de doble envío (el
   duplicate-submit lo refuerza la API con control de concurrencia) y
   manejo de filas del borrador. */
(function () {
    'use strict';

    document.addEventListener('submit', function (evento) {
        var forma = evento.target;
        if (!(forma instanceof HTMLFormElement)) return;
        var mensaje = forma.getAttribute('data-confirmar');
        if (mensaje && !window.confirm(mensaje)) {
            evento.preventDefault();
            return;
        }
        if (forma.dataset.enviando === 'true') {
            evento.preventDefault();
            return;
        }
        forma.dataset.enviando = 'true';
        window.setTimeout(function () {
            forma.querySelectorAll('button[type="submit"]').forEach(function (boton) {
                boton.disabled = true;
            });
        }, 0);
    }, true);

    var plantilla = document.getElementById('plantilla-detalle');
    var cuerpoFilas = document.getElementById('filas-detalle');
    if (plantilla && cuerpoFilas) {
        var renumerar = function () {
            cuerpoFilas
                .querySelectorAll('tr[data-fila-detalle]')
                .forEach(function (fila, indice) {
                    fila.querySelectorAll('input, select').forEach(function (control) {
                        control.name = control.name.replace(
                            /Detalles\[\d+\]/, 'Detalles[' + indice + ']');
                    });
                });
        };

        var botonAgregar = document.getElementById('agregar-detalle');
        if (botonAgregar) {
            botonAgregar.addEventListener('click', function () {
                var cantidad = cuerpoFilas.querySelectorAll('tr[data-fila-detalle]').length;
                cuerpoFilas.insertAdjacentHTML(
                    'beforeend',
                    plantilla.innerHTML.replaceAll('__i__', String(cantidad)));
            });
        }

        cuerpoFilas.addEventListener('click', function (evento) {
            var boton = evento.target.closest('[data-quitar-detalle]');
            if (!boton) return;
            var fila = boton.closest('tr[data-fila-detalle]');
            if (fila) fila.remove();
            renumerar();
        });
    }

    /* Sondeo del badge de novedades. El endpoint responde 304 cuando nada
       cambió, así que un sondeo sin noticias cuesta una respuesta vacía.
       Treinta segundos, igual que la PWA. */
    var contadorNovedades = document.querySelector('[data-contador-novedades]');
    if (contadorNovedades) {
        var urlContador = contadorNovedades.getAttribute('data-url-contador');
        window.setInterval(function () {
            /* Una pestaña oculta no necesita el badge al día. */
            if (document.hidden) return;
            window.fetch(urlContador, { credentials: 'same-origin' })
                .then(function (respuesta) {
                    return respuesta.ok ? respuesta.json() : null;
                })
                .then(function (datos) {
                    if (!datos) return;
                    contadorNovedades.textContent = String(datos.contador);
                })
                .catch(function () {
                    /* Un sondeo fallido no molesta al usuario: el siguiente
                       lo vuelve a intentar. */
                });
        }, 30000);
    }

    /* Campanita global + aviso del navegador (spec 2026-10-05). Un solo
       sondeo cada 30 s alimenta la campanita (siempre visible, no depende de
       ningún permiso) y, si Notification.permission ya es "granted", dispara
       además un aviso nativo. El correo de la cuenta viaja en el atributo
       title que _Layout.cshtml ya pone en .barra__cuenta: se reusa en vez de
       agregar un atributo nuevo. */
    var campana = document.querySelector('[data-campana]');
    if (campana) {
        var cuentaEl = document.querySelector('.barra__cuenta');
        var correo = cuentaEl ? cuentaEl.getAttribute('title') || '' : '';
        var fuentes = Array.prototype.slice.call(
            campana.querySelectorAll('[data-fuente-novedades]'));
        var botonCampana = campana.querySelector('[data-campana-boton]');
        var badge = campana.querySelector('[data-campana-badge]');
        var menu = campana.querySelector('[data-campana-menu]');

        var claveVisto = function (urlContador) {
            return 'campana-visto:' + correo + ':' + urlContador;
        };

        var ultimoVisto = function (urlContador) {
            var valor = window.localStorage.getItem(claveVisto(urlContador));
            return valor === null ? null : parseInt(valor, 10);
        };

        var guardarVisto = function (urlContador, contador) {
            window.localStorage.setItem(claveVisto(urlContador), String(contador));
        };

        var escaparHtml = function (texto) {
            var div = document.createElement('div');
            div.textContent = texto;
            return div.innerHTML;
        };

        var renderizarItem = function (item) {
            return '<li><a class="enlace" href="' + item.urlPagina + '">'
                + '<span class="chip chip--' + escaparHtml(item.chip) + '">'
                + escaparHtml(item.mensaje) + '</span> '
                + new Date(item.fechaUtc).toLocaleString('es-BO')
                + '</a></li>';
        };

        if (botonCampana && menu) {
            botonCampana.addEventListener('click', function () {
                if (menu.hasAttribute('hidden')) menu.removeAttribute('hidden');
                else menu.setAttribute('hidden', '');
            });
        }

        var sondearCampana = function () {
            if (document.hidden) return;
            var pendientes = fuentes.map(function (fuente) {
                var urlContador = fuente.getAttribute('data-fuente-novedades');
                var urlPagina = fuente.getAttribute('data-pagina');
                var dominio = fuente.getAttribute('data-dominio');
                return window.fetch(urlContador, { credentials: 'same-origin' })
                    .then(function (respuesta) {
                        return respuesta.ok ? respuesta.json() : null;
                    })
                    .then(function (datos) {
                        if (!datos) return { contador: 0, items: [] };
                        var visto = ultimoVisto(urlContador);
                        if (visto !== null && datos.contador > visto
                            && window.Notification
                            && window.Notification.permission === 'granted') {
                            var aviso = new window.Notification(
                                'Tienes nuevas novedades en ' + dominio,
                                {
                                    body: 'Hay novedades sin leer para revisar.',
                                    tag: urlContador,
                                });
                            aviso.onclick = function () {
                                window.focus();
                                window.location.href = urlPagina;
                            };
                        }
                        guardarVisto(urlContador, datos.contador);
                        var items = (datos.items || []).map(function (item) {
                            return {
                                mensaje: item.mensaje,
                                chip: item.chip,
                                fechaUtc: item.fechaUtc,
                                urlPagina: urlPagina,
                            };
                        });
                        return { contador: datos.contador, items: items };
                    })
                    .catch(function () {
                        /* Un sondeo fallido no molesta: el siguiente lo reintenta. */
                        return { contador: 0, items: [] };
                    });
            });

            Promise.all(pendientes).then(function (resultados) {
                var total = resultados.reduce(function (suma, r) {
                    return suma + r.contador;
                }, 0);
                if (badge) {
                    if (total > 0) {
                        badge.textContent = String(total);
                        badge.removeAttribute('hidden');
                    } else {
                        badge.setAttribute('hidden', '');
                    }
                }
                if (menu) {
                    var combinados = resultados
                        .reduce(function (acc, r) { return acc.concat(r.items); }, [])
                        .sort(function (a, b) {
                            return new Date(b.fechaUtc) - new Date(a.fechaUtc);
                        })
                        .slice(0, 5);
                    menu.innerHTML = '<ul class="novedades">' + (combinados.length
                        ? combinados.map(renderizarItem).join('')
                        : '<li class="vacio__texto">No hay notificaciones.</li>') + '</ul>';
                }
            });
        };

        sondearCampana();
        window.setInterval(sondearCampana, 30000);
    }

    /* Banner de permiso (spec 2026-10-05): se pide solo tras un clic
       explícito y nunca vuelve a insistir solo una vez que el gestor decide
       (conceder o negar), coherente con que el navegador tampoco deja volver
       a preguntar tras un rechazo. */
    var banner = document.querySelector('[data-banner-notificaciones]');
    if (banner && window.Notification) {
        var cuentaBanner = document.querySelector('.barra__cuenta');
        var correoBanner = cuentaBanner ? cuentaBanner.getAttribute('title') || '' : '';
        var claveBannerOculto = 'notificaciones-banner-oculto:' + correoBanner;
        var yaDecidido = window.Notification.permission !== 'default'
            || window.localStorage.getItem(claveBannerOculto) === 'true';
        if (!yaDecidido) {
            banner.removeAttribute('hidden');
            var botonActivar = banner.querySelector('[data-banner-activar]');
            if (botonActivar) {
                botonActivar.addEventListener('click', function () {
                    window.Notification.requestPermission().then(function () {
                        window.localStorage.setItem(claveBannerOculto, 'true');
                        banner.setAttribute('hidden', '');
                    });
                });
            }
        }
    }
})();
