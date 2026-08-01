using System.Security.Claims;
using AlxorCore.Nucleo.Multiempresa;
using AlxorCore.Nucleo.Seguridad;

namespace AlxorCore.Api.Comun;

/// <summary>
/// Resuelve la empresa activa a partir del claim <c>empresa_id</c> del usuario de la petición.
/// Es la fuente que usan la persistencia (filtro global + RLS) y la autorización.
/// </summary>
public sealed class ContextoEmpresaHttp : IContextoEmpresa
{
    private readonly IHttpContextAccessor _accessor;

    public ContextoEmpresaHttp(IHttpContextAccessor accessor) => _accessor = accessor;

    public Guid? EmpresaId
    {
        get
        {
            var valor = _accessor.HttpContext?.User.FindFirstValue(ClaimsAlxor.EmpresaId);
            return Guid.TryParse(valor, out var id) ? id : null;
        }
    }
}
