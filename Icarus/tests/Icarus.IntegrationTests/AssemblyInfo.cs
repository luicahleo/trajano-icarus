using Xunit;

// Red de seguridad para clases futuras: aunque olviden unirse a la colección
// compartida, nunca podrán levantar otro SQL Server en paralelo.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
