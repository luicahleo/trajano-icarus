using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.ControlAcceso.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OperacionEnrolamiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operaciones_enrolamiento",
                schema: "control_acceso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabajadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaveIdempotencia = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    VersionEnrolamiento = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreadaEnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operaciones_enrolamiento", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operaciones_enrolamiento_ClienteId_ClaveIdempotencia",
                schema: "control_acceso",
                table: "operaciones_enrolamiento",
                columns: new[] { "ClienteId", "ClaveIdempotencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operaciones_enrolamiento_ClienteId_TrabajadorId",
                schema: "control_acceso",
                table: "operaciones_enrolamiento",
                columns: new[] { "ClienteId", "TrabajadorId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operaciones_enrolamiento",
                schema: "control_acceso");
        }
    }
}
