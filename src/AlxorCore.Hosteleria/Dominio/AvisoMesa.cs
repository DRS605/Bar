using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Hosteleria.Dominio;

/// <summary>Tipo de aviso que un cliente lanza desde la mesa con su móvil.</summary>
public enum TipoAvisoMesa
{
    /// <summary>«Llamar al camarero».</summary>
    LlamarCamarero = 1,

    /// <summary>«Pedir la cuenta».</summary>
    PedirCuenta = 2,
}

/// <summary>Un cliente ha lanzado un aviso desde una mesa (pendiente de atender).</summary>
public sealed record AvisoMesaRecibido(Guid AvisoId, Guid EmpresaId, Guid MesaId, TipoAvisoMesa Tipo, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>
/// Aviso ligero que un cliente lanza desde la mesa por QR: «llamar al camarero» o «pedir la cuenta».
/// Aparece en Barra/Salón hasta que el personal lo <see cref="Atender"/>.
/// </summary>
public sealed class AvisoMesa : RaizAgregadoEmpresa<Guid>
{
    private AvisoMesa(Guid id)
        : base(id, Guid.Empty)
    {
    }

    private AvisoMesa(Guid id, Guid empresaId, Guid mesaId, TipoAvisoMesa tipo, DateTimeOffset ahora)
        : base(id, empresaId)
    {
        MesaId = mesaId;
        Tipo = tipo;
        RecibidoEn = ahora;
    }

    /// <summary>Mesa desde la que se lanzó el aviso.</summary>
    public Guid MesaId { get; private set; }

    /// <summary>Qué pidió el cliente (llamar al camarero o pedir la cuenta).</summary>
    public TipoAvisoMesa Tipo { get; private set; }

    public DateTimeOffset RecibidoEn { get; private set; }

    /// <summary>Momento en que el personal lo atendió (nulo mientras está pendiente).</summary>
    public DateTimeOffset? AtendidoEn { get; private set; }

    /// <summary>Si el aviso sigue pendiente de atender.</summary>
    public bool Pendiente => AtendidoEn is null;

    public static AvisoMesa Crear(Guid empresaId, Guid mesaId, TipoAvisoMesa tipo, IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        var aviso = new AvisoMesa(Guid.NewGuid(), empresaId, mesaId, tipo, reloj.AhoraUtc);
        aviso.RegistrarEvento(new AvisoMesaRecibido(aviso.Id, empresaId, mesaId, tipo, reloj.AhoraUtc));
        return aviso;
    }

    /// <summary>Marca el aviso como atendido.</summary>
    public Resultado Atender(IReloj reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        if (AtendidoEn is not null)
        {
            return Resultado.Fallo(Error.Conflicto("aviso.ya_atendido", "El aviso ya estaba atendido."));
        }

        AtendidoEn = reloj.AhoraUtc;
        return Resultado.Ok();
    }
}
