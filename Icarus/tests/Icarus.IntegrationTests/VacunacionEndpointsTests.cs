using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Icarus.Identity.Infrastructure;
using Xunit;

namespace Icarus.IntegrationTests;

// Flujo de vacunación de punta a punta (spec SP7 y spec 2026-10-30): el Gestor
// CAISY con GestorVeterinario prepara y publica el catálogo global, el
// cliente asigna/quita planes publicados y cancela, el trabajador con la
// funcionalidad Vacunacion ve la notificación y completa. El Administrador ya
// no participa del catálogo y el gestor nunca alcanza galpones ni tareas.
[Collection(IntegracionCollection.Nombre)]
public class VacunacionEndpointsTests
{
    private const string RutaGestion = "/api/programas-vacunacion-caisy";
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);
    private readonly IdentityFactory _factory;

    public VacunacionEndpointsTests(IdentityFactory factory) => _factory = factory;

    private async Task<string> LoginComo(string email)
    {
        using var cliente = _factory.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email, contrasena = IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage Autenticado(HttpMethod metodo, string url, string token, object? cuerpo = null)
    {
        var pedido = new HttpRequestMessage(metodo, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };
        if (cuerpo is not null) pedido.Content = JsonContent.Create(cuerpo);
        return pedido;
    }

    private async Task<HttpResponseMessage> Enviar(HttpMethod metodo, string url, string token, object? cuerpo = null)
    {
        using var cliente = _factory.CreateClient();
        return await cliente.SendAsync(Autenticado(metodo, url, token, cuerpo));
    }

    // Las cuentas CAISY las crea el Administrador, igual que el script
    // crear-usuario-gestor-veterinario.ps1.
    private async Task<string> CrearCuentaCaisy(params string[] funcionalidades)
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var email = $"caisy-{Guid.NewGuid():N}@icarus.test";
        var alta = await Enviar(HttpMethod.Post, "/api/usuarios-caisy/", admin, new
        {
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
            funcionalidades,
        });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        return await LoginComo(email);
    }

    private Task<string> CrearGestorVeterinario() => CrearCuentaCaisy("GestorVeterinario");

    private async Task<(Guid ClienteId, string Token)> CrearClienteAvicola()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var email = $"avicola-{Guid.NewGuid():N}@icarus.test";
        var alta = await Enviar(HttpMethod.Post, "/api/clientes", admin, new
        {
            razonSocial = "Avícola de Prueba S.A.C.",
            identificadorFiscal = $"3{Random.Shared.Next(100000000, 999999999)}",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var id = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var modulos = await Enviar(HttpMethod.Put, $"/api/clientes/{id}/modulos", admin,
            new { modulos = new[] { "GestionAvicola" } });
        Assert.Equal(HttpStatusCode.NoContent, modulos.StatusCode);
        return (id, await LoginComo(email));
    }

    private async Task<string> CrearTrabajador(Guid clienteId, string[] funcionalidades, string tokenCliente)
    {
        var email = $"trabajador-{Guid.NewGuid():N}@icarus.test";
        var alta = await Enviar(HttpMethod.Post, $"/api/clientes/{clienteId}/trabajadores", tokenCliente, new
        {
            nombre = "Nombre Ficticio",
            documentoIdentidad = $"8{Random.Shared.Next(10000000, 99999999)}",
            cargo = "Operario",
            fechaIngreso = "2026-01-15",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var trabajadorId = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var asignar = await Enviar(HttpMethod.Put,
            $"/api/clientes/{clienteId}/trabajadores/{trabajadorId}/funcionalidades", tokenCliente,
            new { funcionalidades });
        Assert.Equal(HttpStatusCode.NoContent, asignar.StatusCode);
        return await LoginComo(email);
    }

    private async Task<Guid> CrearGalpon(string tokenCliente, DateOnly nacimientoLote)
    {
        var granja = await Enviar(HttpMethod.Post, "/api/granjas", tokenCliente,
            new { nombre = $"Granja {Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, granja.StatusCode);
        var granjaId = (await granja.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var galpon = await Enviar(HttpMethod.Post, $"/api/granjas/{granjaId}/galpones", tokenCliente, new
        {
            numero = "1", capacidadMaxima = 5000, gallinasActuales = 1000,
            fechaNacimientoLote = nacimientoLote.ToString("yyyy-MM-dd"),
        });
        Assert.Equal(HttpStatusCode.Created, galpon.StatusCode);
        return (await galpon.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static MultipartFormDataContent ExcelCronograma(params (int Edad, string Vacuna)[] items) =>
        ExcelCronogramaDesde(items, new DateOnly?[items.Length]);

    private static MultipartFormDataContent ExcelCronogramaDesde((int Edad, string Vacuna)[] items, DateOnly inicio) =>
        ExcelCronogramaDesde(items, Enumerable.Repeat<DateOnly?>(inicio, items.Length).ToArray());

    private static MultipartFormDataContent ExcelCronogramaDesde((int Edad, string Vacuna)[] items, DateOnly?[] fechas)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Plan");
        hoja.Cell(1, 1).Value = "FECHA";
        hoja.Cell(1, 2).Value = "EDAD";
        hoja.Cell(1, 3).Value = "VACUNA";
        hoja.Cell(1, 4).Value = "MODO DE APLICACION";
        hoja.Cell(1, 5).Value = "OBSERVACIONES";
        var fila = 2;
        for (var i = 0; i < items.Length; i++)
        {
            if (i < fechas.Length && fechas[i] is { } fecha)
                hoja.Cell(fila, 1).Value = fecha.ToDateTime(TimeOnly.MinValue);
            hoja.Cell(fila, 2).Value = items[i].Edad;
            hoja.Cell(fila, 3).Value = items[i].Vacuna;
            fila++;
        }
        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        var contenido = new MultipartFormDataContent();
        contenido.Add(new ByteArrayContent(memoria.ToArray()), "archivo", "cronograma.xlsx");
        return contenido;
    }

    private async Task<Guid> CrearBorrador(string gestor)
    {
        var alta = await Enviar(HttpMethod.Post, RutaGestion, gestor, new
        {
            nombre = $"PLAN {Guid.NewGuid():N}",
            cantidadAves = 1000,
            observaciones = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        return (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> ImportarCronograma(string gestor, Guid programaId, MultipartFormDataContent contenido)
    {
        using var cliente = _factory.CreateClient();
        var importar = new HttpRequestMessage(HttpMethod.Post, $"{RutaGestion}/{programaId}/cronograma-excel")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", gestor) },
            Content = contenido,
        };
        return await cliente.SendAsync(importar);
    }

    private async Task<Guid> CrearProgramaPublicado(string gestor, params (int Edad, string Vacuna)[] items)
    {
        var programaId = await CrearBorrador(gestor);
        Assert.Equal(HttpStatusCode.OK, (await ImportarCronograma(gestor, programaId, ExcelCronograma(items))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await Enviar(HttpMethod.Post, $"{RutaGestion}/{programaId}/publicar", gestor)).StatusCode);
        return programaId;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage respuesta) =>
        await respuesta.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task FlujoCompletoGestorClienteTrabajador()
    {
        var gestor = await CrearGestorVeterinario();
        var programaId = await CrearProgramaPublicado(gestor, (3, "BIO COCCIVET R"), (10, "NEWCASTLE"));
        var (clienteId, tokenCliente) = await CrearClienteAvicola();

        // Cliente: galpón con lote de 3 días y asignación del plan publicado.
        var galponId = await CrearGalpon(tokenCliente, Hoy.AddDays(-3));
        var asignar = await Enviar(HttpMethod.Post, $"/api/galpones/{galponId}/plan-vacunacion", tokenCliente, new { programaId });
        Assert.Equal(HttpStatusCode.NoContent, asignar.StatusCode);

        // Notificación: la del día 3 vence hoy; la del día 10 entra en próximas.
        var notificacion = await Enviar(HttpMethod.Get, "/api/vacunacion/tareas", tokenCliente);
        Assert.Equal(HttpStatusCode.OK, notificacion.StatusCode);
        var cuerpo = await Json(notificacion);
        var vencida = cuerpo.GetProperty("vencidasYHoy").EnumerateArray().Single();
        Assert.Equal("BIO COCCIVET R", vencida.GetProperty("vacuna").GetString());
        Assert.Equal(Hoy.ToString("yyyy-MM-dd"), vencida.GetProperty("fechaProgramada").GetString());
        Assert.Equal("NEWCASTLE", cuerpo.GetProperty("proximas").EnumerateArray().Single().GetProperty("vacuna").GetString());

        // Trabajador con Vacunacion: ve la notificación y completa la tarea.
        var tokenTrabajador = await CrearTrabajador(clienteId, ["vacunacion"], tokenCliente);
        Assert.Equal(HttpStatusCode.OK, (await Enviar(HttpMethod.Get, "/api/vacunacion/tareas", tokenTrabajador)).StatusCode);
        var tareaId = vencida.GetProperty("id").GetGuid();
        var completar = await Enviar(HttpMethod.Post, $"/api/vacunacion/tareas/{tareaId}/completar", tokenTrabajador,
            new { fechaAplicacion = (string?)null, avesVacunadas = 950, observaciones = (string?)null });
        Assert.Equal(HttpStatusCode.NoContent, completar.StatusCode);

        // Segunda vez: 409 por estado (idempotencia natural, spec SP7).
        var repetir = await Enviar(HttpMethod.Post, $"/api/vacunacion/tareas/{tareaId}/completar", tokenTrabajador, new { });
        Assert.Equal(HttpStatusCode.Conflict, repetir.StatusCode);

        // Historial del galpón: la completada y la pendiente, con su estado.
        var tareas = (await Json(await Enviar(HttpMethod.Get, $"/api/galpones/{galponId}/vacunacion/tareas", tokenCliente)))
            .EnumerateArray().ToList();
        Assert.Equal(2, tareas.Count);
        Assert.Equal("Completada", tareas.Single(t => t.GetProperty("id").GetGuid() == tareaId).GetProperty("estado").GetString());

        // Reasignar otro plan: la pendiente anterior se desactiva y la
        // completada queda en el historial (nada se borra físicamente).
        var otroProgramaId = await CrearProgramaPublicado(gestor, (5, "GUMBORO"));
        var reasignar = await Enviar(HttpMethod.Post, $"/api/galpones/{galponId}/plan-vacunacion", tokenCliente,
            new { programaId = otroProgramaId });
        Assert.Equal(HttpStatusCode.NoContent, reasignar.StatusCode);
        var tareasTras = (await Json(await Enviar(HttpMethod.Get, $"/api/galpones/{galponId}/vacunacion/tareas", tokenCliente)))
            .EnumerateArray().ToList();
        Assert.Equal(2, tareasTras.Count);
        Assert.Contains(tareasTras, t => t.GetProperty("vacuna").GetString() == "GUMBORO");
        Assert.Contains(tareasTras, t => t.GetProperty("estado").GetString() == "Completada");

        // Quitar el plan: desactiva las pendientes, conserva el historial.
        Assert.Equal(HttpStatusCode.NoContent,
            (await Enviar(HttpMethod.Delete, $"/api/galpones/{galponId}/plan-vacunacion", tokenCliente)).StatusCode);
        var tareasFinales = (await Json(await Enviar(HttpMethod.Get, $"/api/galpones/{galponId}/vacunacion/tareas", tokenCliente)))
            .EnumerateArray().ToList();
        Assert.Single(tareasFinales);
        Assert.Equal("Completada", tareasFinales[0].GetProperty("estado").GetString());
    }

    [Fact]
    public async Task CicloDelBorradorYDelPublicadoEnLaGestion()
    {
        var gestor = await CrearGestorVeterinario();
        var programaId = await CrearBorrador(gestor);

        // Un borrador sin cronograma no se publica ni se desactiva.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Enviar(HttpMethod.Post, $"{RutaGestion}/{programaId}/publicar", gestor)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Enviar(HttpMethod.Post, $"{RutaGestion}/{programaId}/desactivar", gestor)).StatusCode);

        // El borrador se edita y recibe el cronograma.
        var nombre = $"PLAN EDITADO {Guid.NewGuid():N}";
        Assert.Equal(HttpStatusCode.NoContent, (await Enviar(HttpMethod.Put, $"{RutaGestion}/{programaId}", gestor,
            new { nombre, cantidadAves = 2000, observaciones = "Lote de recría" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await ImportarCronograma(gestor, programaId, ExcelCronograma((3, "BIO COCCIVET R")))).StatusCode);
        var borrador = await Json(await Enviar(HttpMethod.Get, $"{RutaGestion}/{programaId}", gestor));
        Assert.Equal("Borrador", borrador.GetProperty("estado").GetString());
        Assert.Equal(nombre, borrador.GetProperty("nombre").GetString());
        Assert.Equal(1, borrador.GetProperty("items").GetArrayLength());

        // Publicado: inmutable, no se elimina ni se publica otra vez.
        Assert.Equal(HttpStatusCode.NoContent,
            (await Enviar(HttpMethod.Post, $"{RutaGestion}/{programaId}/publicar", gestor)).StatusCode);
        Assert.Equal("Publicado",
            (await Json(await Enviar(HttpMethod.Get, $"{RutaGestion}/{programaId}", gestor))).GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await Enviar(HttpMethod.Put, $"{RutaGestion}/{programaId}", gestor,
            new { nombre, cantidadAves = 10, observaciones = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await ImportarCronograma(gestor, programaId, ExcelCronograma((7, "GUMBORO")))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Enviar(HttpMethod.Delete, $"{RutaGestion}/{programaId}", gestor)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Enviar(HttpMethod.Post, $"{RutaGestion}/{programaId}/publicar", gestor)).StatusCode);

        // Desactivar: el gestor lo sigue viendo, inactivo.
        Assert.Equal(HttpStatusCode.NoContent,
            (await Enviar(HttpMethod.Post, $"{RutaGestion}/{programaId}/desactivar", gestor)).StatusCode);
        var desactivado = await Json(await Enviar(HttpMethod.Get, $"{RutaGestion}/{programaId}", gestor));
        Assert.Equal("Publicado", desactivado.GetProperty("estado").GetString());
        Assert.False(desactivado.GetProperty("estaActivo").GetBoolean());

        // Otro borrador se elimina: desaparece y el reintento responde 404.
        var otroId = await CrearBorrador(gestor);
        Assert.Equal(HttpStatusCode.NoContent, (await Enviar(HttpMethod.Delete, $"{RutaGestion}/{otroId}", gestor)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Enviar(HttpMethod.Get, $"{RutaGestion}/{otroId}", gestor)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Enviar(HttpMethod.Delete, $"{RutaGestion}/{otroId}", gestor)).StatusCode);
    }

    [Fact]
    public async Task ElGestorVeElCatalogoCompletoYElTenantSoloLosPublicados()
    {
        var gestor = await CrearGestorVeterinario();
        var borradorId = await CrearBorrador(gestor);
        var publicadoId = await CrearProgramaPublicado(gestor, (3, "BIO COCCIVET R"));
        var desactivadoId = await CrearProgramaPublicado(gestor, (3, "BIO COCCIVET R"));
        Assert.Equal(HttpStatusCode.NoContent,
            (await Enviar(HttpMethod.Post, $"{RutaGestion}/{desactivadoId}/desactivar", gestor)).StatusCode);
        var eliminadoId = await CrearBorrador(gestor);
        Assert.Equal(HttpStatusCode.NoContent, (await Enviar(HttpMethod.Delete, $"{RutaGestion}/{eliminadoId}", gestor)).StatusCode);

        var deGestion = (await Json(await Enviar(HttpMethod.Get, RutaGestion, gestor))).EnumerateArray().ToList();
        JsonElement Fila(Guid id) => deGestion.Single(p => p.GetProperty("id").GetGuid() == id);
        Assert.Equal("Borrador", Fila(borradorId).GetProperty("estado").GetString());
        Assert.Equal(0, Fila(borradorId).GetProperty("cantidadItems").GetInt32());
        Assert.Equal("Publicado", Fila(publicadoId).GetProperty("estado").GetString());
        Assert.Equal(1, Fila(publicadoId).GetProperty("cantidadItems").GetInt32());
        Assert.False(Fila(desactivadoId).GetProperty("estaActivo").GetBoolean());
        Assert.DoesNotContain(deGestion, p => p.GetProperty("id").GetGuid() == eliminadoId);

        var (clienteId, tokenCliente) = await CrearClienteAvicola();
        var tokenTrabajador = await CrearTrabajador(clienteId, ["vacunacion"], tokenCliente);
        foreach (var token in new[] { tokenCliente, tokenTrabajador })
        {
            var visibles = (await Json(await Enviar(HttpMethod.Get, "/api/vacunacion/programas", token)))
                .EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToList();
            Assert.Contains(publicadoId, visibles);
            Assert.DoesNotContain(borradorId, visibles);
            Assert.DoesNotContain(desactivadoId, visibles);
            Assert.DoesNotContain(eliminadoId, visibles);
            Assert.Equal(HttpStatusCode.OK, (await Enviar(HttpMethod.Get, $"/api/vacunacion/programas/{publicadoId}", token)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Enviar(HttpMethod.Get, $"/api/vacunacion/programas/{borradorId}", token)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Enviar(HttpMethod.Get, $"/api/vacunacion/programas/{desactivadoId}", token)).StatusCode);
        }

        // Un borrador no es asignable: 404 genérico.
        var galponId = await CrearGalpon(tokenCliente, Hoy.AddDays(-3));
        var asignarBorrador = await Enviar(HttpMethod.Post, $"/api/galpones/{galponId}/plan-vacunacion", tokenCliente,
            new { programaId = borradorId });
        Assert.Equal(HttpStatusCode.NotFound, asignarBorrador.StatusCode);
    }

    [Fact]
    public async Task DesactivarProgramaQuitaElPlanVigenteDeLosGalpones()
    {
        var gestor = await CrearGestorVeterinario();
        var programaId = await CrearProgramaPublicado(gestor, (3, "BIO COCCIVET R"), (10, "NEWCASTLE"));
        var (_, tokenCliente) = await CrearClienteAvicola();
        var galponId = await CrearGalpon(tokenCliente, Hoy.AddDays(-3));
        Assert.Equal(HttpStatusCode.NoContent, (await Enviar(HttpMethod.Post,
            $"/api/galpones/{galponId}/plan-vacunacion", tokenCliente, new { programaId })).StatusCode);

        var antes = await Json(await Enviar(HttpMethod.Get, "/api/vacunacion/tareas", tokenCliente));
        Assert.Equal(2, antes.GetProperty("vencidasYHoy").GetArrayLength() + antes.GetProperty("proximas").GetArrayLength());

        // El gestor desactiva el programa: deja de ser vigente donde estaba asignado.
        Assert.Equal(HttpStatusCode.NoContent,
            (await Enviar(HttpMethod.Post, $"{RutaGestion}/{programaId}/desactivar", gestor)).StatusCode);

        Assert.Empty((await Json(await Enviar(HttpMethod.Get, $"/api/galpones/{galponId}/vacunacion/tareas", tokenCliente)))
            .EnumerateArray());
        var despues = await Json(await Enviar(HttpMethod.Get, "/api/vacunacion/tareas", tokenCliente));
        Assert.Equal(0, despues.GetProperty("vencidasYHoy").GetArrayLength() + despues.GetProperty("proximas").GetArrayLength());
    }

    [Fact]
    public async Task ImportarExcelEstableceLaFechaDeEmisionConLaPrimeraFecha()
    {
        var gestor = await CrearGestorVeterinario();
        var programaId = await CrearBorrador(gestor);
        Assert.Equal(JsonValueKind.Null,
            (await Json(await Enviar(HttpMethod.Get, $"{RutaGestion}/{programaId}", gestor))).GetProperty("fechaEmision").ValueKind);

        var importar = await ImportarCronograma(gestor, programaId,
            ExcelCronogramaDesde([(3, "BIO COCCIVET R"), (10, "NEWCASTLE")], new DateOnly(2026, 8, 6)));
        Assert.Equal(HttpStatusCode.OK, importar.StatusCode);
        Assert.Equal(2, (await Json(importar)).GetProperty("itemsImportados").GetInt32());

        var programa = await Json(await Enviar(HttpMethod.Get, $"{RutaGestion}/{programaId}", gestor));
        Assert.Equal("2026-08-06", programa.GetProperty("fechaEmision").GetString());
    }

    [Fact]
    public async Task AsignarPlanUsaLaFechaProgramadaDelExcel()
    {
        var gestor = await CrearGestorVeterinario();
        var programaId = await CrearBorrador(gestor);
        var fechas = new DateOnly?[] { new DateOnly(2026, 8, 6), new DateOnly(2026, 8, 8) };
        Assert.Equal(HttpStatusCode.OK, (await ImportarCronograma(gestor, programaId,
            ExcelCronogramaDesde([(3, "BIO COCCIVET R"), (10, "NEWCASTLE")], fechas))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await Enviar(HttpMethod.Post, $"{RutaGestion}/{programaId}/publicar", gestor)).StatusCode);

        // Galpón con un lote distinto al del plan: la tarea usa la fecha del Excel.
        var (_, tokenCliente) = await CrearClienteAvicola();
        var galponId = await CrearGalpon(tokenCliente, new DateOnly(2025, 9, 1));
        Assert.Equal(HttpStatusCode.NoContent, (await Enviar(HttpMethod.Post,
            $"/api/galpones/{galponId}/plan-vacunacion", tokenCliente, new { programaId })).StatusCode);

        var lista = (await Json(await Enviar(HttpMethod.Get, $"/api/galpones/{galponId}/vacunacion/tareas", tokenCliente)))
            .EnumerateArray().ToList();
        Assert.Equal("2026-08-06",
            lista.Single(t => t.GetProperty("edadDia").GetInt32() == 3).GetProperty("fechaProgramada").GetString());
        Assert.Equal("2026-08-08",
            lista.Single(t => t.GetProperty("edadDia").GetInt32() == 10).GetProperty("fechaProgramada").GetString());
    }

    [Fact]
    public async Task ImportacionConFilaInvalidaNoGuardaNadaYDevuelveElErrorPorFila()
    {
        var gestor = await CrearGestorVeterinario();
        var programaId = await CrearBorrador(gestor);

        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Plan");
        hoja.Cell(1, 1).Value = "FECHA";
        hoja.Cell(1, 2).Value = "EDAD";
        hoja.Cell(1, 3).Value = "VACUNA";
        hoja.Cell(2, 2).Value = "no-numero";
        hoja.Cell(2, 3).Value = "GUMBORO";
        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        var contenido = new MultipartFormDataContent();
        contenido.Add(new ByteArrayContent(memoria.ToArray()), "archivo", "malo.xlsx");

        var respuesta = await ImportarCronograma(gestor, programaId, contenido);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var errores = (await Json(respuesta)).GetProperty("errors").GetProperty("Cronograma")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("Fila 2: La edad debe ser un número entero mayor que cero.", errores);
        Assert.Equal(0, (await Json(await Enviar(HttpMethod.Get, $"{RutaGestion}/{programaId}", gestor)))
            .GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task ElAdministradorYaNoGestionaNiConsultaElCatalogo()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);

        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Post, RutaGestion, admin,
            new { nombre = "PLAN", cantidadAves = 100, observaciones = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Get, RutaGestion, admin)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Get, "/api/vacunacion/programas", admin)).StatusCode);
        // Las rutas de escritura del catálogo bajo /vacunacion/programas ya no existen.
        var crearViejo = await Enviar(HttpMethod.Post, "/api/vacunacion/programas", admin,
            new { nombre = "PLAN", cantidadAves = 100, observaciones = (string?)null });
        Assert.False(crearViejo.IsSuccessStatusCode);
        var desactivarViejo = await Enviar(HttpMethod.Delete, $"/api/vacunacion/programas/{Guid.NewGuid()}", admin);
        Assert.False(desactivarViejo.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ElGestorVeterinarioNuncaAlcanzaGalponesNiTareas()
    {
        var gestor = await CrearGestorVeterinario();
        var programaId = await CrearProgramaPublicado(gestor, (3, "BIO COCCIVET R"));
        var (_, tokenCliente) = await CrearClienteAvicola();
        var galponId = await CrearGalpon(tokenCliente, Hoy.AddDays(-3));
        Assert.Equal(HttpStatusCode.NoContent, (await Enviar(HttpMethod.Post,
            $"/api/galpones/{galponId}/plan-vacunacion", tokenCliente, new { programaId })).StatusCode);
        var tareaId = (await Json(await Enviar(HttpMethod.Get, "/api/vacunacion/tareas", tokenCliente)))
            .GetProperty("vencidasYHoy").EnumerateArray().First().GetProperty("id").GetGuid();

        // El filtro EF de tenant es permisivo sin ClienteId: la política es la
        // única barrera y debe responder 403 en todas estas rutas.
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Get, "/api/vacunacion/tareas", gestor)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Enviar(HttpMethod.Get, $"/api/galpones/{galponId}/vacunacion/tareas", gestor)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Get, $"/api/galpones/{galponId}", gestor)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Post,
            $"/api/galpones/{galponId}/plan-vacunacion", gestor, new { programaId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Enviar(HttpMethod.Delete, $"/api/galpones/{galponId}/plan-vacunacion", gestor)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Enviar(HttpMethod.Post, $"/api/vacunacion/tareas/{tareaId}/completar", gestor, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Get, "/api/vacunacion/programas", gestor)).StatusCode);
    }

    [Fact]
    public async Task SinLaFuncionVeterinariaNadieGestionaElCatalogo()
    {
        var (_, tokenCliente) = await CrearClienteAvicola();
        var gestorAlimento = await CrearCuentaCaisy("GestorPedidoAlimento");

        foreach (var token in new[] { tokenCliente, gestorAlimento })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Get, RutaGestion, token)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Post, RutaGestion, token,
                new { nombre = "PLAN", cantidadAves = 100, observaciones = (string?)null })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await Enviar(HttpMethod.Post, $"{RutaGestion}/{Guid.NewGuid()}/desactivar", token)).StatusCode);
        }
    }

    [Fact]
    public async Task TrabajadorSinVacunacionRecibe403()
    {
        var (clienteId, tokenCliente) = await CrearClienteAvicola();
        var tokenTrabajador = await CrearTrabajador(clienteId, ["produccionhuevos"], tokenCliente);

        var respuesta = await Enviar(HttpMethod.Get, "/api/vacunacion/tareas", tokenTrabajador);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task TrabajadorNoPuedeAsignarNiCancelar()
    {
        var gestor = await CrearGestorVeterinario();
        var programaId = await CrearProgramaPublicado(gestor, (3, "BIO COCCIVET R"));
        var (clienteId, tokenCliente) = await CrearClienteAvicola();
        var galponId = await CrearGalpon(tokenCliente, Hoy.AddDays(-3));
        await Enviar(HttpMethod.Post, $"/api/galpones/{galponId}/plan-vacunacion", tokenCliente, new { programaId });
        var tareaId = (await Json(await Enviar(HttpMethod.Get, "/api/vacunacion/tareas", tokenCliente)))
            .GetProperty("vencidasYHoy").EnumerateArray().First().GetProperty("id").GetGuid();
        var tokenTrabajador = await CrearTrabajador(clienteId, ["vacunacion"], tokenCliente);

        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Post,
            $"/api/galpones/{galponId}/plan-vacunacion", tokenTrabajador, new { programaId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Post,
            $"/api/vacunacion/tareas/{tareaId}/cancelar", tokenTrabajador, new { motivo = "no corresponde" })).StatusCode);
    }

    [Fact]
    public async Task TareaDeOtroTenantDevuelve404()
    {
        var gestor = await CrearGestorVeterinario();
        var programaId = await CrearProgramaPublicado(gestor, (3, "BIO COCCIVET R"));
        var (_, tokenA) = await CrearClienteAvicola();
        var (_, tokenB) = await CrearClienteAvicola();
        var galponId = await CrearGalpon(tokenA, Hoy.AddDays(-3));
        await Enviar(HttpMethod.Post, $"/api/galpones/{galponId}/plan-vacunacion", tokenA, new { programaId });
        var tareaId = (await Json(await Enviar(HttpMethod.Get, "/api/vacunacion/tareas", tokenA)))
            .GetProperty("vencidasYHoy").EnumerateArray().First().GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound,
            (await Enviar(HttpMethod.Post, $"/api/vacunacion/tareas/{tareaId}/completar", tokenB, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Enviar(HttpMethod.Get, $"/api/galpones/{galponId}/vacunacion/tareas", tokenB)).StatusCode);
    }

    [Fact]
    public async Task SinTokenDevuelve401()
    {
        using var cliente = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/vacunacion/tareas")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync(RutaGestion)).StatusCode);
    }
}
