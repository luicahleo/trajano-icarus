using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.GestionAvicola.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Vacunacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "programas_vacunacion",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FechaEmision = table.Column<DateOnly>(type: "date", nullable: false),
                    CantidadAves = table.Column<int>(type: "int", nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programas_vacunacion", x => x.Id);
                    table.CheckConstraint("CK_programas_vacunacion_cantidad_aves", "[CantidadAves] > 0");
                });

            migrationBuilder.CreateTable(
                name: "tareas_vacunacion",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GalponId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramaVacunacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemPlanVacunacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EdadDia = table.Column<int>(type: "int", nullable: false),
                    Vacuna = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ModoAplicacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ObservacionesProgramadas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FechaProgramada = table.Column<DateOnly>(type: "date", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaAplicacion = table.Column<DateOnly>(type: "date", nullable: true),
                    AvesVacunadas = table.Column<int>(type: "int", nullable: true),
                    CompletadaPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ObservacionesAplicacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MotivoCancelacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tareas_vacunacion", x => x.Id);
                    table.CheckConstraint("CK_tareas_vacunacion_aves", "[AvesVacunadas] IS NULL OR [AvesVacunadas] > 0");
                    table.CheckConstraint("CK_tareas_vacunacion_edad", "[EdadDia] > 0");
                    table.CheckConstraint("CK_tareas_vacunacion_estado_fecha", "[Estado] <> 'Completada' OR [FechaAplicacion] IS NOT NULL");
                });

            migrationBuilder.CreateTable(
                name: "programas_vacunacion_items",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EdadDia = table.Column<int>(type: "int", nullable: false),
                    Vacuna = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ModoAplicacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false),
                    ProgramaVacunacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programas_vacunacion_items", x => x.Id);
                    table.CheckConstraint("CK_programas_vacunacion_items_edad", "[EdadDia] > 0");
                    table.ForeignKey(
                        name: "FK_programas_vacunacion_items_programas_vacunacion_ProgramaVacunacionId",
                        column: x => x.ProgramaVacunacionId,
                        principalSchema: "gestion_avicola",
                        principalTable: "programas_vacunacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_programas_vacunacion_Nombre",
                schema: "gestion_avicola",
                table: "programas_vacunacion",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_programas_vacunacion_items_ProgramaVacunacionId",
                schema: "gestion_avicola",
                table: "programas_vacunacion_items",
                column: "ProgramaVacunacionId");

            migrationBuilder.CreateIndex(
                name: "IX_tareas_vacunacion_ClienteId_FechaProgramada",
                schema: "gestion_avicola",
                table: "tareas_vacunacion",
                columns: new[] { "ClienteId", "FechaProgramada" });

            migrationBuilder.CreateIndex(
                name: "IX_tareas_vacunacion_GalponId_Estado",
                schema: "gestion_avicola",
                table: "tareas_vacunacion",
                columns: new[] { "GalponId", "Estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "programas_vacunacion_items",
                schema: "gestion_avicola");

            migrationBuilder.DropTable(
                name: "tareas_vacunacion",
                schema: "gestion_avicola");

            migrationBuilder.DropTable(
                name: "programas_vacunacion",
                schema: "gestion_avicola");
        }
    }
}
