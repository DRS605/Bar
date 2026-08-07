using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Hosteleria.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class MigracionInicialHosteleria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hosteleria");

            migrationBuilder.CreateTable(
                name: "comanda",
                schema: "hosteleria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mesa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    abierta_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cerrada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    base_imponible = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    cuota_iva = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    metodo_cobro = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    factura_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numero_ticket = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comanda", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mesa",
                schema: "hosteleria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    zona = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    capacidad = table.Column<int>(type: "integer", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mesa", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "linea_comanda",
                schema: "hosteleria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comanda_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(14,3)", nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    codigo_iva = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    porcentaje_iva = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    @base = table.Column<decimal>(name: "base", type: "numeric(14,2)", nullable: false),
                    cuota_iva = table.Column<decimal>(type: "numeric(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_linea_comanda", x => x.id);
                    table.ForeignKey(
                        name: "FK_linea_comanda_comanda_comanda_id",
                        column: x => x.comanda_id,
                        principalSchema: "hosteleria",
                        principalTable: "comanda",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_comanda_empresa_estado_mesa",
                schema: "hosteleria",
                table: "comanda",
                columns: new[] { "empresa_id", "estado", "mesa_id" });

            migrationBuilder.CreateIndex(
                name: "ix_linea_comanda_comanda",
                schema: "hosteleria",
                table: "linea_comanda",
                column: "comanda_id");

            migrationBuilder.CreateIndex(
                name: "ix_mesa_empresa_nombre",
                schema: "hosteleria",
                table: "mesa",
                columns: new[] { "empresa_id", "nombre" });

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("hosteleria", "mesa"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("hosteleria", "comanda"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("hosteleria", "linea_comanda"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("hosteleria", "linea_comanda"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("hosteleria", "comanda"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("hosteleria", "mesa"));

            migrationBuilder.DropTable(
                name: "linea_comanda",
                schema: "hosteleria");

            migrationBuilder.DropTable(
                name: "mesa",
                schema: "hosteleria");

            migrationBuilder.DropTable(
                name: "comanda",
                schema: "hosteleria");
        }
    }
}
