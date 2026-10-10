using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Hosteleria.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Suscripcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "suscripcion",
                schema: "hosteleria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suscripcion", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_suscripcion_empresa",
                schema: "hosteleria",
                table: "suscripcion",
                column: "empresa_id",
                unique: true);

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("hosteleria", "suscripcion"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("hosteleria", "suscripcion"));

            migrationBuilder.DropTable(
                name: "suscripcion",
                schema: "hosteleria");
        }
    }
}
