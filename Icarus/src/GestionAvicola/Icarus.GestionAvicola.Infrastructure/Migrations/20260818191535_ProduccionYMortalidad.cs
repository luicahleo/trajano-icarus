using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.GestionAvicola.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProduccionYMortalidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "registros_mortalidad",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GalponId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Hora = table.Column<TimeOnly>(type: "time", nullable: false),
                    CantidadMuertas = table.Column<int>(type: "int", nullable: false),
                    GallinasVivas = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registros_mortalidad", x => x.Id);
                    table.CheckConstraint("CK_registros_mortalidad_cantidad", "[CantidadMuertas] > 0");
                });

            migrationBuilder.CreateTable(
                name: "registros_produccion",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GalponId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Hora = table.Column<TimeOnly>(type: "time", nullable: false),
                    CantidadMaples = table.Column<int>(type: "int", nullable: false),
                    UnidadesIncompletas = table.Column<int>(type: "int", nullable: false),
                    MaplesDescarte = table.Column<int>(type: "int", nullable: false),
                    UnidadesDescarte = table.Column<int>(type: "int", nullable: false),
                    GallinasVivas = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registros_produccion", x => x.Id);
                    table.CheckConstraint("CK_registros_produccion_maples", "[CantidadMaples] >= 0 AND [MaplesDescarte] >= 0");
                    table.CheckConstraint("CK_registros_produccion_sueltos", "[UnidadesIncompletas] >= 0 AND [UnidadesIncompletas] < 30 AND [UnidadesDescarte] >= 0 AND [UnidadesDescarte] < 30");
                });

            migrationBuilder.CreateIndex(
                name: "IX_registros_mortalidad_ClienteId",
                schema: "gestion_avicola",
                table: "registros_mortalidad",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_registros_mortalidad_GalponId_Fecha",
                schema: "gestion_avicola",
                table: "registros_mortalidad",
                columns: new[] { "GalponId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_registros_mortalidad_IdempotencyKey",
                schema: "gestion_avicola",
                table: "registros_mortalidad",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_registros_produccion_ClienteId",
                schema: "gestion_avicola",
                table: "registros_produccion",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_registros_produccion_GalponId_Fecha",
                schema: "gestion_avicola",
                table: "registros_produccion",
                columns: new[] { "GalponId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_registros_produccion_IdempotencyKey",
                schema: "gestion_avicola",
                table: "registros_produccion",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registros_mortalidad",
                schema: "gestion_avicola");

            migrationBuilder.DropTable(
                name: "registros_produccion",
                schema: "gestion_avicola");
        }
    }
}
