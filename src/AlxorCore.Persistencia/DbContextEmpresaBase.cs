using System.Linq.Expressions;
using System.Reflection;
using AlxorCore.Nucleo.Aplicacion;
using AlxorCore.Nucleo.Multiempresa;
using Microsoft.EntityFrameworkCore;

namespace AlxorCore.Persistencia;

/// <summary>
/// Base de los <see cref="DbContext"/> de módulos con datos multiempresa. Aplica automáticamente
/// un filtro global por <c>empresa_id</c> a toda entidad que implemente <see cref="IEntidadEmpresa"/>,
/// de modo que sea imposible "olvidar" el filtrado por empresa en una consulta. La Row-Level
/// Security de PostgreSQL actúa como segunda barrera (ver interceptor).
/// </summary>
public abstract class DbContextEmpresaBase : DbContextBase
{
    private static readonly MethodInfo MetodoFiltro =
        typeof(DbContextEmpresaBase).GetMethod(nameof(ConstruirFiltro), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly IContextoEmpresa _contextoEmpresa;

    protected DbContextEmpresaBase(DbContextOptions opciones, IPublicadorEventos publicadorEventos, IContextoEmpresa contextoEmpresa)
        : base(opciones, publicadorEventos)
    {
        _contextoEmpresa = contextoEmpresa;
    }

    /// <summary>Empresa activa; <see cref="Guid.Empty"/> si no hay ninguna (no devolverá filas).</summary>
    public Guid EmpresaActualId => _contextoEmpresa.EmpresaId ?? Guid.Empty;

    /// <summary>
    /// Aplica el filtro multiempresa a todas las entidades <see cref="IEntidadEmpresa"/> del modelo.
    /// Los módulos deben invocarlo al final de su <c>OnModelCreating</c>.
    /// </summary>
    protected void AplicarFiltroMultiempresa(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var tipo in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IEntidadEmpresa).IsAssignableFrom(tipo.ClrType))
            {
                var filtro = (LambdaExpression)MetodoFiltro.MakeGenericMethod(tipo.ClrType).Invoke(this, null)!;
                modelBuilder.Entity(tipo.ClrType).HasQueryFilter(filtro);
            }
        }
    }

    private Expression<Func<T, bool>> ConstruirFiltro<T>()
        where T : class, IEntidadEmpresa
    {
        return e => e.EmpresaId == EmpresaActualId;
    }
}
