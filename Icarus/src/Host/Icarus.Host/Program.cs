using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Observability;
using Icarus.Host.Servicios;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservabilidad();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserService>();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapGet("/health", () => Results.Ok(new { estado = "ok" }));

await app.RunAsync();

// Expone Program a WebApplicationFactory en los tests de integración.
public partial class Program
{
    protected Program() { }
}
