using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Hosteleria.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class FichaAgotado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "agotado",
                schema: "hosteleria",
                table: "ficha_carta",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "agotado",
                schema: "hosteleria",
                table: "ficha_carta");
        }
    }
}
