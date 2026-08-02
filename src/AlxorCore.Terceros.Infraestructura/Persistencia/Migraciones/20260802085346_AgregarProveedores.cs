using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Terceros.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarProveedores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "proveedor",
                schema: "terceros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nif_fiscal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    direccion_calle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    direccion_cp = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    direccion_poblacion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    direccion_provincia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    direccion_pais = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    irpf_defecto = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proveedor", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_proveedor_empresa_nombre",
                schema: "terceros",
                table: "proveedor",
                columns: new[] { "empresa_id", "nombre" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("terceros", "proveedor"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("terceros", "proveedor"));

            migrationBuilder.DropTable(
                name: "proveedor",
                schema: "terceros");
        }
    }
}
