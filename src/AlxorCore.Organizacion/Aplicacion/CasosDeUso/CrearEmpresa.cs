using AlxorCore.Nucleo.Comun;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;
using AlxorCore.Organizacion.Aplicacion.Modelos;
using AlxorCore.Organizacion.Aplicacion.Puertos;
using AlxorCore.Organizacion.Dominio;

namespace AlxorCore.Organizacion.Aplicacion.CasosDeUso;

/// <summary>Datos para dar de alta una empresa.</summary>
public sealed record CrearEmpresaComando(
    Guid UsuarioId,
    string Nif,
    string RazonSocial,
    string? Calle = null,
    string? CodigoPostal = null,
    string? Poblacion = null,
    string? Provincia = null,
    RegimenIva RegimenIva = RegimenIva.General);

/// <summary>
/// Caso de uso: crear una empresa. El usuario que la crea se convierte en <b>Propietario</b>.
/// La serie de facturación por defecto se crea de forma perezosa la primera vez que se factura
/// (ver <c>IServicioNumeracion</c>), momento en el que la empresa ya está activa en el contexto.
/// </summary>
public sealed class CrearEmpresa
{
    private readonly IRepositorioEmpresas _empresas;
    private readonly IRepositorioMembresias _membresias;
    private readonly IUnidadDeTrabajoOrganizacion _unidadDeTrabajo;
    private readonly IReloj _reloj;

    public CrearEmpresa(
        IRepositorioEmpresas empresas,
        IRepositorioMembresias membresias,
        IUnidadDeTrabajoOrganizacion unidadDeTrabajo,
        IReloj reloj)
    {
        _empresas = empresas;
        _membresias = membresias;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    public async Task<Resultado<EmpresaDto>> EjecutarAsync(CrearEmpresaComando comando, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var nif = Nif.Crear(comando.Nif);
        if (nif.EsFallo)
        {
            return Resultado.Fallo<EmpresaDto>(nif.Error);
        }

        if (await _empresas.ExisteNifAsync(nif.Valor.Valor, ct).ConfigureAwait(false))
        {
            return Resultado.Fallo<EmpresaDto>(Error.Conflicto("empresa.nif_en_uso", "Ya existe una empresa con ese NIF."));
        }

        var direccion = Direccion.Crear(comando.Calle, comando.CodigoPostal, comando.Poblacion, comando.Provincia);

        var empresa = Empresa.Crear(nif.Valor, comando.RazonSocial, direccion, comando.RegimenIva, _reloj);
        if (empresa.EsFallo)
        {
            return Resultado.Fallo<EmpresaDto>(empresa.Error);
        }

        var membresia = Membresia.CrearPropietario(comando.UsuarioId, empresa.Valor.Id, _reloj);

        _empresas.Agregar(empresa.Valor);
        _membresias.Agregar(membresia);
        await _unidadDeTrabajo.GuardarCambiosAsync(ct).ConfigureAwait(false);

        return Resultado.Ok(EmpresaDto.Desde(empresa.Valor));
    }
}
