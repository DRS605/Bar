using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Hosteleria.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class OpcionesProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grupo_opcion",
                schema: "hosteleria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    seleccion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grupo_opcion", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "opcion_producto",
                schema: "hosteleria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grupo_opcion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    precio_delta = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_opcion_producto", x => x.id);
                    table.ForeignKey(
                        name: "FK_opcion_producto_grupo_opcion_grupo_opcion_id",
                        column: x => x.grupo_opcion_id,
                        principalSchema: "hosteleria",
                        principalTable: "grupo_opcion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_grupo_opcion_producto",
                schema: "hosteleria",
                table: "grupo_opcion",
                columns: new[] { "empresa_id", "producto_id" });

            migrationBuilder.CreateIndex(
                name: "ix_opcion_producto_grupo",
                schema: "hosteleria",
                table: "opcion_producto",
                column: "grupo_opcion_id");

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("hosteleria", "grupo_opcion"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("hosteleria", "opcion_producto"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("hosteleria", "opcion_producto"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("hosteleria", "grupo_opcion"));

            migrationBuilder.DropTable(
                name: "opcion_producto",
                schema: "hosteleria");

            migrationBuilder.DropTable(
                name: "grupo_opcion",
                schema: "hosteleria");
        }
    }
}
