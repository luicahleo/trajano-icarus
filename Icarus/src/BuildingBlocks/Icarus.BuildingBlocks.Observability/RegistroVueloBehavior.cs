using FluentValidation;
using Icarus.BuildingBlocks.Application.Observability;
using MediatR;

namespace Icarus.BuildingBlocks.Observability;

public sealed class RegistroVueloBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IRegistroVuelo _registro;

    public RegistroVueloBehavior(IRegistroVuelo registro) => _registro = registro;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IOperacionRegistrable registrable)
            return await next();

        using var operacion = _registro.Iniciar(registrable.Registro);
        try
        {
            var respuesta = await next();
            operacion.Completar();
            return respuesta;
        }
        catch (ValidationException)
        {
            operacion.Rechazar("validation_failed");
            throw;
        }
        catch (Icarus.BuildingBlocks.Domain.DomainException)
        {
            operacion.Rechazar("business_rejected");
            throw;
        }
        catch
        {
            operacion.Fallar();
            throw;
        }
    }

}
