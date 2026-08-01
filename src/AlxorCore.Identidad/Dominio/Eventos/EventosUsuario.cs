using AlxorCore.Nucleo.Dominio;

namespace AlxorCore.Identidad.Dominio.Eventos;

/// <summary>Se ha registrado un nuevo usuario.</summary>
public sealed record UsuarioRegistrado(Guid UsuarioId, string Email, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>El usuario ha verificado su correo electrónico.</summary>
public sealed record EmailUsuarioVerificado(Guid UsuarioId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>El usuario ha cambiado su contraseña.</summary>
public sealed record ContrasenaUsuarioCambiada(Guid UsuarioId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>El usuario ha sido suspendido y no puede autenticarse.</summary>
public sealed record UsuarioSuspendido(Guid UsuarioId, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>El usuario suspendido ha sido reactivado.</summary>
public sealed record UsuarioReactivado(Guid UsuarioId, DateTimeOffset OcurridoEn) : IEventoDominio;
