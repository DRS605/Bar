using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Facturacion.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class MigracionInicialFacturacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "facturacion");

            migrationBuilder.CreateTable(
                name: "factura",
                schema: "facturacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prefijo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ejercicio = table.Column<int>(type: "integer", nullable: false),
                    numero = table.Column<long>(type: "bigint", nullable: false),
                    numero_completo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    fecha_emision = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_operacion = table.Column<DateOnly>(type: "date", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cliente_nif = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cliente_calle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cliente_cp = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    cliente_poblacion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    cliente_provincia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    pais = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    base_imponible = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cuota_iva = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    porcentaje_irpf = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    retencion_irpf = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tipo_factura = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rectifica_factura_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    huella = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    huella_anterior = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    id_registro = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    tipo_operacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    estado_envio_aeat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factura", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "linea_factura",
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
                    factura_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_linea_factura", x => x.id);
                    table.ForeignKey(
                        name: "FK_linea_factura_factura_factura_id",
                        column: x => x.factura_id,
                        principalSchema: "facturacion",
                        principalTable: "factura",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_factura_numero",
                schema: "facturacion",
                table: "factura",
                columns: new[] { "empresa_id", "prefijo", "ejercicio", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_linea_factura_factura_id",
                schema: "facturacion",
                table: "linea_factura",
                column: "factura_id");

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("facturacion", "factura"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("facturacion", "linea_factura"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("facturacion", "linea_factura"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("facturacion", "factura"));

            migrationBuilder.DropTable(
                name: "linea_factura",
                schema: "facturacion");

            migrationBuilder.DropTable(
                name: "factura",
                schema: "facturacion");
        }
    }
}
