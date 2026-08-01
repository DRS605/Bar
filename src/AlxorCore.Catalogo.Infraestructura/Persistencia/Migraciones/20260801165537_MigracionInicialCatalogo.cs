using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Catalogo.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class MigracionInicialCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalogo");

            migrationBuilder.CreateTable(
                name: "producto",
                schema: "catalogo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    referencia = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    codigo_iva = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    unidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_producto", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_producto_empresa_nombre",
                schema: "catalogo",
                table: "producto",
                columns: new[] { "empresa_id", "nombre" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("catalogo", "producto"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("catalogo", "producto"));

            migrationBuilder.DropTable(
                name: "producto",
                schema: "catalogo");
        }
    }
}
