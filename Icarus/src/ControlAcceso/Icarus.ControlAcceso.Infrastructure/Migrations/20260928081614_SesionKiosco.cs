using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.ControlAcceso.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SesionKiosco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sesiones_kiosco",
                schema: "control_acceso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HashCredencial = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreadaEnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiraEnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevocadaEnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EstaActiva = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sesiones_kiosco", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_kiosco_ClienteId_EstaActiva",
                schema: "control_acceso",
                table: "sesiones_kiosco",
                columns: new[] { "ClienteId", "EstaActiva" });

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_kiosco_HashCredencial",
                schema: "control_acceso",
                table: "sesiones_kiosco",
                column: "HashCredencial",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sesiones_kiosco",
                schema: "control_acceso");
        }
    }
}
