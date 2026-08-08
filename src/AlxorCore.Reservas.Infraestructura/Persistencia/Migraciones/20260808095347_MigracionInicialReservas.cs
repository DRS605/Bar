using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Reservas.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class MigracionInicialReservas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reservas");

            migrationBuilder.CreateTable(
                name: "agenda_calendario",
                schema: "reservas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agenda_calendario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reserva",
                schema: "reservas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_cliente = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    telefono = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    fecha_hora = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duracion_minutos = table.Column<int>(type: "integer", nullable: false),
                    comensales = table.Column<int>(type: "integer", nullable: false),
                    mesa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comanda_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reserva", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_agenda_empresa",
                schema: "reservas",
                table: "agenda_calendario",
                column: "empresa_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_agenda_token",
                schema: "reservas",
                table: "agenda_calendario",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reserva_empresa_fecha",
                schema: "reservas",
                table: "reserva",
                columns: new[] { "empresa_id", "fecha_hora" });

            // La reserva es multiempresa (RLS por empresa). La tabla «agenda_calendario» NO lleva RLS:
            // es el propio token secreto el que resuelve la empresa en el feed público de calendario.
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Activar("reservas", "reserva"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AlxorCore.Persistencia.RlsSql.Desactivar("reservas", "reserva"));

            migrationBuilder.DropTable(
                name: "agenda_calendario",
                schema: "reservas");

            migrationBuilder.DropTable(
                name: "reserva",
                schema: "reservas");
        }
    }
}
