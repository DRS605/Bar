using AlxorCore.Hosteleria.Dominio;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Hosteleria.Tests;

public sealed class RelojFijo : IReloj
{
    public DateTimeOffset AhoraUtc { get; init; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
}

public class MesaTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();

    [Fact]
    public void Crear_mesa_valida()
    {
        var mesa = Mesa.Crear(Empresa, "  Mesa 1  ", " Terraza ", 4, Reloj);

        mesa.EsCorrecto.Should().BeTrue();
        mesa.Valor.Nombre.Should().Be("Mesa 1");
        mesa.Valor.Zona.Should().Be("Terraza");
        mesa.Valor.Capacidad.Should().Be(4);
        mesa.Valor.Activa.Should().BeTrue();
    }

    [Fact]
    public void Crear_mesa_sin_nombre_falla()
    {
        Mesa.Crear(Empresa, "   ", null, 2, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Crear_mesa_con_capacidad_negativa_falla()
    {
        Mesa.Crear(Empresa, "Barra", null, -1, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Desactivar_marca_inactiva()
    {
        var mesa = Mesa.Crear(Empresa, "Mesa 2", null, 2, Reloj).Valor;
        mesa.Desactivar(Reloj);
        mesa.Activa.Should().BeFalse();
    }

    [Fact]
    public void Crear_guarda_forma_y_posicion_iniciales()
    {
        var barra = Mesa.Crear(Empresa, "Barra", "Barra", 8, Reloj, FormaMesa.Rectangular, 120, 300).Valor;
        barra.Forma.Should().Be(FormaMesa.Rectangular);
        barra.PosX.Should().Be(120);
        barra.PosY.Should().Be(300);
    }

    [Fact]
    public void Colocar_actualiza_la_posicion_y_acota_al_lienzo()
    {
        var mesa = Mesa.Crear(Empresa, "Mesa 3", null, 2, Reloj).Valor;
        mesa.Colocar(250, 480, Reloj);
        mesa.PosX.Should().Be(250);
        mesa.PosY.Should().Be(480);

        mesa.Colocar(-50, 999999, Reloj);
        mesa.PosX.Should().Be(0);
        mesa.PosY.Should().Be(Mesa.Lienzo);
    }
}

public class ComandaTests
{
    private static readonly IReloj Reloj = new RelojFijo();
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid Mesa = Guid.NewGuid();
    private static readonly Guid Producto = Guid.NewGuid();

    private static Comanda ComandaAbierta() => Comanda.Abrir(Empresa, Mesa, "sin gluten", Reloj);

    [Fact]
    public void Abrir_registra_evento_y_estado_abierta()
    {
        var comanda = ComandaAbierta();

        comanda.Estado.Should().Be(EstadoComanda.Abierta);
        comanda.MesaId.Should().Be(Mesa);
        comanda.Notas.Should().Be("sin gluten");
        comanda.EventosDominio.Should().ContainSingle(e => e is ComandaAbierta);
    }

    [Fact]
    public void Agregar_linea_recalcula_totales_con_iva()
    {
        var comanda = ComandaAbierta();

        var linea = comanda.AgregarLinea(Producto, "Caña", 2m, 1.50m, "IVA10", 10m, Reloj);

        linea.EsCorrecto.Should().BeTrue();
        comanda.Lineas.Should().ContainSingle();
        comanda.BaseImponible.Should().Be(3.00m);
        comanda.CuotaIva.Should().Be(0.30m);
        comanda.Total.Should().Be(3.30m);
    }

    [Fact]
    public void Agregar_linea_con_cantidad_cero_falla()
    {
        var comanda = ComandaAbierta();
        comanda.AgregarLinea(Producto, "Café", 0m, 1.20m, "IVA10", 10m, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Agregar_mismo_producto_acumula_en_una_sola_linea()
    {
        var comanda = ComandaAbierta();
        comanda.AgregarLinea(Producto, "Caña", 2m, 1.50m, "IVA10", 10m, Reloj);
        var segunda = comanda.AgregarLinea(Producto, "Caña", 1m, 1.50m, "IVA10", 10m, Reloj);

        segunda.EsCorrecto.Should().BeTrue();
        comanda.Lineas.Should().ContainSingle();
        comanda.Lineas[0].Cantidad.Should().Be(3m);
        comanda.BaseImponible.Should().Be(4.50m);
        comanda.Total.Should().Be(4.95m);
    }

    [Fact]
    public void Mismo_producto_a_distinto_precio_abre_linea_nueva()
    {
        var comanda = ComandaAbierta();
        comanda.AgregarLinea(Producto, "Caña", 1m, 1.50m, "IVA10", 10m, Reloj);
        comanda.AgregarLinea(Producto, "Caña (happy hour)", 1m, 1.00m, "IVA10", 10m, Reloj);

        comanda.Lineas.Should().HaveCount(2);
    }

    [Fact]
    public void Enviar_a_cocina_manda_solo_lo_nuevo_y_es_idempotente()
    {
        var comanda = ComandaAbierta();
        comanda.AgregarLinea(Producto, "Caña", 2m, 1.50m, "IVA10", 10m, Reloj);

        comanda.EnviarACocina().Valor.Should().ContainSingle(a => a.Descripcion == "Caña" && a.Cantidad == 2m);
        comanda.EnviarACocina().Valor.Should().BeEmpty(); // nada nuevo

        // Pedir 1 más del mismo producto (se acumula en la línea): a cocina solo va la unidad nueva.
        comanda.AgregarLinea(Producto, "Caña", 1m, 1.50m, "IVA10", 10m, Reloj);
        comanda.EnviarACocina().Valor.Should().ContainSingle(a => a.Descripcion == "Caña" && a.Cantidad == 1m);
    }

    [Fact]
    public void Quitar_linea_recalcula_totales()
    {
        var comanda = ComandaAbierta();
        var a = comanda.AgregarLinea(Producto, "Caña", 2m, 1.50m, "IVA10", 10m, Reloj).Valor;
        comanda.AgregarLinea(Producto, "Tapa", 1m, 4.00m, "IVA10", 10m, Reloj);

        comanda.QuitarLinea(a.Id, Reloj).EsCorrecto.Should().BeTrue();

        comanda.Lineas.Should().ContainSingle();
        comanda.BaseImponible.Should().Be(4.00m);
        comanda.Total.Should().Be(4.40m);
    }

    [Fact]
    public void Quitar_linea_inexistente_falla()
    {
        var comanda = ComandaAbierta();
        comanda.QuitarLinea(Guid.NewGuid(), Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Cobrar_comanda_vacia_falla()
    {
        var comanda = ComandaAbierta();
        comanda.MarcarCobrada(Guid.NewGuid(), "T2026/000001", MetodoCobro.Efectivo, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Cobrar_comanda_con_lineas_congela_ticket_y_libera()
    {
        var comanda = ComandaAbierta();
        comanda.AgregarLinea(Producto, "Caña", 2m, 1.50m, "IVA10", 10m, Reloj);
        var factura = Guid.NewGuid();

        var r = comanda.MarcarCobrada(factura, "T2026/000001", MetodoCobro.Tarjeta, Reloj);

        r.EsCorrecto.Should().BeTrue();
        comanda.Estado.Should().Be(EstadoComanda.Cobrada);
        comanda.FacturaId.Should().Be(factura);
        comanda.NumeroTicket.Should().Be("T2026/000001");
        comanda.MetodoCobro.Should().Be(MetodoCobro.Tarjeta);
        comanda.CerradaEn.Should().NotBeNull();
        comanda.EventosDominio.Should().Contain(e => e is ComandaCobrada);
    }

    [Fact]
    public void No_se_pueden_agregar_lineas_a_comanda_cobrada()
    {
        var comanda = ComandaAbierta();
        comanda.AgregarLinea(Producto, "Caña", 1m, 1.50m, "IVA10", 10m, Reloj);
        comanda.MarcarCobrada(Guid.NewGuid(), "T2026/000001", MetodoCobro.Efectivo, Reloj);

        comanda.AgregarLinea(Producto, "Otra", 1m, 1.50m, "IVA10", 10m, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Anular_comanda_abierta_funciona_y_no_se_puede_cobrar_despues()
    {
        var comanda = ComandaAbierta();
        comanda.AgregarLinea(Producto, "Caña", 1m, 1.50m, "IVA10", 10m, Reloj);

        comanda.Anular(Reloj).EsCorrecto.Should().BeTrue();
        comanda.Estado.Should().Be(EstadoComanda.Anulada);
        comanda.MarcarCobrada(Guid.NewGuid(), "T2026/000002", MetodoCobro.Efectivo, Reloj).EsFallo.Should().BeTrue();
    }

    [Fact]
    public void Cobro_parcial_valida_y_resuelve_las_lineas_del_ticket()
    {
        var comanda = ComandaAbierta();
        var canas = comanda.AgregarLinea(Producto, "Caña", 3m, 1.50m, "IVA10", 10m, Reloj).Valor;

        var billing = comanda.ValidarCobroParcial([new ItemCobroParcial(canas.Id, 2m)]);

        billing.EsCorrecto.Should().BeTrue();
        billing.Valor.Should().ContainSingle(l => l.Descripcion == "Caña" && l.Cantidad == 2m && l.PrecioUnitario == 1.50m);
    }

    [Fact]
    public void Cobro_parcial_por_encima_del_pendiente_falla()
    {
        var comanda = ComandaAbierta();
        var canas = comanda.AgregarLinea(Producto, "Caña", 2m, 1.50m, "IVA10", 10m, Reloj).Valor;

        comanda.ValidarCobroParcial([new ItemCobroParcial(canas.Id, 3m)])
            .Error.Codigo.Should().Be("comanda.cobro_excede_pendiente");
    }

    [Fact]
    public void Cobro_parcial_no_cierra_la_comanda_mientras_queda_pendiente()
    {
        var comanda = ComandaAbierta();
        var canas = comanda.AgregarLinea(Producto, "Caña", 3m, 1.50m, "IVA10", 10m, Reloj).Valor;

        var r = comanda.AplicarCobroParcial([new ItemCobroParcial(canas.Id, 2m)], Guid.NewGuid(), "T2026/000001", MetodoCobro.Efectivo, Reloj);

        r.EsCorrecto.Should().BeTrue();
        comanda.Estado.Should().Be(EstadoComanda.Abierta);
        comanda.TieneCobroParcial.Should().BeTrue();
        canas.CantidadPendienteCobro.Should().Be(1m);
        comanda.TotalPendienteCobro.Should().Be(1.65m); // 1 × 1,50 + 10% IVA
    }

    [Fact]
    public void Cobro_parcial_que_paga_todo_lo_pendiente_cierra_la_comanda()
    {
        var comanda = ComandaAbierta();
        var canas = comanda.AgregarLinea(Producto, "Caña", 2m, 1.50m, "IVA10", 10m, Reloj).Valor;
        var tapa = comanda.AgregarLinea(Producto, "Tapa", 1m, 4.00m, "IVA10", 10m, Reloj).Valor;
        var factura = Guid.NewGuid();

        comanda.AplicarCobroParcial([new ItemCobroParcial(canas.Id, 2m)], Guid.NewGuid(), "T2026/000001", MetodoCobro.Efectivo, Reloj);
        comanda.Estado.Should().Be(EstadoComanda.Abierta);

        var r = comanda.AplicarCobroParcial([new ItemCobroParcial(tapa.Id, 1m)], factura, "T2026/000002", MetodoCobro.Tarjeta, Reloj);

        r.EsCorrecto.Should().BeTrue();
        comanda.Estado.Should().Be(EstadoComanda.Cobrada);
        comanda.FacturaId.Should().Be(factura);
        comanda.NumeroTicket.Should().Be("T2026/000002");
        comanda.EstaTotalmentePagada.Should().BeTrue();
        comanda.EventosDominio.Should().Contain(e => e is ComandaCobrada);
    }

    [Fact]
    public void No_se_puede_quitar_ni_reducir_por_debajo_de_lo_ya_cobrado()
    {
        var comanda = ComandaAbierta();
        var canas = comanda.AgregarLinea(Producto, "Caña", 3m, 1.50m, "IVA10", 10m, Reloj).Valor;
        comanda.AplicarCobroParcial([new ItemCobroParcial(canas.Id, 2m)], Guid.NewGuid(), "T2026/000001", MetodoCobro.Efectivo, Reloj);

        comanda.QuitarLinea(canas.Id, Reloj).Error.Codigo.Should().Be("comanda.linea_cobrada");
        comanda.FijarCantidadLinea(canas.Id, 1m, Reloj).Error.Codigo.Should().Be("comanda.cantidad_menor_cobrada");
        comanda.FijarCantidadLinea(canas.Id, 4m, Reloj).EsCorrecto.Should().BeTrue(); // subir sí se puede
    }

    [Fact]
    public void Mover_cambia_de_mesa_y_rechaza_la_misma_o_una_comanda_no_abierta()
    {
        var comanda = ComandaAbierta();
        comanda.AgregarLinea(Producto, "Caña", 1m, 1.50m, "IVA10", 10m, Reloj);
        var otraMesa = Guid.NewGuid();

        comanda.CambiarMesa(otraMesa, Reloj).EsCorrecto.Should().BeTrue();
        comanda.MesaId.Should().Be(otraMesa);
        comanda.EventosDominio.Should().Contain(e => e is ComandaMovida);
        comanda.CambiarMesa(otraMesa, Reloj).Error.Codigo.Should().Be("comanda.misma_mesa");
    }

    [Fact]
    public void Juntar_traslada_las_lineas_acumulando_y_cierra_la_de_origen()
    {
        var destino = ComandaAbierta();
        destino.AgregarLinea(Producto, "Caña", 2m, 1.50m, "IVA10", 10m, Reloj);
        var origen = Comanda.Abrir(Empresa, Guid.NewGuid(), null, Reloj);
        origen.AgregarLinea(Producto, "Caña", 1m, 1.50m, "IVA10", 10m, Reloj); // mismo producto/precio: acumula
        var tapaProducto = Guid.NewGuid();
        origen.AgregarLinea(tapaProducto, "Tapa", 1m, 4.00m, "IVA10", 10m, Reloj);

        destino.AbsorberDe(origen, Reloj).EsCorrecto.Should().BeTrue();
        origen.MarcarJuntada(destino.Id, Reloj).EsCorrecto.Should().BeTrue();

        destino.Lineas.Single(l => l.Descripcion == "Caña").Cantidad.Should().Be(3m); // 2 + 1 acumuladas
        destino.Lineas.Should().Contain(l => l.Descripcion == "Tapa" && l.Cantidad == 1m);
        destino.Total.Should().Be(9.35m); // (3×1,50 + 4,00) = 8,50 + 10% IVA
        origen.Estado.Should().Be(EstadoComanda.Juntada);
        origen.CerradaEn.Should().NotBeNull();
        origen.EventosDominio.Should().Contain(e => e is ComandaJuntada);
    }

    [Fact]
    public void No_se_puede_juntar_una_cuenta_con_cobro_parcial_en_curso()
    {
        var destino = ComandaAbierta();
        destino.AgregarLinea(Producto, "Caña", 1m, 1.50m, "IVA10", 10m, Reloj);
        var origen = Comanda.Abrir(Empresa, Guid.NewGuid(), null, Reloj);
        var linea = origen.AgregarLinea(Producto, "Caña", 2m, 1.50m, "IVA10", 10m, Reloj).Valor;
        origen.AplicarCobroParcial([new ItemCobroParcial(linea.Id, 1m)], Guid.NewGuid(), "T2026/000001", MetodoCobro.Efectivo, Reloj);

        destino.AbsorberDe(origen, Reloj).Error.Codigo.Should().Be("comanda.juntar_con_cobro_parcial");
    }
}
