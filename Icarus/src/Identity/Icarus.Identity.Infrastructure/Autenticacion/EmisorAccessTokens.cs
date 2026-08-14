using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Icarus.Identity.Application.Sesiones;
using Icarus.Identity.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Icarus.Identity.Infrastructure.Autenticacion;

public sealed class EmisorAccessTokens : IEmisorAccessTokens
{
    private readonly OpcionesJwt _opciones;

    public EmisorAccessTokens(IOptions<OpcionesJwt> opciones) => _opciones = opciones.Value;

    public string Emitir(Guid usuarioId, string rol, Guid? clienteId, Guid? trabajadorId, out int expiraEnSegundos)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuarioId.ToString()),
            new(ClaimsIdentidad.Rol, rol),
        };
        if (clienteId is not null)
            claims.Add(new Claim(ClaimsIdentidad.ClienteId, clienteId.Value.ToString()));
        if (trabajadorId is not null)
            claims.Add(new Claim(ClaimsIdentidad.TrabajadorId, trabajadorId.Value.ToString()));

        expiraEnSegundos = _opciones.MinutosAccessToken * 60;
        var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Clave));
        var credenciales = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _opciones.Emisor,
            audience: _opciones.Audiencia,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_opciones.MinutosAccessToken),
            signingCredentials: credenciales);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
