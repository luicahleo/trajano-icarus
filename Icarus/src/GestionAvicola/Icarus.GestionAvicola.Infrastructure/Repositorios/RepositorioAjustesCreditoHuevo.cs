using Icarus.GestionAvicola.Application.PreciosHuevo;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

public sealed class RepositorioAjustesCreditoHuevo(GestionAvicolaDbContext db) : IRepositorioAjustesCreditoHuevo
{
    public void Agregar(AjusteCreditoHuevo ajuste) => db.AjustesCreditoHuevo.Add(ajuste);
}
