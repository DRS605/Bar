using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Hosteleria.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ConfiguracionCarta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuracion_carta",
                schema: "hosteleria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tema = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracion_carta", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_configuracion_carta_empresa",
                schema: "hosteleria",
                table: "configuracion_carta",
                column: "empresa_id",
                unique: true);

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("hosteleria", "configuracion_carta"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("hosteleria", "configuracion_carta"));

            migrationBuilder.DropTable(
                name: "configuracion_carta",
                schema: "hosteleria");
        }
    }
}
