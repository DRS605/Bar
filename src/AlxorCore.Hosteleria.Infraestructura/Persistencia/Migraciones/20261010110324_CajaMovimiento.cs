using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Hosteleria.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CajaMovimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "caja_movimiento",
                schema: "hosteleria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    importe = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    concepto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_nombre = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    momento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_caja_movimiento", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_caja_movimiento_empresa_fecha",
                schema: "hosteleria",
                table: "caja_movimiento",
                columns: new[] { "empresa_id", "fecha" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("hosteleria", "caja_movimiento"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("hosteleria", "caja_movimiento"));

            migrationBuilder.DropTable(
                name: "caja_movimiento",
                schema: "hosteleria");
        }
    }
}
