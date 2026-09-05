using System.Security.Cryptography;
using FluentValidation;
using Icarus.GestionAvicola.Application.Documentos;
using Microsoft.Extensions.Configuration;
using SkiaSharp;

namespace Icarus.GestionAvicola.Infrastructure.Documentos;

// Volumen local privado para los respaldos de notas (spec SP8C): nombres
// físicos UUID, escritura atómica (archivo temporal + renombrado), original
// inmutable con hash SHA-256 y una copia de visualización segura reencodificada
// (orientación normalizada según EXIF, sin metadatos, compresión legible).
// El MIME se deduce de la firma del contenido y no del nombre declarado: un
// PDF renombrado a .jpg o un polyglot con datos sobrantes se rechazan. La
// vista derivada descarta segmentos de estructura, por lo que el contenido
// mostrado nunca transporta metadatos ni cargas ocultas; el original solo se
// sirve como adjunto autorizado.
public sealed class AlmacenDocumentosPedidoLocal : IAlmacenDocumentosPedido
{
    private readonly string _raiz;
    private readonly long _maxTamanoBytes;
    private readonly int _maxDimensiones;

    public AlmacenDocumentosPedidoLocal(IConfiguration configuracion)
    {
        var seccion = configuracion.GetSection(OpcionesAlmacenDocumentosPedido.Seccion);
        _raiz = seccion["Ruta"]
            ?? Path.Combine(AppContext.BaseDirectory, "documentos-pedidos");
        _maxTamanoBytes = seccion.GetValue<long?>("MaxTamanoBytes")
            ?? 5 * 1024 * 1024;
        _maxDimensiones = seccion.GetValue<int?>("MaxDimensionesPixeles")
            ?? 8000;
    }

    // Variante para pruebas: raiz y límites explícitos.
    internal AlmacenDocumentosPedidoLocal(string raiz, long maxTamanoBytes, int maxDimensiones)
    {
        _raiz = raiz;
        _maxTamanoBytes = maxTamanoBytes;
        _maxDimensiones = maxDimensiones;
    }

    public async Task<DocumentoAlmacenado> GuardarAsync(
        Stream contenido, CancellationToken cancellationToken = default)
    {
        using var memoria = new MemoryStream();
        await contenido.CopyToAsync(memoria, cancellationToken);
        var bytes = memoria.ToArray();
        if (bytes.Length == 0 || bytes.Length > _maxTamanoBytes)
            throw new ValidationException("El archivo supera el tamaño máximo permitido.");

        var mime = DetectarFirma(bytes);
        AsegurarSinDatosSobrantes(bytes);
        using (var bitmap = SKBitmap.Decode(bytes))
        {
            if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
                throw new ValidationException("El archivo no es una imagen permitida.");
            if (bitmap.Width > _maxDimensiones || bitmap.Height > _maxDimensiones)
                throw new ValidationException("La imagen supera las dimensiones máximas permitidas.");
        }

        var bytesVista = GenerarVistaSegura(bytes);
        var claveOriginal = Guid.NewGuid();
        var claveVista = Guid.NewGuid();

        // Escritura atómica: primero a un temporal del mismo volumen y luego
        // el renombrado, de modo que no queden archivos a medias si el
        // proceso muere a mitad de guardado.
        Directory.CreateDirectory(_raiz);
        await EscribirAtomicoAsync(RutaOriginal(claveOriginal), bytes, cancellationToken);
        await EscribirAtomicoAsync(RutaVista(claveVista), bytesVista, cancellationToken);

        return new DocumentoAlmacenado(
            claveOriginal,
            claveVista,
            Convert.ToHexString(SHA256.HashData(bytes)),
            mime,
            bytes.Length,
            bytesVista.Length);
    }

    public Task<Stream?> AbrirOriginalAsync(
        Guid clave, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult<Stream?>(Abrir(RutaOriginal(clave)));
    }

    public Task<Stream?> AbrirVistaAsync(
        Guid clave, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult<Stream?>(Abrir(RutaVista(clave)));
    }

    private static Stream? Abrir(string ruta) =>
        File.Exists(ruta) ? File.OpenRead(ruta) : null;

