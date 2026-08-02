using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Facturacion.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarFacturasRecurrentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "factura_recurrente",
                schema: "facturacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    periodicidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    proxima_emision = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: true),
                    porcentaje_irpf = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    facturas_generadas = table.Column<int>(type: "integer", nullable: false),
                    ultima_emision = table.Column<DateOnly>(type: "date", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factura_recurrente", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "linea_recurrente",
                schema: "facturacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(14,3)", nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    descuento = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    codigo_iva = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    porcentaje_iva = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    @base = table.Column<decimal>(name: "base", type: "numeric(14,2)", nullable: false),
                    cuota_iva = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    factura_recurrente_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_linea_recurrente", x => x.id);
                    table.ForeignKey(
                        name: "FK_linea_recurrente_factura_recurrente_factura_recurrente_id",
                        column: x => x.factura_recurrente_id,
                        principalSchema: "facturacion",
                        principalTable: "factura_recurrente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_recurrente_vencidas",
                schema: "facturacion",
                table: "factura_recurrente",
                columns: new[] { "empresa_id", "activa", "proxima_emision" });

            migrationBuilder.CreateIndex(
                name: "IX_linea_recurrente_factura_recurrente_id",
                schema: "facturacion",
                table: "linea_recurrente",
                column: "factura_recurrente_id");

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("facturacion", "factura_recurrente"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("facturacion", "linea_recurrente"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("facturacion", "linea_recurrente"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("facturacion", "factura_recurrente"));

            migrationBuilder.DropTable(
                name: "linea_recurrente",
                schema: "facturacion");

            migrationBuilder.DropTable(
                name: "factura_recurrente",
                schema: "facturacion");
        }
    }
}
