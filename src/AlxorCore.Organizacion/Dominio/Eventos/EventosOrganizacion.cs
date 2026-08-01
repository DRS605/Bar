using AlxorCore.Nucleo.Dominio;

namespace AlxorCore.Organizacion.Dominio.Eventos;

/// <summary>Se ha creado una empresa.</summary>
public sealed record EmpresaCreada(Guid EmpresaId, string Nif, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>Se ha creado una membresía (usuario dentro de una empresa).</summary>
public sealed record MembresiaCreada(Guid MembresiaId, Guid UsuarioId, Guid EmpresaId, string RolCodigo, DateTimeOffset OcurridoEn) : IEventoDominio;

/// <summary>Se ha creado una serie de numeración.</summary>
public sealed record SerieCreada(Guid SerieId, Guid EmpresaId, string Prefijo, int Ejercicio, DateTimeOffset OcurridoEn) : IEventoDominio;
