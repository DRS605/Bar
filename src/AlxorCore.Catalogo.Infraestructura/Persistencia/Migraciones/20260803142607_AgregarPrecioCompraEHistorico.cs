using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Catalogo.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarPrecioCompraEHistorico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "precio_compra",
                schema: "catalogo",
                table: "producto",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "historico_precio",
                schema: "catalogo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    precio_venta = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    precio_compra = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    registrado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historico_precio", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_historico_precio_producto",
                schema: "catalogo",
                table: "historico_precio",
                columns: new[] { "empresa_id", "producto_id", "registrado_en" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("catalogo", "historico_precio"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("catalogo", "historico_precio"));

            migrationBuilder.DropTable(
                name: "historico_precio",
                schema: "catalogo");

            migrationBuilder.DropColumn(
                name: "precio_compra",
                schema: "catalogo",
                table: "producto");
        }
    }
}