    // Orientación EXIF de un JPEG: recorre los segmentos hasta el SOS, busca
    // el APP1 "Exif\0\0" y lee la etiqueta 0x0112 del IFD0. PNG y WebP no
    // aplican orientación EXIF en la práctica: se devuelven tal cual.
    private static SKEncodedOrigin LeerOrientacionExif(byte[] bytes)
    {
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
            return SKEncodedOrigin.TopLeft;
        var i = 2;
        while (i + 4 <= bytes.Length)
        {
            if (bytes[i] != 0xFF)
                return SKEncodedOrigin.TopLeft;
            var marcador = bytes[i + 1];
            if (marcador is 0xD8 or 0x01 or 0xD9 or (>= 0xD0 and <= 0xD7))
            {
                i += 2;
                continue;
            }
            if (marcador == 0xDA)
                break;
            var longitud = (bytes[i + 2] << 8) | bytes[i + 3];
            if (marcador == 0xE1 && longitud >= 8
                && EsExif(bytes, i + 4)
                && LeerOrientacionTiff(bytes, i + 10, out var valor))
                return valor switch
                {
                    2 => SKEncodedOrigin.TopRight,
                    3 => SKEncodedOrigin.BottomRight,
                    4 => SKEncodedOrigin.BottomLeft,
                    5 => SKEncodedOrigin.LeftTop,
                    6 => SKEncodedOrigin.RightTop,
                    7 => SKEncodedOrigin.RightBottom,
                    8 => SKEncodedOrigin.LeftBottom,
                    _ => SKEncodedOrigin.TopLeft,
                };
            i += 2 + longitud;
        }
        return SKEncodedOrigin.TopLeft;
    }

    private static bool EsExif(byte[] bytes, int inicio) =>
        bytes[inicio] == (byte)'E' && bytes[inicio + 1] == (byte)'x'
            && bytes[inicio + 2] == (byte)'i' && bytes[inicio + 3] == (byte)'f'
            && bytes[inicio + 4] == 0x00 && bytes[inicio + 5] == 0x00;

    private static bool LeerOrientacionTiff(byte[] bytes, int inicioTiff, out ushort valor)
    {
        valor = 0;
        if (inicioTiff + 8 > bytes.Length)
            return false;
        var littleEndian = bytes[inicioTiff] == (byte)'I' && bytes[inicioTiff + 1] == (byte)'I';
        var bigEndian = bytes[inicioTiff] == (byte)'M' && bytes[inicioTiff + 1] == (byte)'M';
        if (!littleEndian && !bigEndian)
            return false;
        var offsetIfd = LeerEntero(bytes, inicioTiff + 4, 4, littleEndian);
        var inicioIfd = inicioTiff + (int)Math.Min(offsetIfd, 0x7FFFFFFF);
        if (inicioIfd + 2 > bytes.Length)
            return false;
        var cantidad = LeerEntero(bytes, inicioIfd, 2, littleEndian);
        for (var e = 0; e < cantidad; e++)
        {
            var inicioEntrada = inicioIfd + 2 + (e * 12);
            if (inicioEntrada + 12 > bytes.Length)
                return false;
            if (LeerEntero(bytes, inicioEntrada, 2, littleEndian) != 0x0112)
                continue;
            valor = (ushort)LeerEntero(bytes, inicioEntrada + 8, 2, littleEndian);
            return valor is >= 1 and <= 8;
        }
        return false;
    }

    private static uint LeerEntero(byte[] bytes, int inicio, int largo, bool littleEndian)
    {
        uint valor = 0;
        for (var i = 0; i < largo; i++)
        {
            var b = bytes[inicio + i];
            valor = littleEndian ? valor | ((uint)b << (i * 8)) : (valor << 8) | b;
        }
        return valor;
    }

    private string RutaOriginal(Guid clave) => Path.Combine(_raiz, clave.ToString("N") + ".bin");
    private string RutaVista(Guid clave) => Path.Combine(_raiz, clave.ToString("N") + ".jpg");

