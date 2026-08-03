using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Terceros.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ClienteRecargoEquivalencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "recargo_equivalencia",
                schema: "terceros",
                table: "cliente",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "recargo_equivalencia",
                schema: "terceros",
                table: "cliente");
        }
    }
}
