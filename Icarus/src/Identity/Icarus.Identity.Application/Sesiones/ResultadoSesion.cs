namespace Icarus.Identity.Application.Sesiones;

// El RefreshToken viaja solo hasta el endpoint, que lo deja en cookie HttpOnly
// y nunca se expone en logs ni en el cuerpo de la respuesta.
public sealed record ResultadoSesion(string AccessToken, string RefreshToken, int ExpiraEnSegundos);
