using System.Globalization;
using System.Security.Cryptography;
using FluentValidation;
using Icarus.GestionAvicola.Application.Documentos;
using Icarus.GestionAvicola.Infrastructure.Documentos;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP8C Tarea 2 (spec: "Documentos privados"): el respaldo de la nota en papel
// se guarda en un volumen privado detrás de IAlmacenDocumentosPedido. El
// original queda inmutable con su hash SHA-256 (valor probatorio) y se genera
// una copia de visualización segura: orientación normalizada, sin metadatos y
// comprimida de forma legible. Solo se aceptan imágenes reales (firma/MIME,
// tamaño, dimensiones), rechazando polyglots y extensiones falsas. Los
// nombres físicos son UUID y la escritura es atómica.
public class AlmacenDocumentosPedidoTests : IDisposable
{
    private readonly string _raiz = Path.Combine(
        Path.GetTempPath(), "icarus-almacen-" + Guid.NewGuid().ToString("N"));

    private AlmacenDocumentosPedidoLocal CrearAlmacen(
        long maxTamanoBytes = 5 * 1024 * 1024, int maxDimensiones = 8000)
    {
        Directory.CreateDirectory(_raiz);
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["AlmacenDocumentosPedido:Ruta"] = _raiz,
                ["AlmacenDocumentosPedido:MaxTamanoBytes"] = maxTamanoBytes.ToString(CultureInfo.InvariantCulture),
                ["AlmacenDocumentosPedido:MaxDimensionesPixeles"] = maxDimensiones.ToString(CultureInfo.InvariantCulture),
            }).Build();
        return new AlmacenDocumentosPedidoLocal(configuracion);
    }

    private static byte[] ImagenPng(int ancho = 2, int alto = 1)
    {
        using var bitmap = new SKBitmap(ancho, alto);
        bitmap.Erase(SKColors.Red);
        using var imagen = SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(SKEncodedImageFormat.Png, 90);
        return datos.ToArray();
    }

    private static byte[] ImagenJpeg(int ancho = 2, int alto = 1)
    {
        using var bitmap = new SKBitmap(ancho, alto);
        bitmap.Erase(SKColors.Blue);
        using var imagen = SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(SKEncodedImageFormat.Jpeg, 90);
        return datos.ToArray();
    }

    // Inserta un segmento APP1 con EXIF mínimo (solo la orientación) tras el
    // SOI del JPEG: la estructura TIFF little-endian que Skia interpreta.
    private static byte[] JpegConOrientacion(byte[] jpeg, ushort orientacion)
    {
        var tiff = new List<byte>
        {
            (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, // cabecera + offset IFD
            0x01, 0x00, // una entrada en IFD0
        };
        tiff.AddRange([0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00]); // tag 0x0112, SHORT, 1
        tiff.AddRange([(byte)(orientacion & 0xFF), (byte)(orientacion >> 8), 0x00, 0x00]);
        tiff.AddRange([0x00, 0x00, 0x00, 0x00]); // sin IFD1

        var payload = new List<byte> { (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00 };
        payload.AddRange(tiff);
        var longitud = payload.Count + 2;

        var resultado = new List<byte>
        {
            jpeg[0], jpeg[1], 0xFF, 0xE1, (byte)(longitud >> 8), (byte)(longitud & 0xFF),
        };
        resultado.AddRange(payload);
        resultado.AddRange(jpeg[2..]);
        return [.. resultado];
    }

    private static byte[] ConComentarioJpeg(byte[] jpeg, string comentario)
    {
        var payload = System.Text.Encoding.ASCII.GetBytes(comentario);
        var longitud = payload.Length + 2;
        var resultado = new List<byte>
        {
            jpeg[0], jpeg[1], 0xFF, 0xFE, (byte)(longitud >> 8), (byte)(longitud & 0xFF),
        };
        resultado.AddRange(payload);
        resultado.AddRange(jpeg[2..]);
        return [.. resultado];
    }

    private static (int Ancho, int Alto) Dimensiones(byte[] imagen)
    {
        using var bitmap = SKBitmap.Decode(imagen);
        return (bitmap!.Width, bitmap.Height);
    }

    // Recorre los marcadores JPEG y devuelve los de estructura (0xE0-0xEF y 0xFE).
    private static HashSet<byte> MarcadoresEstructura(byte[] jpeg)
    {
        var marcadores = new HashSet<byte>();
        var i = 2;
        while (i + 4 <= jpeg.Length)
        {
            if (jpeg[i] != 0xFF)
                break;
            var marcador = jpeg[i + 1];
            if (marcador == 0xD8 || marcador == 0xD9 || marcador is 0x01 || (marcador >= 0xD0 && marcador <= 0xD7))
            {
                i += 2;
                continue;
            }
            if (marcador == 0xDA)
                break;
            if (marcador is >= 0xE0 and <= 0xEF || marcador == 0xFE)
                marcadores.Add(marcador);
            var longitud = (jpeg[i + 2] << 8) | jpeg[i + 3];
            i += 2 + longitud;
        }
        return marcadores;
    }

    [Fact]
    public async Task RechazaUnArchivoQueNoEsImagenPermitida()
    {
        var almacen = CrearAlmacen();
        var pdf = "%PDF-1.7 contenido de prueba"u8.ToArray();

        var excepcion = await Assert.ThrowsAsync<ValidationException>(() =>
            almacen.GuardarAsync(new MemoryStream(pdf)));

        Assert.Equal("El archivo no es una imagen permitida.", excepcion.Message);
    }

    [Fact]
    public async Task RechazaUnArchivoQueExcedeElTamanioMaximo()
    {
        var almacen = CrearAlmacen(maxTamanoBytes: 10);
        var imagen = ImagenPng();

        var excepcion = await Assert.ThrowsAsync<ValidationException>(() =>
            almacen.GuardarAsync(new MemoryStream(imagen)));

        Assert.Equal("El archivo supera el tamaño máximo permitido.", excepcion.Message);
    }

    [Fact]
    public async Task RechazaDimensionesQueExcedenElLimite()
    {
        var almacen = CrearAlmacen(maxDimensiones: 1);
        var imagen = ImagenPng(ancho: 2, alto: 1);

        var excepcion = await Assert.ThrowsAsync<ValidationException>(() =>
            almacen.GuardarAsync(new MemoryStream(imagen)));

        Assert.Equal("La imagen supera las dimensiones máximas permitidas.", excepcion.Message);
    }

    [Fact]
    public async Task RechazaUnPolyglotPngConDatosDespuesDelIend()
    {
        var almacen = CrearAlmacen();
        var imagen = ImagenPng();
        var poliglota = imagen.Concat("<?php echo 1; ?>"u8.ToArray()).ToArray();

        var excepcion = await Assert.ThrowsAsync<ValidationException>(() =>
            almacen.GuardarAsync(new MemoryStream(poliglota)));

        Assert.Equal("El archivo contiene datos después del final de la imagen.", excepcion.Message);
    }

    [Fact]
    public async Task RechazaUnPolyglotJpegConBasuraDespuesDelFinal()
    {
        var almacen = CrearAlmacen();
        var imagen = ImagenJpeg();
        var poliglota = imagen.Concat("<script>"u8.ToArray()).ToArray();

        var excepcion = await Assert.ThrowsAsync<ValidationException>(() =>
            almacen.GuardarAsync(new MemoryStream(poliglota)));

        Assert.Equal("El archivo contiene datos después del final de la imagen.", excepcion.Message);
    }

    [Fact]
    public async Task AceptaJpegPngYWebpReales()
    {
        var almacen = CrearAlmacen();

        var jpeg = await almacen.GuardarAsync(new MemoryStream(ImagenJpeg()));
        var png = await almacen.GuardarAsync(new MemoryStream(ImagenPng()));
        using (var bitmap = new SKBitmap(1, 1))
        using (var datos = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Webp, 80))
            _ = await almacen.GuardarAsync(new MemoryStream(datos.ToArray()));

        Assert.Equal("image/jpeg", jpeg.Mime);
        Assert.Equal("image/png", png.Mime);
    }

    [Fact]
    public async Task GuardaElOriginalInmutableConHashYClavesUuid()
    {
        var almacen = CrearAlmacen();
        var imagen = ImagenPng();

        var guardado = await almacen.GuardarAsync(new MemoryStream(imagen));

        Assert.NotEqual(Guid.Empty, guardado.ClaveOriginal);
        Assert.NotEqual(Guid.Empty, guardado.ClaveVista);
        Assert.NotEqual(guardado.ClaveOriginal, guardado.ClaveVista);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(imagen)),
            guardado.HashSha256);
        Assert.Equal(imagen.Length, guardado.TamanoOriginalBytes);

        await using var original = await almacen.AbrirOriginalAsync(guardado.ClaveOriginal)!;
        Assert.NotNull(original);
        using var memoria = new MemoryStream();
        await original!.CopyToAsync(memoria);
        Assert.Equal(imagen, memoria.ToArray());

        // La vista derivada es una imagen válida (JPEG reencodificado).
        await using var vista = await almacen.AbrirVistaAsync(guardado.ClaveVista)!;
        Assert.NotNull(vista);
        var bytesVista = new MemoryStream();
        await vista!.CopyToAsync(bytesVista);
        Assert.Equal(0xFF, bytesVista.ToArray()[0]);
        Assert.Equal(0xD8, bytesVista.ToArray()[1]);

        // Solo los dos archivos de UUID: sin temporales residuales ni rutas previsibles.
        var archivos = Directory.GetFiles(_raiz);
        Assert.Equal(2, archivos.Length);
        Assert.All(archivos, a =>
            Assert.True(Guid.TryParseExact(
                Path.GetFileNameWithoutExtension(a), "N", out _)));
    }

    [Fact]
    public async Task LaVistaSeguraEliminaLosMetadatos()
    {
        var almacen = CrearAlmacen();
        var jpeg = ConComentarioJpeg(
            JpegConOrientacion(ImagenJpeg(), 6), "comentario con autor y GPS de ejemplo");

        var guardado = await almacen.GuardarAsync(new MemoryStream(jpeg));

        await using var vista = await almacen.AbrirVistaAsync(guardado.ClaveVista)!;
        var bytesVista = new MemoryStream();
        await vista!.CopyToAsync(bytesVista);
        var marcadores = MarcadoresEstructura(bytesVista.ToArray());
        // Sin APP1 (EXIF) ni COM (comentarios); el APP0 JFIF mínimo que escribe
        // el codificador no transporta datos sensibles.
        Assert.False(marcadores.Contains(0xE1), "La vista conserva metadatos EXIF.");
        Assert.False(marcadores.Contains(0xFE), "La vista conserva comentarios.");
    }

    [Fact]
    public async Task LaVistaSeguraNormalizaLaOrientacion()
    {
        var almacen = CrearAlmacen();
        var jpeg = JpegConOrientacion(ImagenJpeg(ancho: 3, alto: 2), 6);

        var guardado = await almacen.GuardarAsync(new MemoryStream(jpeg));

        await using var vista = await almacen.AbrirVistaAsync(guardado.ClaveVista)!;
        var bytesVista = new MemoryStream();
        await vista!.CopyToAsync(bytesVista);
        var (ancho, alto) = Dimensiones(bytesVista.ToArray());
        Assert.Equal(2, ancho);
        Assert.Equal(3, alto);

        // El original se conserva tal cual se recibió: valor probatorio.
        await using var original = await almacen.AbrirOriginalAsync(guardado.ClaveOriginal)!;
        var bytesOriginal = new MemoryStream();
        await original!.CopyToAsync(bytesOriginal);
        Assert.Equal(jpeg, bytesOriginal.ToArray());
    }

    [Fact]
    public async Task LosArchivosAusentesDevuelvenNullSinRutasEnLosErrores()
    {
        var almacen = CrearAlmacen();

        Assert.Null(await almacen.AbrirOriginalAsync(Guid.NewGuid()));
        Assert.Null(await almacen.AbrirVistaAsync(Guid.NewGuid()));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && Directory.Exists(_raiz))
            Directory.Delete(_raiz, true);
    }
}
