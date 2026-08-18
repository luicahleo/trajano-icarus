using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.GestionAvicola.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InicialGestionAvicola : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "gestion_avicola");

            migrationBuilder.CreateTable(
                name: "galpones",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GranjaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CapacidadMaxima = table.Column<int>(type: "int", nullable: false),
                    GallinasActuales = table.Column<int>(type: "int", nullable: false),
                    FechaNacimientoLote = table.Column<DateOnly>(type: "date", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_galpones", x => x.Id);
                    table.CheckConstraint("CK_galpones_capacidad", "[CapacidadMaxima] > 0");
                    table.CheckConstraint("CK_galpones_inventario", "[GallinasActuales] >= 0 AND [GallinasActuales] <= [CapacidadMaxima]");
                });

            migrationBuilder.CreateTable(
                name: "granjas",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_granjas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_galpones_ClienteId",
                schema: "gestion_avicola",
                table: "galpones",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_galpones_GranjaId_Numero",
                schema: "gestion_avicola",
                table: "galpones",
                columns: new[] { "GranjaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_granjas_ClienteId",
                schema: "gestion_avicola",
                table: "granjas",
                column: "ClienteId",
                unique: true,
                filter: "[EstaActivo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_granjas_ClienteId_Nombre",
                schema: "gestion_avicola",
                table: "granjas",
                columns: new[] { "ClienteId", "Nombre" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "galpones",
                schema: "gestion_avicola");

            migrationBuilder.DropTable(
                name: "granjas",
                schema: "gestion_avicola");
        }
    }
}
