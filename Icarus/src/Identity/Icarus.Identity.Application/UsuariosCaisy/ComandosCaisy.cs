using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.Identity.Domain;
using MediatR;

namespace Icarus.Identity.Application.UsuariosCaisy;

// Administración de cuentas CAISY por el Administrador de plataforma (spec
// SP8). Registro de vuelo sin PII: solo ids técnicos y conteos; nunca el
// correo, la contraseña ni los nombres de funcionalidad.
public sealed record CrearUsuarioCaisyCommand(
    string Email, string Contrasena, IReadOnlyList<string> Funcionalidades)
    : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "identity.usuarios_caisy.crear",
        new Dictionary<string, DatoRegistroVuelo> { ["CantidadFuncionalidades"] = DatoRegistroVuelo.Entero });
}

public sealed record DesactivarUsuarioCaisyCommand(Guid UsuarioId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "identity.usuarios_caisy.desactivar",
        new Dictionary<string, DatoRegistroVuelo>());
}

public sealed record DefinirFuncionalidadesCaisyCommand(
    Guid UsuarioId, IReadOnlyList<string> Funcionalidades)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "identity.usuarios_caisy.definir_funcionalidades",
        new Dictionary<string, DatoRegistroVuelo> { ["CantidadFuncionalidades"] = DatoRegistroVuelo.Entero });
}

public sealed record ListarUsuariosCaisyQuery
    : IRequest<IReadOnlyList<UsuarioCaisyResumen>>;

public sealed record UsuarioCaisyResumen(
    Guid Id, string Email, bool Activo, IReadOnlyList<string> Funcionalidades);

public sealed class CrearUsuarioCaisyValidator : AbstractValidator<CrearUsuarioCaisyCommand>
{
    public CrearUsuarioCaisyValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(c => c.Contrasena).NotEmpty().MinimumLength(8);
        RuleFor(c => c.Funcionalidades).NotNull();
        RuleForEach(c => c.Funcionalidades)
            .Must(ReglasFuncionalidadesCaisy.EsValida)
            .WithMessage("Funcionalidad de CAISY no definida.");
    }
}

public sealed class DefinirFuncionalidadesCaisyValidator : AbstractValidator<DefinirFuncionalidadesCaisyCommand>
{
    public DefinirFuncionalidadesCaisyValidator()
    {
        RuleFor(c => c.Funcionalidades).NotNull();
        RuleForEach(c => c.Funcionalidades)
            .Must(ReglasFuncionalidadesCaisy.EsValida)
            .WithMessage("Funcionalidad de CAISY no definida.");
    }
}

public sealed class CrearUsuarioCaisyHandler(ICuentasCaisy cuentas)
    : IRequestHandler<CrearUsuarioCaisyCommand, Guid>
{
    public async Task<Guid> Handle(CrearUsuarioCaisyCommand request, CancellationToken cancellationToken)
    {
        var funcionalidades = ReglasFuncionalidadesCaisy.Combinar(request.Funcionalidades);
        var cuentaId = await cuentas.CrearAsync(
            request.Email.Trim(), request.Contrasena, funcionalidades, cancellationToken);
        if (cuentaId is null)
            throw new ConflictException("No se pudo registrar la cuenta de CAISY.");
        return cuentaId.Value;
    }
}

public sealed class DesactivarUsuarioCaisyHandler(ICuentasCaisy cuentas)
    : IRequestHandler<DesactivarUsuarioCaisyCommand>
{
    public async Task Handle(DesactivarUsuarioCaisyCommand request, CancellationToken cancellationToken)
    {
        if (!await cuentas.DesactivarAsync(request.UsuarioId, cancellationToken))
            throw new NotFoundException("Cuenta de CAISY", request.UsuarioId);
    }
}

public sealed class DefinirFuncionalidadesCaisyHandler(ICuentasCaisy cuentas)
    : IRequestHandler<DefinirFuncionalidadesCaisyCommand>
{
    public async Task Handle(DefinirFuncionalidadesCaisyCommand request, CancellationToken cancellationToken)
    {
        var funcionalidades = ReglasFuncionalidadesCaisy.Combinar(request.Funcionalidades);
        if (!await cuentas.DefinirFuncionalidadesAsync(request.UsuarioId, funcionalidades, cancellationToken))
            throw new NotFoundException("Cuenta de CAISY", request.UsuarioId);
    }
}

// El listado administrativo incluye el correo: identifica las cuentas en la
// pantalla del Administrador, pero no viaja a Seq (anti-PII).
public sealed class ListarUsuariosCaisyHandler(ICuentasCaisy cuentas)
    : IRequestHandler<ListarUsuariosCaisyQuery, IReadOnlyList<UsuarioCaisyResumen>>
{
    public Task<IReadOnlyList<UsuarioCaisyResumen>> Handle(
        ListarUsuariosCaisyQuery request, CancellationToken cancellationToken) =>
        cuentas.ListarAsync(cancellationToken);
}
