using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Organizacion.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class MigracionInicialOrganizacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "organizacion");

            migrationBuilder.CreateTable(
                name: "empresa",
                schema: "organizacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nif = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    razon_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    direccion_calle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    direccion_cp = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    direccion_poblacion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    direccion_provincia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    direccion_pais = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    regimen_iva = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    pais = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empresa", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "membresia",
                schema: "organizacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol_codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membresia", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "serie_numeracion",
                schema: "organizacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ejercicio = table.Column<int>(type: "integer", nullable: false),
                    prefijo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    siguiente_numero = table.Column<long>(type: "bigint", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_serie_numeracion", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_empresa_nif",
                schema: "organizacion",
                table: "empresa",
                column: "nif",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_membresia_usuario_empresa",
                schema: "organizacion",
                table: "membresia",
                columns: new[] { "usuario_id", "empresa_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_serie_empresa_tipo_ejercicio_prefijo",
                schema: "organizacion",
                table: "serie_numeracion",
                columns: new[] { "empresa_id", "tipo_documento", "ejercicio", "prefijo" },
                unique: true);

            // Row-Level Security por empresa sobre la tabla multiempresa (segunda barrera de aislamiento).
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("organizacion", "serie_numeracion"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("organizacion", "serie_numeracion"));

            migrationBuilder.DropTable(
                name: "empresa",
                schema: "organizacion");

            migrationBuilder.DropTable(
                name: "membresia",
                schema: "organizacion");

            migrationBuilder.DropTable(
                name: "serie_numeracion",
                schema: "organizacion");
        }
    }
}
