using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.ControlAcceso.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NotificacionesInternasControlAcceso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notificaciones_internas_acceso",
                schema: "control_acceso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Leida = table.Column<bool>(type: "bit", nullable: false),
                    LeidaPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FechaLeidaUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificaciones_internas_acceso", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_internas_acceso_ClienteId_FechaUtc",
                schema: "control_acceso",
                table: "notificaciones_internas_acceso",
                columns: new[] { "ClienteId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_internas_acceso_IncidenciaId",
                schema: "control_acceso",
                table: "notificaciones_internas_acceso",
                column: "IncidenciaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notificaciones_internas_acceso",
                schema: "control_acceso");
        }
    }
}
