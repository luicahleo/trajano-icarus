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
})();
