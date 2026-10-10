using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Hosteleria.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Promociones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "promocion",
                schema: "hosteleria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    porcentaje = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    ambito = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    categoria = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dias = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    hora_inicio = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    hora_fin = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promocion", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_promocion_empresa",
                schema: "hosteleria",
                table: "promocion",
                column: "empresa_id");

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("hosteleria", "promocion"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("hosteleria", "promocion"));

            migrationBuilder.DropTable(
                name: "promocion",
                schema: "hosteleria");
        }
    }
}
