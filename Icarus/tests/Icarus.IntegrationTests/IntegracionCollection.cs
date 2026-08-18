namespace Icarus.IntegrationTests;

// Un único host y contenedor SQL Server para toda la suite. Las clases de una
// colección xUnit no se ejecutan en paralelo, evitando que cada clase levante
// un SQL Server y agote la memoria de Docker al crecer la suite.
[CollectionDefinition(Nombre)]
public sealed class IntegracionCollection : ICollectionFixture<IdentityFactory>
{
    public const string Nombre = "Integración con SQL Server";
}
