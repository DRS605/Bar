using AlxorCore.Identidad.Dominio;
using AlxorCore.Identidad.Tests.Dobles;
using AlxorCore.Nucleo.Tiempo;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Identidad.Tests.Dominio;

public class TokenCuentaUsuarioTests
{
    private static readonly IReloj Reloj = RelojFijo.Predeterminado();

    private static Usuario NuevoUsuario() =>
        Usuario.Registrar(Email.Crear("ana@ejemplo.com").Valor, "Ana", HashContrasena.DesdeHash("hash"), Reloj).Valor;

    [Fact]
    public void Token_solo_guarda_el_hash_no_el_valor()
    {
        var token = TokenCuenta.Nuevo();
        TokenCuenta.Hash(token).Should().NotBe(token).And.HaveLength(64);
    }

    [Fact]
    public void ConfirmarEmail_con_token_valido_verifica_y_lo_consume()
    {
        var u = NuevoUsuario();
        var token = TokenCuenta.Nuevo();
        u.EmitirTokenVerificacion(token, Reloj.AhoraUtc.AddHours(48), Reloj);

        u.ConfirmarEmailConToken(token, Reloj).EsCorrecto.Should().BeTrue();
        u.EmailVerificado.Should().BeTrue();
        u.TokenVerificacionHash.Should().BeNull();
    }

    [Fact]
    public void ConfirmarEmail_con_token_incorrecto_falla()
    {
        var u = NuevoUsuario();
        u.EmitirTokenVerificacion(TokenCuenta.Nuevo(), Reloj.AhoraUtc.AddHours(48), Reloj);

        u.ConfirmarEmailConToken("otro-token", Reloj).EsFallo.Should().BeTrue();
        u.EmailVerificado.Should().BeFalse();
    }

    [Fact]
    public void ConfirmarEmail_con_token_caducado_falla()
    {
        var u = NuevoUsuario();
        var token = TokenCuenta.Nuevo();
        u.EmitirTokenVerificacion(token, Reloj.AhoraUtc.AddHours(-1), Reloj); // ya caducado

        var r = u.ConfirmarEmailConToken(token, Reloj);
        r.EsFallo.Should().BeTrue();
        r.Error.Codigo.Should().Be("verificacion.token_caducado");
    }

    [Fact]
    public void Restablecer_con_token_valido_cambia_hash_y_consume_el_token()
    {
        var u = NuevoUsuario();
        var token = TokenCuenta.Nuevo();
        u.EmitirTokenRestablecimiento(token, Reloj.AhoraUtc.AddHours(1), Reloj);

        u.RestablecerConToken(token, HashContrasena.DesdeHash("nuevo-hash"), Reloj).EsCorrecto.Should().BeTrue();
        u.HashContrasena.Valor.Should().Be("nuevo-hash");
        u.TokenRestablecimientoHash.Should().BeNull();
        // El token no puede reutilizarse.
        u.RestablecerConToken(token, HashContrasena.DesdeHash("otro"), Reloj).EsFallo.Should().BeTrue();
    }
}
