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

public class FichaCartaTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();

    [Fact]
    public void Alergenos_ida_y_vuelta_entre_banderas_y_nombres()
    {
        var banderas = Alergenos.DeNombres(new[] { "Gluten", "lacteos", "desconocido" });
        banderas.Should().Be(Alergeno.Gluten | Alergeno.Lacteos);
        Alergenos.ANombres(banderas).Should().BeEquivalentTo(new[] { "Gluten", "Lacteos" });
    }

    [Fact]
    public void Fijar_foto_valida_la_guarda_y_quitar_la_borra()
    {
        var ficha = FichaCarta.Crear(Empresa, Guid.NewGuid(), Reloj);
        var datos = new byte[] { 1, 2, 3, 4 };

        ficha.FijarFoto(datos, "image/jpeg", Reloj).EsCorrecto.Should().BeTrue();
        ficha.TieneFoto.Should().BeTrue();
        ficha.FotoTipo.Should().Be("image/jpeg");

        ficha.QuitarFoto(Reloj);
        ficha.TieneFoto.Should().BeFalse();
    }

    [Fact]
    public void Destacados_recomendado_y_picante_se_fijan()
    {
        var ficha = FichaCarta.Crear(Empresa, Guid.NewGuid(), Reloj);
        ficha.Recomendado.Should().BeFalse();
        ficha.Picante.Should().BeFalse();

        ficha.FijarDestacados(recomendado: true, picante: true, Reloj);
        ficha.Recomendado.Should().BeTrue();
        ficha.Picante.Should().BeTrue();

        ficha.FijarDestacados(recomendado: false, picante: true, Reloj);
        ficha.Recomendado.Should().BeFalse();
        ficha.Picante.Should().BeTrue();
    }

    [Fact]
    public void Disponibilidad_marca_y_reactiva_el_plato()
    {
        var ficha = FichaCarta.Crear(Empresa, Guid.NewGuid(), Reloj);
        ficha.Agotado.Should().BeFalse();

        ficha.FijarDisponibilidad(agotado: true, Reloj);
        ficha.Agotado.Should().BeTrue();

        ficha.FijarDisponibilidad(agotado: false, Reloj);
        ficha.Agotado.Should().BeFalse();
    }

    [Fact]
    public void Fijar_foto_rechaza_tipo_no_valido_y_tamano_excesivo()
    {
        var ficha = FichaCarta.Crear(Empresa, Guid.NewGuid(), Reloj);

        ficha.FijarFoto(new byte[] { 1 }, "application/pdf", Reloj).Error.Codigo.Should().Be("ficha.foto_tipo");
        ficha.FijarFoto(new byte[FichaCarta.TamanoMaximoFoto + 1], "image/png", Reloj).Error.Codigo.Should().Be("ficha.foto_grande");
    }
}

public class ConfiguracionCartaTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();

    [Theory]
    [InlineData("noche", "noche")]
    [InlineData("MARINO", "marino")]
    [InlineData(" vino ", "vino")]
    [InlineData("inexistente", "verde")]
    [InlineData(null, "verde")]
    public void Normaliza_el_tema_a_uno_valido(string? entrada, string esperado)
    {
        TemasCarta.Normalizar(entrada).Should().Be(esperado);
    }

    [Fact]
    public void Crear_y_fijar_tema()
    {
        var cfg = ConfiguracionCarta.Crear(Empresa, "terracota", Reloj);
        cfg.Tema.Should().Be("terracota");

        cfg.FijarTema("raro", Reloj);
        cfg.Tema.Should().Be("verde"); // no válido → por defecto
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
