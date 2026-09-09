using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.GestionAvicola.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PreciosHuevo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "publicaciones_precios_huevo",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaNotificacion = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaVigencia = table.Column<DateOnly>(type: "date", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    DocumentoOriginalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Servicio = table.Column<decimal>(type: "decimal(10,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_publicaciones_precios_huevo", x => x.Id);
                    table.CheckConstraint("CK_publicaciones_precios_huevo_servicio", "[Servicio] > 0");
                });

            migrationBuilder.CreateTable(
                name: "detalles_precio_huevo",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tamano = table.Column<int>(type: "int", nullable: false),
                    PrecioAlProductor = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    PrecioActualDocumento = table.Column<decimal>(type: "decimal(10,4)", nullable: true),
                    PublicacionPrecioHuevoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalles_precio_huevo", x => x.Id);
                    table.CheckConstraint("CK_detalles_precio_huevo_productor", "[PrecioAlProductor] > 0");
                    table.ForeignKey(
                        name: "FK_detalles_precio_huevo_publicaciones_precios_huevo_PublicacionPrecioHuevoId",
                        column: x => x.PublicacionPrecioHuevoId,
                        principalSchema: "gestion_avicola",
                        principalTable: "publicaciones_precios_huevo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_detalles_precio_huevo_PublicacionPrecioHuevoId_Tamano",
                schema: "gestion_avicola",
                table: "detalles_precio_huevo",
                columns: new[] { "PublicacionPrecioHuevoId", "Tamano" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_publicaciones_precios_huevo_FechaVigencia",
                schema: "gestion_avicola",
                table: "publicaciones_precios_huevo",
                column: "FechaVigencia",
                unique: true,
                filter: "[Estado] = 1 AND [EstaActivo] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detalles_precio_huevo",
                schema: "gestion_avicola");

            migrationBuilder.DropTable(
                name: "publicaciones_precios_huevo",
                schema: "gestion_avicola");
        }
    }
}
