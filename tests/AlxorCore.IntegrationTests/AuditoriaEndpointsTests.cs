using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace AlxorCore.IntegrationTests;

/// <summary>Pruebas de integración de la auditoría (quién hizo qué y cuándo).</summary>
public sealed class AuditoriaEndpointsTests : IClassFixture<FabricaApiPruebas>
{
    private readonly FabricaApiPruebas _fabrica;

    public AuditoriaEndpointsTests(FabricaApiPruebas fabrica) => _fabrica = fabrica;

    private sealed record RegistroAuditoriaResp(string Accion, string Metodo, string Ruta, int CodigoEstado);

    [Fact]
    public async Task Una_operacion_de_alta_queda_registrada_en_la_auditoria()
    {
        var (cliente, _) = await Ayudas.ConEmpresaAsync(_fabrica);

        await cliente.PostAsJsonAsync("/clientes", new { Nombre = "Cliente auditado" });

        var registros = await cliente.GetFromJsonAsync<List<RegistroAuditoriaResp>>("/auditoria");

        registros.Should().Contain(r => r.Metodo == "POST" && r.Ruta == "/clientes" && r.Accion == "Alta en clientes");
    }

    [Fact]
    public async Task La_auditoria_esta_aislada_por_empresa()
    {
        var (empresaA, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        await empresaA.PostAsJsonAsync("/clientes", new { Nombre = "Cliente de A" });

        var (empresaB, _) = await Ayudas.ConEmpresaAsync(_fabrica);
        var registrosB = await empresaB.GetFromJsonAsync<List<RegistroAuditoriaResp>>("/auditoria");

        registrosB.Should().NotContain(r => r.Ruta == "/clientes");
    }
}
