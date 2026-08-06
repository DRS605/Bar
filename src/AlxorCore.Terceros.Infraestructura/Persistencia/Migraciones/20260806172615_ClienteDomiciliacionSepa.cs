using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Terceros.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ClienteDomiciliacionSepa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "iban",
                schema: "terceros",
                table: "cliente",
                type: "character varying(34)",
                maxLength: 34,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "mandato_fecha",
                schema: "terceros",
                table: "cliente",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mandato_referencia",
                schema: "terceros",
                table: "cliente",
                type: "character varying(35)",
                maxLength: 35,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "iban",
                schema: "terceros",
                table: "cliente");

            migrationBuilder.DropColumn(
                name: "mandato_fecha",
                schema: "terceros",
                table: "cliente");

            migrationBuilder.DropColumn(
                name: "mandato_referencia",
                schema: "terceros",
                table: "cliente");
        }
    }
}
