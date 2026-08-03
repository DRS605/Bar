using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Facturacion.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class RecargoEquivalencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cuota_recargo",
                schema: "facturacion",
                table: "linea_factura",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "porcentaje_recargo",
                schema: "facturacion",
                table: "linea_factura",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "recargo_equivalencia",
                schema: "facturacion",
                table: "factura",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "recargo_total",
                schema: "facturacion",
                table: "factura",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cuota_recargo",
                schema: "facturacion",
                table: "linea_factura");

            migrationBuilder.DropColumn(
                name: "porcentaje_recargo",
                schema: "facturacion",
                table: "linea_factura");

            migrationBuilder.DropColumn(
                name: "recargo_equivalencia",
                schema: "facturacion",
                table: "factura");

            migrationBuilder.DropColumn(
                name: "recargo_total",
                schema: "facturacion",
                table: "factura");
        }
    }
}
