using AlxorCore.Identidad.Dominio;
using AlxorCore.Identidad.Dominio.Eventos;
using AlxorCore.Identidad.Tests.Dobles;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Identidad.Tests.Dominio;

public class UsuarioTests
{
    private static readonly RelojFijo Reloj = RelojFijo.Predeterminado();

    private static Email UnEmail() => Email.Crear("nuevo@ejemplo.com").Valor;

    private static HashContrasena UnHash() => HashContrasena.DesdeHash("hash:secreto");

    [Fact]
    public void Registrar_crea_usuario_activo_no_verificado_y_emite_evento()
    {
        var resultado = Usuario.Registrar(UnEmail(), "Ana", UnHash(), Reloj);

        resultado.EsCorrecto.Should().BeTrue();
        var usuario = resultado.Valor;
        usuario.Estado.Should().Be(EstadoUsuario.Activo);
        usuario.EmailVerificado.Should().BeFalse();
        usuario.PuedeAutenticarse.Should().BeTrue();
        usuario.EventosDominio.Should().ContainSingle(e => e is UsuarioRegistrado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Registrar_rechaza_nombre_vacio(string? nombre)
    {
        var resultado = Usuario.Registrar(UnEmail(), nombre, UnHash(), Reloj);

        resultado.EsFallo.Should().BeTrue();
        resultado.Error.Codigo.Should().Be("usuario.nombre_vacio");
    }

    [Fact]
    public void VerificarEmail_marca_verificado_y_es_idempotente()
    {
        var usuario = Usuario.Registrar(UnEmail(), "Ana", UnHash(), Reloj).Valor;
        usuario.LimpiarEventos();

        usuario.VerificarEmail(Reloj);
        usuario.VerificarEmail(Reloj); // segunda vez no cambia nada

        usuario.EmailVerificado.Should().BeTrue();
        usuario.EventosDominio.Should().ContainSingle(e => e is EmailUsuarioVerificado);
    }

    [Fact]
    public void Suspender_impide_autenticarse_y_Reactivar_lo_restaura()
    {
        var usuario = Usuario.Registrar(UnEmail(), "Ana", UnHash(), Reloj).Valor;

        usuario.Suspender(Reloj);
        usuario.Estado.Should().Be(EstadoUsuario.Suspendido);
        usuario.PuedeAutenticarse.Should().BeFalse();

        usuario.Reactivar(Reloj);
        usuario.Estado.Should().Be(EstadoUsuario.Activo);
        usuario.PuedeAutenticarse.Should().BeTrue();
    }

    [Fact]
    public void CambiarContrasena_actualiza_el_hash()
    {
        var usuario = Usuario.Registrar(UnEmail(), "Ana", UnHash(), Reloj).Valor;
        var nuevo = HashContrasena.DesdeHash("hash:otro");

        usuario.CambiarContrasena(nuevo, Reloj);

        usuario.HashContrasena.Should().Be(nuevo);
    }
}
