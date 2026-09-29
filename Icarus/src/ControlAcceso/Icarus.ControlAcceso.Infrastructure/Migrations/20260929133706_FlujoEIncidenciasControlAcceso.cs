using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.ControlAcceso.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FlujoEIncidenciasControlAcceso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "flujos_marcacion",
                schema: "control_acceso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SesionKioscoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Accion = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    CreadaEnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinalizadaEnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flujos_marcacion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "incidencias_acceso",
                schema: "control_acceso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlujoMarcacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SesionKioscoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Accion = table.Column<int>(type: "int", nullable: false),
                    PrimerRechazoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TercerRechazoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    TrabajadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JornadaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MotivoResolucion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResueltaEnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreadaEnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incidencias_acceso", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "capturas_marcacion",
                schema: "control_acceso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaveCaptura = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Resultado = table.Column<int>(type: "int", nullable: false),
                    TrabajadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InstanteUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FlujoMarcacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capturas_marcacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_capturas_marcacion_flujos_marcacion_FlujoMarcacionId",
                        column: x => x.FlujoMarcacionId,
                        principalSchema: "control_acceso",
                        principalTable: "flujos_marcacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_capturas_marcacion_ClaveCaptura",
                schema: "control_acceso",
                table: "capturas_marcacion",
                column: "ClaveCaptura",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_capturas_marcacion_FlujoMarcacionId_ClaveCaptura",
                schema: "control_acceso",
                table: "capturas_marcacion",
                columns: new[] { "FlujoMarcacionId", "ClaveCaptura" },
                unique: true,
                filter: "[FlujoMarcacionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_flujos_marcacion_ClienteId",
                schema: "control_acceso",
                table: "flujos_marcacion",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_flujos_marcacion_ClienteId_SesionKioscoId_Estado",
                schema: "control_acceso",
                table: "flujos_marcacion",
                columns: new[] { "ClienteId", "SesionKioscoId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_incidencias_acceso_ClienteId_Estado_TercerRechazoUtc",
                schema: "control_acceso",
                table: "incidencias_acceso",
                columns: new[] { "ClienteId", "Estado", "TercerRechazoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_incidencias_acceso_FlujoMarcacionId",
                schema: "control_acceso",
                table: "incidencias_acceso",
                column: "FlujoMarcacionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "capturas_marcacion",
                schema: "control_acceso");

            migrationBuilder.DropTable(
                name: "incidencias_acceso",
                schema: "control_acceso");

            migrationBuilder.DropTable(
                name: "flujos_marcacion",
                schema: "control_acceso");
        }
    }
}
