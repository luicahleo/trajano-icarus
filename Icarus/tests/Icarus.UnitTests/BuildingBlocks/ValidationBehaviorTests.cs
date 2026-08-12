using FluentValidation;
using Icarus.BuildingBlocks.Application.Behaviors;
using MediatR;
using Xunit;

namespace Icarus.UnitTests.BuildingBlocks;

public class ValidationBehaviorTests
{
    private sealed record SolicitudFalsa(string Nombre) : IRequest<string>;

    private sealed class ValidadorFalso : AbstractValidator<SolicitudFalsa>
    {
        public ValidadorFalso() => RuleFor(s => s.Nombre).NotEmpty();
    }

    [Fact]
    public async Task SolicitudValidaLlamaAlSiguiente()
    {
        var behavior = new ValidationBehavior<SolicitudFalsa, string>(new[] { new ValidadorFalso() });
        var resultado = await behavior.Handle(
            new SolicitudFalsa("ok"),
            () => Task.FromResult("respuesta"),
            CancellationToken.None);
        Assert.Equal("respuesta", resultado);
    }

    [Fact]
    public async Task SolicitudInvalidaLanzaValidationException()
    {
        var behavior = new ValidationBehavior<SolicitudFalsa, string>(new[] { new ValidadorFalso() });
        await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new SolicitudFalsa(""),
            () => Task.FromResult("respuesta"),
            CancellationToken.None));
    }

    [Fact]
    public async Task SinValidadoresLlamaAlSiguiente()
    {
        var behavior = new ValidationBehavior<SolicitudFalsa, string>(
            Enumerable.Empty<IValidator<SolicitudFalsa>>());
        var resultado = await behavior.Handle(
            new SolicitudFalsa(""),
            () => Task.FromResult("respuesta"),
            CancellationToken.None);
        Assert.Equal("respuesta", resultado);
    }
}
