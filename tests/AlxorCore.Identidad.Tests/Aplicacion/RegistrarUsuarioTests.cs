using AlxorCore.Identidad.Aplicacion.CasosDeUso;
using AlxorCore.Identidad.Tests.Dobles;
using AlxorCore.Nucleo.Resultados;
using FluentAssertions;
using Xunit;

namespace AlxorCore.Identidad.Tests.Aplicacion;

public class RegistrarUsuarioTests
{
    private readonly FakeRepositorioUsuarios _usuarios = new();
    private readonly FakeHasherContrasena _hasher = new();
    private readonly FakeServicioVerificacionEmail _email = new();
    private readonly FakeUnidadDeTrabajo _uow = new();
    private readonly RelojFijo _reloj = RelojFijo.Predeterminado();

    private RegistrarUsuario CrearCasoDeUso() => new(_usuarios, _hasher, _email, _uow, _reloj);

    [Fact]
    public async Task Registro_correcto_persiste_confirma_y_envia_verificacion()
    {
        var caso = CrearCasoDeUso();

        var resultado = await caso.EjecutarAsync(new RegistrarUsuarioComando("Ana@Ejemplo.com", "Ana", "contrasena123"));

        resultado.EsCorrecto.Should().BeTrue();
        resultado.Valor.Perfil.Email.Should().Be("ana@ejemplo.com");
        resultado.Valor.TokenVerificacion.Should().NotBeNullOrEmpty();
        _usuarios.Usuarios.Should().ContainSingle();
        _usuarios.Usuarios[0].TokenVerificacionHash.Should().NotBeNull();
        _uow.Confirmaciones.Should().Be(1);
        _email.Envios.Should().Be(1);
    }

    [Fact]
    public async Task Registro_con_email_duplicado_devuelve_conflicto()
    {
        var caso = CrearCasoDeUso();
        await caso.EjecutarAsync(new RegistrarUsuarioComando("dup@ejemplo.com", "Ana", "contrasena123"));

        var segundo = await caso.EjecutarAsync(new RegistrarUsuarioComando("dup@ejemplo.com", "Otro", "contrasena123"));

        segundo.EsFallo.Should().BeTrue();
        segundo.Error.Tipo.Should().Be(TipoError.Conflicto);
        segundo.Error.Codigo.Should().Be("usuario.email_en_uso");
    }

    [Theory]
    [InlineData("corta")]
    [InlineData("1234567")]
    public async Task Registro_con_contrasena_corta_es_invalido(string contrasena)
    {
        var caso = CrearCasoDeUso();

        var resultado = await caso.EjecutarAsync(new RegistrarUsuarioComando("nueva@ejemplo.com", "Ana", contrasena));

        resultado.EsFallo.Should().BeTrue();
        resultado.Error.Codigo.Should().Be("contrasena.corta");
    }

    [Fact]
    public async Task Registro_con_email_invalido_es_invalido()
    {
        var caso = CrearCasoDeUso();

        var resultado = await caso.EjecutarAsync(new RegistrarUsuarioComando("no-es-email", "Ana", "contrasena123"));

        resultado.EsFallo.Should().BeTrue();
        resultado.Error.Tipo.Should().Be(TipoError.Validacion);
    }
}
