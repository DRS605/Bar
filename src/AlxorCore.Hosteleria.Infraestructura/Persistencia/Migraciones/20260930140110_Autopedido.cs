using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Hosteleria.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Autopedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Las mesas ya existentes reciben un token aleatorio (no el Guid vacío), para que su QR de
            // autopedido sea válido sin tener que regenerarlo a mano.
            migrationBuilder.AddColumn<Guid>(
                name: "token_carta",
                schema: "hosteleria",
                table: "mesa",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.CreateTable(
                name: "aviso_mesa",
                schema: "hosteleria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mesa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recibido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atendido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aviso_mesa", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pedido_web",
                schema: "hosteleria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mesa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idioma = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recibido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resuelto_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    comanda_id = table.Column<Guid>(type: "uuid", nullable: true),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedido_web", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "traduccion_carta",
                schema: "hosteleria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ambito = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    clave = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    idioma = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_traduccion_carta", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "linea_pedido_web",
                schema: "hosteleria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_web_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(14,3)", nullable: false),
                    nota = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_linea_pedido_web", x => x.id);
                    table.ForeignKey(
                        name: "FK_linea_pedido_web_pedido_web_pedido_web_id",
                        column: x => x.pedido_web_id,
                        principalSchema: "hosteleria",
                        principalTable: "pedido_web",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_aviso_mesa_empresa_atendido",
                schema: "hosteleria",
                table: "aviso_mesa",
                columns: new[] { "empresa_id", "atendido_en" });

            migrationBuilder.CreateIndex(
                name: "ix_linea_pedido_web_pedido",
                schema: "hosteleria",
                table: "linea_pedido_web",
                column: "pedido_web_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedido_web_empresa_estado",
                schema: "hosteleria",
                table: "pedido_web",
                columns: new[] { "empresa_id", "estado", "recibido_en" });

            migrationBuilder.CreateIndex(
                name: "ux_traduccion_carta",
                schema: "hosteleria",
                table: "traduccion_carta",
                columns: new[] { "empresa_id", "ambito", "clave", "idioma" },
                unique: true);

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("hosteleria", "pedido_web"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("hosteleria", "linea_pedido_web"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("hosteleria", "aviso_mesa"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("hosteleria", "traduccion_carta"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("hosteleria", "traduccion_carta"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("hosteleria", "aviso_mesa"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("hosteleria", "linea_pedido_web"));
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("hosteleria", "pedido_web"));

            migrationBuilder.DropTable(
                name: "aviso_mesa",
                schema: "hosteleria");

            migrationBuilder.DropTable(
                name: "linea_pedido_web",
                schema: "hosteleria");

            migrationBuilder.DropTable(
                name: "traduccion_carta",
                schema: "hosteleria");

            migrationBuilder.DropTable(
                name: "pedido_web",
                schema: "hosteleria");

            migrationBuilder.DropColumn(
                name: "token_carta",
                schema: "hosteleria",
                table: "mesa");
        }
    }
}
