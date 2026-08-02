using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlxorCore.Identidad.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class TokensCuentaUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "token_restablecimiento_expira",
                schema: "identidad",
                table: "usuario",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "token_restablecimiento_hash",
                schema: "identidad",
                table: "usuario",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "token_verificacion_expira",
                schema: "identidad",
                table: "usuario",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "token_verificacion_hash",
                schema: "identidad",
                table: "usuario",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_token_restablecimiento",
                schema: "identidad",
                table: "usuario",
                column: "token_restablecimiento_hash");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_token_verificacion",
                schema: "identidad",
                table: "usuario",
                column: "token_verificacion_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_usuario_token_restablecimiento",
                schema: "identidad",
                table: "usuario");

            migrationBuilder.DropIndex(
                name: "ix_usuario_token_verificacion",
                schema: "identidad",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "token_restablecimiento_expira",
                schema: "identidad",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "token_restablecimiento_hash",
                schema: "identidad",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "token_verificacion_expira",
                schema: "identidad",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "token_verificacion_hash",
                schema: "identidad",
                table: "usuario");
        }
    }
}
