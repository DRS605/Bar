using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Gastos.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class MigracionInicialGastos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "gastos");

            migrationBuilder.CreateTable(
                name: "gasto",
                schema: "gastos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proveedor_texto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    concepto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    base_imponible = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    codigo_iva = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    porcentaje_iva = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    cuota_iva = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    porcentaje_irpf = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    retencion_irpf = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gasto", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gasto_empresa_fecha",
                schema: "gastos",
                table: "gasto",
                columns: new[] { "empresa_id", "fecha" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("gastos", "gasto"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("gastos", "gasto"));

            migrationBuilder.DropTable(
                name: "gasto",
                schema: "gastos");
        }
    }
}