    private static async Task EscribirAtomicoAsync(string ruta, byte[] bytes, CancellationToken cancellationToken)
    {
        var temporal = ruta + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(temporal, bytes, cancellationToken);
        File.Move(temporal, ruta, false);
    }

    // Firma real del contenido: la extensión declarada no se consulta.
    private static string DetectarFirma(byte[] bytes)
    {
        if (bytes is [0xFF, 0xD8, ..])
            return "image/jpeg";
        if (bytes is [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, ..])
            return "image/png";
        if (bytes.Length > 12
            && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
            && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
            return "image/webp";
        throw new ValidationException("El archivo no es una imagen permitida.");
    }

    // Polyglots: una imagen no puede tener bytes después de su fin (EOI en
    // JPEG, IEND con su CRC en PNG) ni un contenedor RIFF con longitud que no
    // coincida con el archivo.
    private static void AsegurarSinDatosSobrantes(byte[] bytes)
    {
        if (bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            if (bytes[^2] == 0xFF && bytes[^1] == 0xD9)
                return;
        }
        else if (bytes[^12..].SequenceEqual(InicioFinPng))
            return;
        else if (bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F')
        {
            var longitudDeclarada = bytes[4] | (bytes[5] << 8) | (bytes[6] << 16) | (bytes[7] << 24);
            if (longitudDeclarada == bytes.Length - 8)
                return;
        }
        throw new ValidationException("El archivo contiene datos después del final de la imagen.");
    }

    private static readonly byte[] InicioFinPng =
    [
        0x00, 0x00, 0x00, 0x00, (byte)'I', (byte)'E', (byte)'N', (byte)'D',
        0xAE, 0x42, 0x60, 0x82,
    ];

    // Copia de visualización: descodifica, aplica la orientación EXIF y
    // reencodifica a JPEG (la reencodificación descarta todos los segmentos de
    // estructura: EXIF, GPS, comentarios y perfiles) con calidad legible.
    private static byte[] GenerarVistaSegura(byte[] bytes)
    {
        var origen = LeerOrientacionExif(bytes);
        using var bitmap = SKBitmap.Decode(bytes)
            ?? throw new ValidationException("El archivo no es una imagen permitida.");
        using var normalizada = AplicarOrientacion(bitmap, origen);
        using var imagen = SKImage.FromBitmap(normalizada);
        using var datos = imagen.Encode(SKEncodedImageFormat.Jpeg, 85);
        return datos.ToArray();
    }

    private static SKBitmap AplicarOrientacion(SKBitmap origen, SKEncodedOrigin orientacion)
    {
        var (ancho, alto) = orientacion is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom
                ? (origen.Height, origen.Width)
                : (origen.Width, origen.Height);
        var destino = new SKBitmap(ancho, alto);
        using var lienzo = new SKCanvas(destino);
        lienzo.SetMatrix(MatrizDe(orientacion, origen.Width, origen.Height));
        lienzo.DrawBitmap(origen, 0, 0);
        lienzo.Flush();
        return destino;
    }

    // Matrices afines por origen EXIF: u = a·x + c·y + e, v = b·x + d·y + f.
    private static SKMatrix MatrizDe(SKEncodedOrigin orientacion, int ancho, int alto) =>
        orientacion switch
        {
            SKEncodedOrigin.TopRight => Afine(-1, 0, ancho, 0, 1, 0),
            SKEncodedOrigin.BottomRight => Afine(-1, 0, ancho, 0, -1, alto),
            SKEncodedOrigin.BottomLeft => Afine(1, 0, 0, 0, -1, alto),
            SKEncodedOrigin.LeftTop => Afine(0, 1, 0, 1, 0, 0),
            SKEncodedOrigin.RightTop => Afine(0, -1, alto, 1, 0, 0),
            SKEncodedOrigin.RightBottom => Afine(0, -1, alto, -1, 0, ancho),
            SKEncodedOrigin.LeftBottom => Afine(0, 1, 0, -1, 0, ancho),
            _ => SKMatrix.CreateIdentity(),
        };

    private static SKMatrix Afine(float a, float c, float e, float b, float d, float f) =>
        new(a, c, e, b, d, f, 0, 0, 1);
}
