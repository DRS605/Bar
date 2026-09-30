namespace AlxorCore.Hosteleria.Dominio;

/// <summary>Un artículo que se envía a cocina/barra: descripción, cantidad (la nueva de este envío) y nota.</summary>
public sealed record ArticuloCocina(string Descripcion, decimal Cantidad, string? Nota = null);
