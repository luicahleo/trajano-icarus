namespace Icarus.BuildingBlocks.Application.Observability;

public interface IRegistroVuelo
{
    IOperacionVuelo Iniciar(DescriptorOperacionRegistroVuelo descriptor);
    void Decidir(string operacion, string codigo, string resultado,
        IReadOnlyDictionary<string, object?>? campos = null);
    void PersistenciaCompletada(string contexto, int filas, long duracionMs);
    void PersistenciaFallida(string contexto, long duracionMs);
    void TransaccionTerminada(string contexto, bool confirmada);
    ICompensacionVuelo IniciarCompensacion(string operacion);
}

public interface IOperacionVuelo : IDisposable
{
    void Decidir(string codigo, string resultado, IReadOnlyDictionary<string, object?>? campos = null);
    void Completar();
    void Rechazar(string codigo);
    void Fallar();
}

public interface ICompensacionVuelo : IDisposable
{
    void Completar();
    void Fallar();
}
