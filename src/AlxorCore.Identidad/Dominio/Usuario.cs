using AlxorCore.Identidad.Dominio.Eventos;
using AlxorCore.Nucleo.Dominio;
using AlxorCore.Nucleo.Resultados;
using AlxorCore.Nucleo.Tiempo;

namespace AlxorCore.Identidad.Dominio;

/// <summary>
/// Usuario de la plataforma. Es una identidad global (no pertenece a una empresa): un mismo
/// usuario podrá operar en varias empresas a través de sus membresías (módulo Organización).
/// Raíz de agregado responsable de sus invariantes de estado y credenciales.
/// </summary>
public sealed class Usuario : RaizAgregado<Guid>
{
    public const int LongitudMaximaNombre = 120;

    // Constructor privado para EF Core (rehidratación desde la base de datos).
    private Usuario(Guid id)
        : base(id)
    {
        Email = null!;
        Nombre = null!;
        HashContrasena = null!;
    }

    private Usuario(Guid id, Email email, string nombre, HashContrasena hash, DateTimeOffset ahora)
        : base(id)
    {
        Email = email;
        Nombre = nombre;
        HashContrasena = hash;
        Estado = EstadoUsuario.Activo;
        EmailVerificado = false;
        CreadoEn = ahora;
        ActualizadoEn = ahora;
    }

    /// <summary>Correo electrónico (identificador de acceso, único en la plataforma).</summary>
    public Email Email { get; private set; }

    /// <summary>Nombre visible del usuario.</summary>
    public string Nombre { get; private set; }

    /// <summary>Contraseña cifrada.</summary>
    public HashContrasena HashContrasena { get; private set; }

    /// <summary>Estado de la cuenta.</summary>
    public EstadoUsuario Estado { get; private set; }

    /// <summary>Indica si el usuario ha verificado su correo.</summary>
    public bool EmailVerificado { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    public DateTimeOffset ActualizadoEn { get; private set; }

    /// <summary>Indica si el usuario puede autenticarse en este momento.</summary>
    public bool PuedeAutenticarse => Estado == EstadoUsuario.Activo;

    /// <summary>
    /// Registra un nuevo usuario ya validado. El correo se activa de inmediato para no añadir
    /// fricción al alta; la verificación de correo se solicita aparte y no bloquea el uso.
    /// </summary>
    public static Resultado<Usuario> Registrar(Email email, string? nombre, HashContrasena hash, IReloj reloj)
    {
        var nombreNormalizado = (nombre ?? string.Empty).Trim();

        if (nombreNormalizado.Length == 0)
        {
            return Resultado.Fallo<Usuario>(Error.Validacion("usuario.nombre_vacio", "El nombre es obligatorio."));
        }

        if (nombreNormalizado.Length > LongitudMaximaNombre)
        {
            return Resultado.Fallo<Usuario>(Error.Validacion("usuario.nombre_largo", "El nombre es demasiado largo."));
        }

        var usuario = new Usuario(Guid.NewGuid(), email, nombreNormalizado, hash, reloj.AhoraUtc);
        usuario.RegistrarEvento(new UsuarioRegistrado(usuario.Id, email.Valor, reloj.AhoraUtc));
        return Resultado.Ok(usuario);
    }

    /// <summary>Marca el correo como verificado. Idempotente.</summary>
    public void VerificarEmail(IReloj reloj)
    {
        if (EmailVerificado)
        {
            return;
        }

        EmailVerificado = true;
        Tocar(reloj);
        RegistrarEvento(new EmailUsuarioVerificado(Id, reloj.AhoraUtc));
    }

    /// <summary>Sustituye la contraseña por un nuevo hash.</summary>
    public void CambiarContrasena(HashContrasena nuevoHash, IReloj reloj)
    {
        HashContrasena = nuevoHash;
        Tocar(reloj);
        RegistrarEvento(new ContrasenaUsuarioCambiada(Id, reloj.AhoraUtc));
    }

    /// <summary>Suspende la cuenta. Idempotente.</summary>
    public void Suspender(IReloj reloj)
    {
        if (Estado == EstadoUsuario.Suspendido)
        {
            return;
        }

        Estado = EstadoUsuario.Suspendido;
        Tocar(reloj);
        RegistrarEvento(new UsuarioSuspendido(Id, reloj.AhoraUtc));
    }

    /// <summary>Reactiva una cuenta suspendida. Idempotente.</summary>
    public void Reactivar(IReloj reloj)
    {
        if (Estado == EstadoUsuario.Activo)
        {
            return;
        }

        Estado = EstadoUsuario.Activo;
        Tocar(reloj);
        RegistrarEvento(new UsuarioReactivado(Id, reloj.AhoraUtc));
    }

    private void Tocar(IReloj reloj) => ActualizadoEn = reloj.AhoraUtc;
}
