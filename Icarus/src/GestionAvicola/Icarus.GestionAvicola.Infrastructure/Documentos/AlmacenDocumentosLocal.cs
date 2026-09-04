using Icarus.GestionAvicola.Application.PreciosAlimentos;
using Microsoft.Extensions.Configuration;

namespace Icarus.GestionAvicola.Infrastructure.Documentos;

// Volumen local privado para el PDF original (spec SP8): nombres físicos UUID,
// sin ruta ni URL en SQL; el contrato permite migrar a S3 sin tocar el dominio.
// El volumen forma parte del backup externo de la VPS (no es copia por sí solo).
public sealed class AlmacenDocumentosLocal : IAlmacenDocumentosPrecios
{
    private readonly string _raiz;

    public AlmacenDocumentosLocal(IConfiguration configuracion)
    {
        _raiz = configuracion["AlmacenDocumentos:Ruta"]
            ?? Path.Combine(AppContext.BaseDirectory, "documentos-privados");
    }

    public async Task<Guid> GuardarAsync(Stream contenido, CancellationToken cancellationToken = default)
    {
        var clave = Guid.NewGuid();
        Directory.CreateDirectory(_raiz);
        await using var archivo = File.Create(RutaDe(clave));
        await contenido.CopyToAsync(archivo, cancellationToken);
        return clave;
    }

    public Task<Stream?> AbrirAsync(Guid clave, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var ruta = RutaDe(clave);
        return Task.FromResult<Stream?>(File.Exists(ruta) ? File.OpenRead(ruta) : null);
    }

    private string RutaDe(Guid clave) => Path.Combine(_raiz, clave.ToString("N"));
}
