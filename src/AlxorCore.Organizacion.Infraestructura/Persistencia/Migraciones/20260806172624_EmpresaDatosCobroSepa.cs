using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Organizacion.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class EmpresaDatosCobroSepa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "iban",
                schema: "organizacion",
                table: "empresa",
                type: "character varying(34)",
                maxLength: 34,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identificador_acreedor",
                schema: "organizacion",
                table: "empresa",
                type: "character varying(35)",
                maxLength: 35,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "iban",
                schema: "organizacion",
                table: "empresa");

            migrationBuilder.DropColumn(
                name: "identificador_acreedor",
                schema: "organizacion",
                table: "empresa");
        }
    }
}
