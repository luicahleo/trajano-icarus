using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.ControlAcceso.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MarcacionManual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AutorId",
                schema: "control_acceso",
                table: "marcaciones",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreadaEnUtc",
                schema: "control_acceso",
                table: "marcaciones",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HoraDeclaradaUtc",
                schema: "control_acceso",
                table: "marcaciones",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Motivo",
                schema: "control_acceso",
                table: "marcaciones",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutorId",
                schema: "control_acceso",
                table: "marcaciones");

            migrationBuilder.DropColumn(
                name: "CreadaEnUtc",
                schema: "control_acceso",
                table: "marcaciones");

            migrationBuilder.DropColumn(
                name: "HoraDeclaradaUtc",
                schema: "control_acceso",
                table: "marcaciones");

            migrationBuilder.DropColumn(
                name: "Motivo",
                schema: "control_acceso",
                table: "marcaciones");
        }
    }
}
