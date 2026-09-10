using Icarus.GestionAvicola.Application.DespachosHuevo;
using Icarus.GestionAvicola.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Icarus.GestionAvicola.Infrastructure.Documentos;

public sealed class ReciboDespachoHuevoRendererQuestPdf : IReciboDespachoHuevoRenderer
{
    static ReciboDespachoHuevoRendererQuestPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> RenderizarAsync(DespachoHuevo despacho, CancellationToken cancellationToken = default)
    {
        var documento = Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(30);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(11));

                pagina.Header().Text("Recibo de recepción de huevo").SemiBold().FontSize(18);

                pagina.Content().Column(columna =>
                {
                    columna.Spacing(8);
                    columna.Item().Text($"Fecha de despacho: {despacho.FechaDespacho:dd/MM/yyyy}");
                    columna.Item().Text($"Fecha de recepción: {despacho.FechaRecepcion:dd/MM/yyyy}");

                    columna.Item().PaddingTop(10).Table(tabla =>
                    {
                        tabla.ColumnsDefinition(columnas =>
                        {
                            columnas.RelativeColumn(2);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1);
                        });
                        tabla.Header(encabezado =>
                        {
                            encabezado.Cell().Text("Tamaño").SemiBold();
                            encabezado.Cell().Text("Amarras").SemiBold();
                            encabezado.Cell().Text("Huevos").SemiBold();
                            encabezado.Cell().Text("Precio").SemiBold();
                            encabezado.Cell().Text("Subtotal").SemiBold();
                        });
                        foreach (var linea in despacho.Detalles.OrderBy(d => d.Tamano))
                        {
                            tabla.Cell().Text(linea.Tamano.ToString());
                            tabla.Cell().Text($"{linea.CantidadAmarras} + {linea.UnidadesSueltas}");
                            tabla.Cell().Text(linea.CantidadHuevos.ToString());
                            tabla.Cell().Text(linea.PrecioUnitarioCongelado?.ToString("0.0000") ?? "—");
                            tabla.Cell().Text(linea.Subtotal?.ToString("0.00") ?? "—");
                        }
                    });

                    columna.Item().PaddingTop(10)
                        .Text($"Total amarras: {despacho.TotalAmarras}    Total huevos: {despacho.TotalHuevos}");
                    columna.Item()
                        .Text($"Total Bs: {despacho.TotalBs?.ToString("0.00") ?? "—"}").SemiBold();

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
