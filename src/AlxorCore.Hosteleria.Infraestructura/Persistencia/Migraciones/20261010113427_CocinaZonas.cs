using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Hosteleria.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CocinaZonas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cantidad_servida",
                schema: "hosteleria",
                table: "linea_comanda",
                type: "numeric(14,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "zona_producto",
                schema: "hosteleria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zona = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zona_producto", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_zona_producto",
                schema: "hosteleria",
                table: "zona_producto",
                columns: new[] { "empresa_id", "producto_id" },
                unique: true);

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("hosteleria", "zona_producto"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("hosteleria", "zona_producto"));

            migrationBuilder.DropTable(
                name: "zona_producto",
                schema: "hosteleria");

            migrationBuilder.DropColumn(
                name: "cantidad_servida",
                schema: "hosteleria",
                table: "linea_comanda");
        }
    }
}
