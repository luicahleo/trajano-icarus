using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Serilog;
using Trajano.GestorCaisy.Autenticacion;
using Trajano.GestorCaisy.Filtros;
using Trajano.GestorCaisy.Observabilidad;
using Trajano.GestorCaisy.Servicios;
using Trajano.GestorCaisy.Sesion;

var builder = WebApplication.CreateBuilder(args);

// Observabilidad propia (misma convención que Icarus.Host, sin referenciar el
// backend): sinks, niveles y enrichers viven en la sección Serilog de la
// configuración. Una caída de Seq no impide responder peticiones.
builder.Host.UseSerilog((context, services, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

// Cultura fija de la aplicación de oficina: formatos invariantes (punto
// decimal, fechas ISO en formularios) y textos en español.
var culturaInvariante = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = culturaInvariante;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("es");

builder.Services.AddControllersWithViews(opciones =>
    opciones.Filters.Add<FiltroErroresApi>());

// Texto español real en el HTML: el encoder conservador escapa ñ y acentos
// como referencias numéricas; con Latin-1 se renderizan tal cual.
builder.Services.AddSingleton(HtmlEncoder.Create(
    UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement));

builder.Services.AddAntiforgery(opciones =>
{
    opciones.Cookie.Name = "trajano_gestorcaisy_af";
    opciones.Cookie.HttpOnly = true;
    opciones.Cookie.SameSite = SameSiteMode.Strict;
    opciones.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Sesión de oficina: cookie de autenticación protegida con DataProtection y
// HttpOnly. El JWT de acceso y el refresh token viajan como claims cifrados,
// el navegador nunca los ve en claro ni se guardan en localStorage.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opciones =>
    {
        opciones.Cookie.Name = "trajano_gestorcaisy";
        opciones.Cookie.HttpOnly = true;
        opciones.Cookie.SameSite = SameSiteMode.Lax;
        opciones.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        opciones.Cookie.IsEssential = true;
        opciones.LoginPath = "/Sesion/Acceder";
        opciones.AccessDeniedPath = "/Sesion/Denegado";
        opciones.ExpireTimeSpan = TimeSpan.FromHours(8);
        opciones.SlidingExpiration = true;
    });

builder.Services.AddSingleton<IAuthorizationHandler, ManejadorRolYFuncionalidad>();
builder.Services.AddAuthorization(opciones =>
{
    opciones.AddPolicy(
        ConstantesAutorizacion.PoliticaGestorPedidoAlimento, politica =>
            politica.AddRequirements(new RequerimientoRolYFuncionalidad(
                ConstantesAutorizacion.RolGestorCaisy,
                ConstantesAutorizacion.BitGestorPedidoAlimento)));
    opciones.AddPolicy(
        ConstantesAutorizacion.PoliticaGestorRecepcionHuevos, politica =>
            politica.AddRequirements(new RequerimientoRolYFuncionalidad(
                ConstantesAutorizacion.RolGestorCaisy,
                ConstantesAutorizacion.BitGestorRecepcionHuevos)));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ISesionCaisyActual, SesionCaisyCookie>();
builder.Services.AddTransient<CorrelacionApiHandler>();
builder.Services.AddHttpClient<IApiIcarusClient, ApiIcarusClient>()
    .AddHttpMessageHandler<CorrelacionApiHandler>();

var app = builder.Build();

// Un solo resumen por petición externa; envuelve las reejecuciones de error y
// conserva el patrón de ruta original. Sin query, cuerpos ni excepción cruda.
app.UseMiddleware<ContextoPeticionMiddleware>();
app.UseSerilogRequestLogging(opciones =>
{
    RegistroHttpSeguro.Configurar(opciones);
    // El resumen usa el logger del host (DI), no el estático: evita que un
    // logger ajeno o en apagado silencie el resumen.
    opciones.Logger = app.Services.GetRequiredService<Serilog.ILogger>();
});
// Cierra los caminos en que el manejador de excepciones relanza (respuesta ya
// iniciada o fallo de la propia página de error): aborta sin fabricar otra
// respuesta y sin dejar la excepción cruda al resumen HTTP.
app.UseMiddleware<RespuestaErrorSeguraMiddleware>();
// SuppressDiagnosticsCallback: el diagnóstico propio (backend.error) ya es
// seguro, así que se suprime el evento redundante del framework con la
// excepción cruda. Las fuentes que igualmente relanzan quedan cubiertas por
// RespuestaErrorSeguraMiddleware y por la exclusión declarativa en appsettings.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    ExceptionHandlingPath = "/Sesion/Error",
    SuppressDiagnosticsCallback = _ => true,
});
app.UseMiddleware<ExcepcionesSegurasMiddleware>();
app.UseStatusCodePagesWithReExecute("/Sesion/Error", "?codigo={0}");
app.UseStaticFiles();
app.UseRouting();
app.UseMiddleware<ContextoRutaMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// Solo la aplicación de oficina de CAISY vive aquí: sin service worker, sin
// caché offline y sin IndexedDB (spec SP8: el pedido de alimento es online).
app.MapControllerRoute("default", "{controller=Precios}/{action=Index}/{id?}");

await app.RunAsync();

// Expone Program a WebApplicationFactory en las pruebas.
public partial class Program
{
    protected Program()
    {
    }
}
