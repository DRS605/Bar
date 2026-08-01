using AlxorCore.Identidad.Aplicacion.Puertos;
using AlxorCore.Identidad.Dominio;
using AlxorCore.Nucleo.Aplicacion;

namespace AlxorCore.Identidad.Tests.Dobles;

/// <summary>Hasher trivial y determinista para tests (no seguro; solo pruebas).</summary>
public sealed class FakeHasherContrasena : IHasherContrasena
{
    public string Hash(string contrasena) => "hash:" + contrasena;

    public bool Verificar(string hash, string contrasena) => hash == "hash:" + contrasena;
}

/// <summary>Proveedor de tokens de prueba.</summary>
public sealed class FakeProveedorTokens : IProveedorTokens
{
    public TokenAcceso GenerarToken(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        return new TokenAcceso("token-" + usuario.Id, new DateTimeOffset(2026, 1, 1, 13, 0, 0, TimeSpan.Zero));
    }
}

/// <summary>Servicio de verificación de email de prueba: cuenta los envíos.</summary>
public sealed class FakeServicioVerificacionEmail : IServicioVerificacionEmail
{
    public int Envios { get; private set; }

    public Task EnviarVerificacionAsync(Usuario usuario, CancellationToken ct = default)
    {
        Envios++;
        return Task.CompletedTask;
    }
}

/// <summary>Unidad de trabajo de prueba: cuenta las confirmaciones.</summary>
public sealed class FakeUnidadDeTrabajo : IUnidadDeTrabajo
{
    public int Confirmaciones { get; private set; }

    public Task<int> GuardarCambiosAsync(CancellationToken ct = default)
    {
        Confirmaciones++;
        return Task.FromResult(1);
    }
}
