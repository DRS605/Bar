using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Hosteleria.Tests;

public class PedidoWebTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid Mesa = Guid.NewGuid();

    private static (Guid, string, decimal, string?) Item(string desc, decimal cant = 1m, string? nota = null) =>
        (Guid.NewGuid(), desc, cant, nota);

    [Fact]
    public void Crear_registra_pendiente_con_lineas_y_evento()
    {
        var pedido = PedidoWeb.Crear(Empresa, Mesa, IdiomaCarta.En, new[] { Item("Beer", 2m, "cold"), Item("Tortilla") }, Reloj);

        pedido.EsCorrecto.Should().BeTrue();
        pedido.Valor.Estado.Should().Be(EstadoPedidoWeb.Pendiente);
        pedido.Valor.Idioma.Should().Be(IdiomaCarta.En);
        pedido.Valor.Lineas.Should().HaveCount(2);
        pedido.Valor.Lineas[0].Nota.Should().Be("cold");
        pedido.Valor.EventosDominio.Should().ContainSingle(e => e is PedidoWebRecibido);
    }

    [Fact]
    public void Crear_sin_lineas_falla()
    {
        PedidoWeb.Crear(Empresa, Mesa, IdiomaCarta.Es, Array.Empty<(Guid, string, decimal, string?)>(), Reloj)
            .Error.Codigo.Should().Be("pedido_web.sin_lineas");
    }

    [Fact]
    public void Crear_descarta_las_lineas_con_cantidad_cero()
    {
        var pedido = PedidoWeb.Crear(Empresa, Mesa, IdiomaCarta.Es, new[] { Item("Caña", 1m), Item("Vacía", 0m) }, Reloj);

        pedido.EsCorrecto.Should().BeTrue();
        pedido.Valor.Lineas.Should().ContainSingle(l => l.Descripcion == "Caña");
    }

    [Fact]
    public void Aceptar_marca_aceptado_y_guarda_la_comanda()
    {
        var pedido = PedidoWeb.Crear(Empresa, Mesa, IdiomaCarta.Es, new[] { Item("Caña") }, Reloj).Valor;
        var comandaId = Guid.NewGuid();

        pedido.Aceptar(comandaId, Reloj).EsCorrecto.Should().BeTrue();
        pedido.Estado.Should().Be(EstadoPedidoWeb.Aceptado);
        pedido.ComandaId.Should().Be(comandaId);
        pedido.ResueltoEn.Should().NotBeNull();

        // No se puede aceptar ni rechazar dos veces.
        pedido.Aceptar(Guid.NewGuid(), Reloj).Error.Codigo.Should().Be("pedido_web.no_pendiente");
        pedido.Rechazar(Reloj).Error.Codigo.Should().Be("pedido_web.no_pendiente");
    }

    [Fact]
    public void Rechazar_marca_rechazado()
    {
        var pedido = PedidoWeb.Crear(Empresa, Mesa, IdiomaCarta.Es, new[] { Item("Caña") }, Reloj).Valor;

        pedido.Rechazar(Reloj).EsCorrecto.Should().BeTrue();
        pedido.Estado.Should().Be(EstadoPedidoWeb.Rechazado);
    }
}

public class AvisoMesaTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid Mesa = Guid.NewGuid();

    [Fact]
    public void Crear_deja_el_aviso_pendiente_y_registra_evento()
    {
        var aviso = AvisoMesa.Crear(Empresa, Mesa, TipoAvisoMesa.LlamarCamarero, Reloj);

        aviso.Tipo.Should().Be(TipoAvisoMesa.LlamarCamarero);
        aviso.Pendiente.Should().BeTrue();
        aviso.EventosDominio.Should().ContainSingle(e => e is AvisoMesaRecibido);
    }

    [Fact]
    public void Atender_marca_atendido_y_no_se_repite()
    {
        var aviso = AvisoMesa.Crear(Empresa, Mesa, TipoAvisoMesa.PedirCuenta, Reloj);

        aviso.Atender(Reloj).EsCorrecto.Should().BeTrue();
        aviso.Pendiente.Should().BeFalse();
        aviso.Atender(Reloj).Error.Codigo.Should().Be("aviso.ya_atendido");
    }
}

public class TokenCartaMesaTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();

    [Fact]
    public void Crear_mesa_genera_un_token_de_carta()
    {
        var mesa = Mesa.Crear(Empresa, "Mesa 1", null, 4, Reloj).Valor;
        mesa.TokenCarta.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Regenerar_token_lo_cambia()
    {
        var mesa = Mesa.Crear(Empresa, "Mesa 1", null, 4, Reloj).Valor;
        var anterior = mesa.TokenCarta;

        mesa.RegenerarTokenCarta(Reloj);
        mesa.TokenCarta.Should().NotBe(anterior).And.NotBe(Guid.Empty);
    }
}
