using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Tesoreria.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class MigracionInicialTesoreria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tesoreria");

            migrationBuilder.CreateTable(
                name: "movimiento",
                schema: "tesoreria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sentido = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    importe = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    metodo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimiento", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_documento",
                schema: "tesoreria",
                table: "movimiento",
                columns: new[] { "empresa_id", "tipo_documento", "documento_id" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("tesoreria", "movimiento"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("tesoreria", "movimiento"));

            migrationBuilder.DropTable(
                name: "movimiento",
                schema: "tesoreria");
        }
    }
}
