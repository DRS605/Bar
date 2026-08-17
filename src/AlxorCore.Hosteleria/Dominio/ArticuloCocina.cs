namespace AlxorCore.Hosteleria.Dominio;

/// <summary>Un artículo que se envía a cocina/barra: descripción y cantidad (la nueva de este envío).</summary>
public sealed record ArticuloCocina(string Descripcion, decimal Cantidad);
