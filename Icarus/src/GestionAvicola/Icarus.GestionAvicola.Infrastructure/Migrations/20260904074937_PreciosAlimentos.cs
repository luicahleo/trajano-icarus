using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.GestionAvicola.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PreciosAlimentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notificaciones_precios_alimentos",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaDocumento = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenteDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    DocumentoOriginalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AporteCaisy = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Fondo = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Servicios = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificaciones_precios_alimentos", x => x.Id);
                    table.CheckConstraint("CK_notificaciones_precios_aportes", "[AporteCaisy] > 0 AND [Fondo] > 0 AND [Servicios] > 0");
                });

            migrationBuilder.CreateTable(
                name: "detalles_precio_alimento",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoAlimento = table.Column<int>(type: "int", nullable: false),
                    Presentacion = table.Column<int>(type: "int", nullable: false),
                    PrecioFinalPor40Kg = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    EdadDesdeDias = table.Column<int>(type: "int", nullable: true),
                    EdadHastaDias = table.Column<int>(type: "int", nullable: true),
                    PrecioActualDocumento = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    NotificacionPreciosAlimentosId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalles_precio_alimento", x => x.Id);
                    table.CheckConstraint("CK_detalles_precio_final", "[PrecioFinalPor40Kg] > 0");
                    table.ForeignKey(
                        name: "FK_detalles_precio_alimento_notificaciones_precios_alimentos_NotificacionPreciosAlimentosId",
                        column: x => x.NotificacionPreciosAlimentosId,
                        principalSchema: "gestion_avicola",
                        principalTable: "notificaciones_precios_alimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_detalles_precio_alimento_NotificacionPreciosAlimentosId_TipoAlimento_Presentacion",
                schema: "gestion_avicola",
                table: "detalles_precio_alimento",
                columns: new[] { "NotificacionPreciosAlimentosId", "TipoAlimento", "Presentacion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_precios_alimentos_VigenteDesde",
                schema: "gestion_avicola",
                table: "notificaciones_precios_alimentos",
                column: "VigenteDesde",
                unique: true,
                filter: "[Estado] = 1 AND [EstaActivo] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detalles_precio_alimento",
                schema: "gestion_avicola");

            migrationBuilder.DropTable(
                name: "notificaciones_precios_alimentos",
                schema: "gestion_avicola");
        }
    }
}
