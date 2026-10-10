using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Hosteleria.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ComandaCamarero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "usuario_id",
                schema: "hosteleria",
                table: "comanda",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "usuario_nombre",
                schema: "hosteleria",
                table: "comanda",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "usuario_id",
                schema: "hosteleria",
                table: "comanda");

            migrationBuilder.DropColumn(
                name: "usuario_nombre",
                schema: "hosteleria",
                table: "comanda");
        }
    }
}
