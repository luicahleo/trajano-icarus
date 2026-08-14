using System.Text;
using Icarus.BuildingBlocks.Application;
using Icarus.Identity.Application.RegistroCuentas;
using Icarus.Identity.Application.Sesiones;
using Icarus.Identity.Domain;
using Icarus.Identity.Infrastructure.Autenticacion;
using Icarus.Identity.Infrastructure.Persistencia;
using Icarus.Identity.Infrastructure.RegistroCuentas;
using Icarus.Identity.Infrastructure.Usuarios;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Icarus.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentidadInfraestructura(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddDbContext<IdentityDbContext>(opciones =>
            opciones.UseSqlServer(configuracion.GetConnectionString("Icarus")));

        servicios.AddIdentityCore<Usuario>()
            .AddEntityFrameworkStores<IdentityDbContext>();

        servicios.Configure<OpcionesJwt>(configuracion.GetSection(OpcionesJwt.Seccion));

        servicios.AddScoped<IVerificadorCredenciales, VerificadorCredenciales>();
        servicios.AddScoped<IRegistradorUsuarios, RegistradorUsuarios>();
        servicios.AddScoped<IConsultaUsuarios, ConsultaUsuarios>();
        servicios.AddScoped<IEmisorAccessTokens, EmisorAccessTokens>();
        servicios.AddScoped<IServicioRefreshTokens, ServicioRefreshTokens>();

        var jwt = configuracion.GetSection(OpcionesJwt.Seccion).Get<OpcionesJwt>() ?? new OpcionesJwt();
        if (string.IsNullOrEmpty(jwt.Clave))
            throw new InvalidOperationException("Falta la configuración Jwt:Clave.");

        servicios.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opciones =>
            {
                // Sin mapeo de claims entrantes: "sub", "rol" y "clienteId"
                // llegan tal cual a CurrentUserService.
                opciones.MapInboundClaims = false;
                opciones.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Emisor,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audiencia,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Clave)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
            });

        servicios.AddAuthorizationBuilder()
            .AddPolicy(PoliticasAutorizacion.SoloAdministrador,
                politica => politica.RequireClaim(ClaimsIdentidad.Rol, nameof(Rol.Administrador)))
            .AddPolicy(PoliticasAutorizacion.GestionTrabajadores,
                politica => politica.RequireClaim(ClaimsIdentidad.Rol, nameof(Rol.Cliente)));

        return servicios;
    }
}
