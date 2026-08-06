using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Aplicacion.Modelos;
using AlxorCore.Organizacion.Aplicacion.Puertos;

namespace AlxorCore.Organizacion.Aplicacion.CasosDeUso;

/// <summary>Datos de cobro por domiciliación de la empresa (IBAN de ingreso e identificador del acreedor SEPA).</summary>
public sealed record DatosCobroComando(string? Iban, string? IdentificadorAcreedor);

/// <summary>Caso de uso: fijar los datos de cobro (SEPA) de la empresa activa.</summary>
public sealed class ActualizarDatosCobro
{
    private readonly IRepositorioEmpresas _empresas;
    private readonly IUnidadDeTrabajoOrganizacion _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public ActualizarDatosCobro(IRepositorioEmpresas empresas, IUnidadDeTrabajoOrganizacion unidadDeTrabajo, IReloj reloj)
    {
        _empresas = empresas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<EmpresaDto>> EjecutarAsync(Guid empresaId, DatosCobroComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var empresa = await _empresas.ObtenerPorIdAsync(empresaId, ct).ConfigureAwait(false);
        if (empresa is null)
        {
            return Resultado.Fallo<EmpresaDto>(Error.NoEncontrado("empresa.no_encontrada", "La empresa no existe."));
        }

        empresa.EstablecerDatosCobro(comando.Iban, comando.IdentificadorAcreedor, _reloj);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);
        return Resultado.Ok(EmpresaDto.Desde(empresa));
    }
}
