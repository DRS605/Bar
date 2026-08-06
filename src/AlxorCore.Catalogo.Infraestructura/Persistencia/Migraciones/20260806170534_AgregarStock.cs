using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Catalogo.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "controlar_stock",
                schema: "catalogo",
                table: "producto",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "stock",
                schema: "catalogo",
                table: "producto",
                type: "numeric(14,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "movimiento_stock",
                schema: "catalogo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(14,3)", nullable: false),
                    stock_resultante = table.Column<decimal>(type: "numeric(14,3)", nullable: false),
                    motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimiento_stock", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_stock_producto",
                schema: "catalogo",
                table: "movimiento_stock",
                columns: new[] { "empresa_id", "producto_id", "creado_en" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("catalogo", "movimiento_stock"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("catalogo", "movimiento_stock"));

            migrationBuilder.DropTable(
                name: "movimiento_stock",
                schema: "catalogo");

            migrationBuilder.DropColumn(
                name: "controlar_stock",
                schema: "catalogo",
                table: "producto");

            migrationBuilder.DropColumn(
                name: "stock",
                schema: "catalogo",
                table: "producto");
        }
    }
}
