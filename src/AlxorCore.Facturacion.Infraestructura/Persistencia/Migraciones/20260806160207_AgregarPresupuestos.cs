using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Facturacion.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarPresupuestos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "presupuesto",
                schema: "facturacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_completo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    validez = table.Column<DateOnly>(type: "date", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    base_imponible = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cuota_iva = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    factura_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_presupuesto", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "linea_presupuesto",
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
                    presupuesto_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_linea_presupuesto", x => x.id);
                    table.ForeignKey(
                        name: "FK_linea_presupuesto_presupuesto_presupuesto_id",
                        column: x => x.presupuesto_id,
                        principalSchema: "facturacion",
                        principalTable: "presupuesto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_linea_presupuesto_presupuesto_id",
                schema: "facturacion",
                table: "linea_presupuesto",
                column: "presupuesto_id");

            migrationBuilder.CreateIndex(
                name: "ix_presupuesto_empresa_fecha",
                schema: "facturacion",
                table: "presupuesto",
                columns: new[] { "empresa_id", "fecha" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("facturacion", "presupuesto"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("facturacion", "linea_presupuesto"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("facturacion", "linea_presupuesto"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("facturacion", "presupuesto"));

            migrationBuilder.DropTable(
                name: "linea_presupuesto",
                schema: "facturacion");

            migrationBuilder.DropTable(
                name: "presupuesto",
                schema: "facturacion");
        }
    }
}
