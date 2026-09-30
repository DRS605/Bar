using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Hosteleria.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class FichaCarta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ficha_carta",
                schema: "hosteleria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alergenos = table.Column<int>(type: "integer", nullable: false),
                    foto = table.Column<byte[]>(type: "bytea", nullable: true),
                    foto_tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ficha_carta", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_ficha_carta_producto",
                schema: "hosteleria",
                table: "ficha_carta",
                columns: new[] { "empresa_id", "producto_id" },
                unique: true);

            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("hosteleria", "ficha_carta"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("hosteleria", "ficha_carta"));

            migrationBuilder.DropTable(
                name: "ficha_carta",
                schema: "hosteleria");
        }
    }
}
