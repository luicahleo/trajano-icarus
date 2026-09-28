using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.ControlAcceso.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OperacionMarcacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operaciones_marcacion",
                schema: "control_acceso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabajadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaveIdempotencia = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Accion = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    JornadaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreadaEnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiraEnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operaciones_marcacion", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operaciones_marcacion_ClienteId_ClaveIdempotencia",
                schema: "control_acceso",
                table: "operaciones_marcacion",
                columns: new[] { "ClienteId", "ClaveIdempotencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operaciones_marcacion_ClienteId_TrabajadorId",
                schema: "control_acceso",
                table: "operaciones_marcacion",
                columns: new[] { "ClienteId", "TrabajadorId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operaciones_marcacion",
                schema: "control_acceso");
        }
    }
}
