using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Facturacion.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class FacturaVencimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_vencimiento",
                schema: "facturacion",
                table: "factura",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            // Facturas ya existentes: su vencimiento se considera la fecha de emisión (contado).
            migrationBuilder.Sql("UPDATE facturacion.factura SET fecha_vencimiento = fecha_emision;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fecha_vencimiento",
                schema: "facturacion",
                table: "factura");
        }
    }
}
