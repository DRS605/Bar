using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Hosteleria.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class FichaDestacados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "picante",
                schema: "hosteleria",
                table: "ficha_carta",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "recomendado",
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
                name: "picante",
                schema: "hosteleria",
                table: "ficha_carta");

            migrationBuilder.DropColumn(
                name: "recomendado",
                schema: "hosteleria",
                table: "ficha_carta");
        }
    }
}
