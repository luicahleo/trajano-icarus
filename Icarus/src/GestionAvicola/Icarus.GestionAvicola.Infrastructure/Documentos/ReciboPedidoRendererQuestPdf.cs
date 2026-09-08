using Icarus.GestionAvicola.Application.PedidosAlimento;
using Icarus.GestionAvicola.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Icarus.GestionAvicola.Infrastructure.Documentos;

// Recibo imprimible (spec SP8D): un PDF de una página con los datos ya
// guardados del despacho, para que CAISY lo firme/selle en papel. No
// registra nada en el registro de vuelo por sí mismo — el handler que lo
// invoca es quien decide qué se audita.
//
// Licencia QuestPDF Community: gratuita mientras los ingresos anuales de la
// organización sean menores a USD 1M. Si eso deja de aplicar, migrar a una
// licencia paga o a PdfSharpCore.
public sealed class ReciboPedidoRendererQuestPdf : IReciboPedidoRenderer
{
    static ReciboPedidoRendererQuestPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> RenderizarAsync(PedidoAlimento pedido, CancellationToken cancellationToken = default)
    {
        var entrega = pedido.Entrega
            ?? throw new InvalidOperationException("El pedido no tiene entrega registrada.");
        var precios = pedido.Detalles.ToDictionary(d => d.TipoAlimento);

        var documento = Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(30);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(11));

                pagina.Header().Text("Recibo de despacho de alimento")
                    .SemiBold().FontSize(18);

                pagina.Content().Column(columna =>
                {
                    columna.Spacing(8);
                    columna.Item().Text($"Número de nota: {entrega.NumeroNota}");
                    columna.Item().Text($"Fecha de nota: {entrega.FechaNota:dd/MM/yyyy}");
                    columna.Item().Text($"Fecha de despacho: {entrega.FechaDespacho:dd/MM/yyyy}");

                    columna.Item().PaddingTop(10).Table(tabla =>
                    {
                        tabla.ColumnsDefinition(columnas =>
                        {
                            columnas.RelativeColumn(2);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1);
                        });
                        tabla.Header(encabezado =>
                        {
                            encabezado.Cell().Text("Tipo").SemiBold();
                            encabezado.Cell().Text("Entregado").SemiBold();
                            encabezado.Cell().Text("Precio / 40 kg").SemiBold();
                            encabezado.Cell().Text("Subtotal").SemiBold();
                        });
                        foreach (var linea in entrega.Lineas)
                        {
                            var detalle = precios.GetValueOrDefault(linea.TipoAlimento);
                            var subtotal = detalle?.PrecioFinalPor40Kg is { } precio
                                ? precio * linea.Equivalentes40Kg
                                : (decimal?)null;
                            tabla.Cell().Text(linea.TipoAlimento.ToString());
                            tabla.Cell().Text(linea.CantidadEntregada.ToString());
                            tabla.Cell().Text(detalle?.PrecioFinalPor40Kg?.ToString("0.00") ?? "—");
                            tabla.Cell().Text(subtotal?.ToString("0.00") ?? "—");
                        }
                    });

                    columna.Item().PaddingTop(10)
                        .Text($"Total informado (nota): {entrega.TotalNetoInformado?.ToString("0.00") ?? "—"}");
                    columna.Item()
                        .Text($"Total despachado (canónico): {pedido.Detalles.Sum(d => d.SubtotalSolicitado ?? 0):0.00}");

                    columna.Item().PaddingTop(30).Row(fila =>
                    {
                        fila.RelativeItem().Column(firma =>
                        {
                            firma.Item().PaddingTop(30).LineHorizontal(1);
                            firma.Item().Text("Firma y sello de CAISY");
                        });
                    });
                });
            });
        });

        return Task.FromResult(documento.GeneratePdf());
    }
}
