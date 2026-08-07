using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Hosteleria.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class MesaPlano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "forma",
                schema: "hosteleria",
                table: "mesa",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Cuadrada");

            migrationBuilder.AddColumn<double>(
                name: "pos_x",
                schema: "hosteleria",
                table: "mesa",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "pos_y",
                schema: "hosteleria",
                table: "mesa",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "forma",
                schema: "hosteleria",
                table: "mesa");

            migrationBuilder.DropColumn(
                name: "pos_x",
                schema: "hosteleria",
                table: "mesa");

            migrationBuilder.DropColumn(
                name: "pos_y",
                schema: "hosteleria",
                table: "mesa");
        }
    }
}
