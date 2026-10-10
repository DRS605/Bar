using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Hosteleria.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class MenuDia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "menu_dia",
                schema: "hosteleria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    precio = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    incluye = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_dia", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "menu_dia_plato",
                schema: "hosteleria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    menu_dia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seccion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_dia_plato", x => x.id);
                    table.ForeignKey(
                        name: "FK_menu_dia_plato_menu_dia_menu_dia_id",
                        column: x => x.menu_dia_id,
                        principalSchema: "hosteleria",
                        principalTable: "menu_dia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_menu_dia_empresa",
                schema: "hosteleria",
                table: "menu_dia",
                column: "empresa_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_menu_dia_plato_menu",
                schema: "hosteleria",
                table: "menu_dia_plato",
                column: "menu_dia_id");

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("hosteleria", "menu_dia"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("hosteleria", "menu_dia_plato"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("hosteleria", "menu_dia_plato"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("hosteleria", "menu_dia"));

            migrationBuilder.DropTable(
                name: "menu_dia_plato",
                schema: "hosteleria");

            migrationBuilder.DropTable(
                name: "menu_dia",
                schema: "hosteleria");
        }
    }
}
