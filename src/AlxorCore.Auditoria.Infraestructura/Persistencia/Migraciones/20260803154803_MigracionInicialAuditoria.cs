using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Auditoria.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class MigracionInicialAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "auditoria");

            migrationBuilder.CreateTable(
                name: "registro_auditoria",
                schema: "auditoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    accion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    metodo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ruta = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    codigo_estado = table.Column<int>(type: "integer", nullable: false),
                    ocurrido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registro_auditoria", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_empresa_fecha",
                schema: "auditoria",
                table: "registro_auditoria",
                columns: new[] { "empresa_id", "ocurrido_en" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("auditoria", "registro_auditoria"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("auditoria", "registro_auditoria"));

            migrationBuilder.DropTable(
                name: "registro_auditoria",
                schema: "auditoria");
        }
    }
}
