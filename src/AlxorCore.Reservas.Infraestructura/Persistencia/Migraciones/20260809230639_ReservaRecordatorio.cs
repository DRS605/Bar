using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Reservas.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ReservaRecordatorio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "recordatorio_enviado_en",
                schema: "reservas",
                table: "reserva",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "recordatorio_enviado_en",
                schema: "reservas",
                table: "reserva");
        }
    }
}
