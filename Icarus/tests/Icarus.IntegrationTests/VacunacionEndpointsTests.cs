using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Icarus.Identity.Infrastructure;
using Xunit;

namespace Icarus.IntegrationTests;

// Flujo de vacunación de punta a punta (spec SP7): el Administrador gestiona
// el catálogo global, el cliente asigna/quita planes y cancela, el trabajador
// con la funcionalidad Vacunacion ve la notificación y completa.
[Collection(IntegracionCollection.Nombre)]
public class VacunacionEndpointsTests
{
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

    private async Task<(Guid ClienteId, string Token)> CrearClienteAvicola()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        using var cliente = _factory.CreateClient();
        var email = $"avicola-{Guid.NewGuid():N}@icarus.test";
        var alta = await cliente.SendAsync(Autenticado(HttpMethod.Post, "/api/clientes", admin, new
        {
            razonSocial = "Avícola de Prueba S.A.C.",
            identificadorFiscal = $"3{Random.Shared.Next(100000000, 999999999)}",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        }));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var id = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var modulos = await cliente.SendAsync(Autenticado(HttpMethod.Put, $"/api/clientes/{id}/modulos", admin,
            new { modulos = new[] { "GestionAvicola" } }));
        Assert.Equal(HttpStatusCode.NoContent, modulos.StatusCode);
        return (id, await LoginComo(email));
    }

    private async Task<string> CrearTrabajador(Guid clienteId, string[] funcionalidades, string tokenCliente)
    {
        using var cliente = _factory.CreateClient();
        var email = $"trabajador-{Guid.NewGuid():N}@icarus.test";
        var alta = await cliente.SendAsync(Autenticado(HttpMethod.Post, $"/api/clientes/{clienteId}/trabajadores",
            tokenCliente, new
            {
                nombre = "Nombre Ficticio",
                documentoIdentidad = $"8{Random.Shared.Next(10000000, 99999999)}",
                cargo = "Operario",
                fechaIngreso = "2026-01-15",
                email,
                contrasena = IdentityFactory.ContrasenaDePrueba,
            }));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var trabajadorId = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var asignar = await cliente.SendAsync(Autenticado(HttpMethod.Put,
            $"/api/clientes/{clienteId}/trabajadores/{trabajadorId}/funcionalidades", tokenCliente,
            new { funcionalidades }));
        Assert.Equal(HttpStatusCode.NoContent, asignar.StatusCode);
        return await LoginComo(email);
    }

    private async Task<Guid> CrearGalpon(string tokenCliente, DateOnly nacimientoLote)
    {
        using var cliente = _factory.CreateClient();
        var granja = await cliente.SendAsync(Autenticado(HttpMethod.Post, "/api/granjas", tokenCliente,
            new { nombre = $"Granja {Guid.NewGuid():N}" }));
        Assert.Equal(HttpStatusCode.Created, granja.StatusCode);
        var granjaId = (await granja.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var galpon = await cliente.SendAsync(Autenticado(HttpMethod.Post, $"/api/granjas/{granjaId}/galpones",
            tokenCliente, new
            {
                numero = "1", capacidadMaxima = 5000, gallinasActuales = 1000,
                fechaNacimientoLote = nacimientoLote.ToString("yyyy-MM-dd"),
            }));
        Assert.Equal(HttpStatusCode.Created, galpon.StatusCode);
        return (await galpon.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static MultipartFormDataContent ExcelCronograma(params (int Edad, string Vacuna)[] items)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Plan");
        hoja.Cell(1, 1).Value = "FECHA";
        hoja.Cell(1, 2).Value = "EDAD";
        hoja.Cell(1, 3).Value = "VACUNA";
        hoja.Cell(1, 4).Value = "MODO DE APLICACION";
        hoja.Cell(1, 5).Value = "OBSERVACIONES";
        var fila = 2;
        foreach (var (edad, vacuna) in items)
        {
            hoja.Cell(fila, 2).Value = edad;
            hoja.Cell(fila, 3).Value = vacuna;
            fila++;
        }
        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        var contenido = new MultipartFormDataContent();
        contenido.Add(new ByteArrayContent(memoria.ToArray()), "archivo", "cronograma.xlsx");
        return contenido;
    }

    private async Task<Guid> CrearProgramaConCronograma(string admin, params (int Edad, string Vacuna)[] items)
    {
        using var cliente = _factory.CreateClient();
        var alta = await cliente.SendAsync(Autenticado(HttpMethod.Post, "/api/vacunacion/programas", admin, new
        {
            nombre = $"PLAN {Guid.NewGuid():N}",
            fechaEmision = Hoy.ToString("yyyy-MM-dd"),
            cantidadAves = 1000,
            observaciones = (string?)null,
        }));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var programaId = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var importar = new HttpRequestMessage(HttpMethod.Post, $"/api/vacunacion/programas/{programaId}/cronograma-excel")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", admin) },
            Content = ExcelCronograma(items),
        };
        var respuesta = await cliente.SendAsync(importar);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return programaId;
    }

    [Fact]
    public async Task FlujoCompletoAdminClienteTrabajador()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var programaId = await CrearProgramaConCronograma(admin, (3, "BIO COCCIVET R"), (10, "NEWCASTLE"));
        var (clienteId, tokenCliente) = await CrearClienteAvicola();
        using var cliente = _factory.CreateClient();

        // Cliente: galpón con lote de 3 días y asignación del plan.
        var galponId = await CrearGalpon(tokenCliente, Hoy.AddDays(-3));
        var asignar = await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/api/galpones/{galponId}/plan-vacunacion", tokenCliente, new { programaId }));
        Assert.Equal(HttpStatusCode.NoContent, asignar.StatusCode);

        // Notificación: la del día 3 vence hoy; la del día 10 entra en próximas.
        var notificacion = await cliente.SendAsync(Autenticado(HttpMethod.Get, "/api/vacunacion/tareas", tokenCliente));
        Assert.Equal(HttpStatusCode.OK, notificacion.StatusCode);
        var cuerpo = await notificacion.Content.ReadFromJsonAsync<JsonElement>();
        var vencida = cuerpo.GetProperty("vencidasYHoy").EnumerateArray().Single();
        Assert.Equal("BIO COCCIVET R", vencida.GetProperty("vacuna").GetString());
        Assert.Equal(Hoy.ToString("yyyy-MM-dd"), vencida.GetProperty("fechaProgramada").GetString());
        Assert.Equal("NEWCASTLE", cuerpo.GetProperty("proximas").EnumerateArray().Single().GetProperty("vacuna").GetString());

        // Trabajador con Vacunacion: ve la notificación y completa la tarea.
        var tokenTrabajador = await CrearTrabajador(clienteId, ["vacunacion"], tokenCliente);
        var notificacionTrabajador = await cliente.SendAsync(
            Autenticado(HttpMethod.Get, "/api/vacunacion/tareas", tokenTrabajador));
        Assert.Equal(HttpStatusCode.OK, notificacionTrabajador.StatusCode);
        var tareaId = vencida.GetProperty("id").GetGuid();
        var completar = await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/api/vacunacion/tareas/{tareaId}/completar", tokenTrabajador,
            new { fechaAplicacion = (string?)null, avesVacunadas = 950, observaciones = (string?)null }));
        Assert.Equal(HttpStatusCode.NoContent, completar.StatusCode);

        // Segunda vez: 409 por estado (idempotencia natural, spec SP7).
        var repetir = await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/api/vacunacion/tareas/{tareaId}/completar", tokenTrabajador, new { }));
        Assert.Equal(HttpStatusCode.Conflict, repetir.StatusCode);

        // Historial del galpón: la completada y la pendiente, con su estado.
        var historial = await cliente.SendAsync(Autenticado(HttpMethod.Get,
            $"/api/galpones/{galponId}/vacunacion/tareas", tokenCliente));
        var tareas = (await historial.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.Equal(2, tareas.Count);
        Assert.Equal("Completada", tareas.Single(t => t.GetProperty("id").GetGuid() == tareaId).GetProperty("estado").GetString());

        // Reasignar otro plan: la pendiente anterior se desactiva y la
        // completada queda en el historial (nada se borra físicamente).
        var otroProgramaId = await CrearProgramaConCronograma(admin, (5, "GUMBORO"));
        var reasignar = await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/api/galpones/{galponId}/plan-vacunacion", tokenCliente, new { programaId = otroProgramaId }));
        Assert.Equal(HttpStatusCode.NoContent, reasignar.StatusCode);
        var historialTras = await cliente.SendAsync(Autenticado(HttpMethod.Get,
            $"/api/galpones/{galponId}/vacunacion/tareas", tokenCliente));
        var tareasTras = (await historialTras.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.Equal(2, tareasTras.Count);
        Assert.Contains(tareasTras, t => t.GetProperty("vacuna").GetString() == "GUMBORO");
        Assert.Contains(tareasTras, t => t.GetProperty("estado").GetString() == "Completada");

        // Quitar el plan: desactiva las pendientes, conserva el historial.
        var quitar = await cliente.SendAsync(Autenticado(HttpMethod.Delete,
            $"/api/galpones/{galponId}/plan-vacunacion", tokenCliente));
        Assert.Equal(HttpStatusCode.NoContent, quitar.StatusCode);
        var historialFinal = await cliente.SendAsync(Autenticado(HttpMethod.Get,
            $"/api/galpones/{galponId}/vacunacion/tareas", tokenCliente));
        var tareasFinales = (await historialFinal.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.Single(tareasFinales);
        Assert.Equal("Completada", tareasFinales[0].GetProperty("estado").GetString());
    }

    [Fact]
    public async Task ClienteNoGestionaElCatalogo()
    {
        var (_, tokenCliente) = await CrearClienteAvicola();
        using var cliente = _factory.CreateClient();

        var crear = await cliente.SendAsync(Autenticado(HttpMethod.Post, "/api/vacunacion/programas", tokenCliente, new
        {
            nombre = "PLAN", fechaEmision = Hoy.ToString("yyyy-MM-dd"), cantidadAves = 100, observaciones = (string?)null,
        }));
        Assert.Equal(HttpStatusCode.Forbidden, crear.StatusCode);
        var desactivar = await cliente.SendAsync(Autenticado(HttpMethod.Delete,
            $"/api/vacunacion/programas/{Guid.NewGuid()}", tokenCliente));
        Assert.Equal(HttpStatusCode.Forbidden, desactivar.StatusCode);
    }

    [Fact]
    public async Task AdminVeElCatalogoIncluyendoInactivosYElClienteNo()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var programaId = await CrearProgramaConCronograma(admin, (3, "BIO COCCIVET R"));
        using var cliente = _factory.CreateClient();
        var desactivar = await cliente.SendAsync(Autenticado(HttpMethod.Delete,
            $"/api/vacunacion/programas/{programaId}", admin));
        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);

        var listaAdmin = await cliente.SendAsync(Autenticado(HttpMethod.Get,
            "/api/vacunacion/programas?incluirInactivos=true", admin));
        Assert.Equal(HttpStatusCode.OK, listaAdmin.StatusCode);
        Assert.Contains((await listaAdmin.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray(),
            p => p.GetProperty("id").GetGuid() == programaId && !p.GetProperty("estaActivo").GetBoolean());

        var (_, tokenCliente) = await CrearClienteAvicola();
        var listaCliente = await cliente.SendAsync(Autenticado(HttpMethod.Get, "/api/vacunacion/programas", tokenCliente));
        Assert.Equal(HttpStatusCode.OK, listaCliente.StatusCode);
        Assert.DoesNotContain((await listaCliente.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray(),
            p => p.GetProperty("id").GetGuid() == programaId);
        // Aunque pida incluirInactivos, el handler solo lo honra al Administrador.
        var listaClienteInactivos = await cliente.SendAsync(Autenticado(HttpMethod.Get,
            "/api/vacunacion/programas?incluirInactivos=true", tokenCliente));
        Assert.DoesNotContain((await listaClienteInactivos.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray(),
            p => p.GetProperty("id").GetGuid() == programaId);
    }

    [Fact]
    public async Task TrabajadorSinVacunacionRecibe403()
    {
        var (clienteId, tokenCliente) = await CrearClienteAvicola();
        var tokenTrabajador = await CrearTrabajador(clienteId, ["produccionhuevos"], tokenCliente);
        using var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(Autenticado(HttpMethod.Get, "/api/vacunacion/tareas", tokenTrabajador));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task TrabajadorNoPuedeAsignarNiCancelar()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var programaId = await CrearProgramaConCronograma(admin, (3, "BIO COCCIVET R"));
        var (clienteId, tokenCliente) = await CrearClienteAvicola();
        using var cliente = _factory.CreateClient();
        var galponId = await CrearGalpon(tokenCliente, Hoy.AddDays(-3));
        await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/api/galpones/{galponId}/plan-vacunacion", tokenCliente, new { programaId }));
        var notificacion = await cliente.SendAsync(Autenticado(HttpMethod.Get, "/api/vacunacion/tareas", tokenCliente));
        var tareaId = (await notificacion.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("vencidasYHoy").EnumerateArray().First().GetProperty("id").GetGuid();
        var tokenTrabajador = await CrearTrabajador(clienteId, ["vacunacion"], tokenCliente);

        var asignar = await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/api/galpones/{galponId}/plan-vacunacion", tokenTrabajador, new { programaId }));
        Assert.Equal(HttpStatusCode.Forbidden, asignar.StatusCode);
        var cancelar = await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/api/vacunacion/tareas/{tareaId}/cancelar", tokenTrabajador, new { motivo = "no corresponde" }));
        Assert.Equal(HttpStatusCode.Forbidden, cancelar.StatusCode);
    }

    [Fact]
    public async Task TareaDeOtroTenantDevuelve404()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var programaId = await CrearProgramaConCronograma(admin, (3, "BIO COCCIVET R"));
        var (_, tokenA) = await CrearClienteAvicola();
        var (_, tokenB) = await CrearClienteAvicola();
        using var cliente = _factory.CreateClient();
        var galponId = await CrearGalpon(tokenA, Hoy.AddDays(-3));
        await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/api/galpones/{galponId}/plan-vacunacion", tokenA, new { programaId }));
        var notificacion = await cliente.SendAsync(Autenticado(HttpMethod.Get, "/api/vacunacion/tareas", tokenA));
        var tareaId = (await notificacion.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("vencidasYHoy").EnumerateArray().First().GetProperty("id").GetGuid();

        var completar = await cliente.SendAsync(Autenticado(HttpMethod.Post,
            $"/api/vacunacion/tareas/{tareaId}/completar", tokenB, new { }));
        Assert.Equal(HttpStatusCode.NotFound, completar.StatusCode);
        var historial = await cliente.SendAsync(Autenticado(HttpMethod.Get,
            $"/api/galpones/{galponId}/vacunacion/tareas", tokenB));
        Assert.Equal(HttpStatusCode.NotFound, historial.StatusCode);
    }

    [Fact]
    public async Task ImportacionConFilaInvalidaNoGuardaNada()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        using var cliente = _factory.CreateClient();
        var alta = await cliente.SendAsync(Autenticado(HttpMethod.Post, "/api/vacunacion/programas", admin, new
        {
            nombre = $"PLAN {Guid.NewGuid():N}",
            fechaEmision = Hoy.ToString("yyyy-MM-dd"),
            cantidadAves = 1000,
            observaciones = (string?)null,
        }));
        var programaId = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

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
        var importar = new HttpRequestMessage(HttpMethod.Post, $"/api/vacunacion/programas/{programaId}/cronograma-excel")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", admin) },
            Content = contenido,
        };

        var respuesta = await cliente.SendAsync(importar);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problema.GetProperty("errors").GetProperty("Cronograma").GetArrayLength() > 0);
        var detalle = await cliente.SendAsync(Autenticado(HttpMethod.Get,
            $"/api/vacunacion/programas/{programaId}", admin));
        Assert.Equal(0, (await detalle.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task SinTokenDevuelve401()
    {
        using var cliente = _factory.CreateClient();
        var respuesta = await cliente.GetAsync("/api/vacunacion/tareas");
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
