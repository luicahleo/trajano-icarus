using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Behaviors;
using Icarus.BuildingBlocks.Observability;
using Icarus.Host.Endpoints;
using Icarus.Host.Servicios;
using Icarus.Identity.Application.Sesiones;
using Icarus.Identity.Infrastructure;
using Icarus.Identity.Infrastructure.Persistencia;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservabilidad();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserService>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(IniciarSesionCommand).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(IniciarSesionCommand).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddIdentidadInfraestructura(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { estado = "ok" }));
app.MapIdentidad();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    // Migra y siembra las cuentas de prueba por rol (dev y tests de integración).
    // En Testing la factory inyecta la cadena de conexión y las claves fijas.
    using var alcance = app.Services.CreateScope();
    var db = alcance.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
    await SemillaIdentidad.SembrarAsync(
        alcance.ServiceProvider,
        app.Configuration["Semilla:ContrasenaPrueba"] ?? "Solo-Desarrollo-123");
}

await app.RunAsync();

// Expone Program a WebApplicationFactory en los tests de integración.
public partial class Program
{
    protected Program() { }
}
