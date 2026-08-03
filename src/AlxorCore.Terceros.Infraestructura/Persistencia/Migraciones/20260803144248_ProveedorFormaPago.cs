using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Terceros.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ProveedorFormaPago : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "forma_pago",
                schema: "terceros",
                table: "proveedor",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NoIndicada");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "forma_pago",
                schema: "terceros",
                table: "proveedor");
        }
    }
}
