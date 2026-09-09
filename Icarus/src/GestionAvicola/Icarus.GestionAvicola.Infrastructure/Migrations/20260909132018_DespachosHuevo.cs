using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.GestionAvicola.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DespachosHuevo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "despachos_huevo",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GranjaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    FechaDespacho = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_despachos_huevo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "detalles_despacho_huevo",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tamano = table.Column<int>(type: "int", nullable: false),
                    CantidadAmarras = table.Column<int>(type: "int", nullable: false),
                    UnidadesSueltas = table.Column<int>(type: "int", nullable: false),
                    PrecioProductorCongelado = table.Column<decimal>(type: "decimal(10,4)", nullable: true),
                    PublicacionPrecioHuevoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DespachoHuevoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalles_despacho_huevo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_detalles_despacho_huevo_despachos_huevo_DespachoHuevoId",
                        column: x => x.DespachoHuevoId,
                        principalSchema: "gestion_avicola",
                        principalTable: "despachos_huevo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "documentos_despacho_huevo",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaveOriginal = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaveVista = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mime = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    TamanoVistaBytes = table.Column<long>(type: "bigint", nullable: false),
                    HashSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NombreSeguro = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DespachoHuevoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documentos_despacho_huevo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_documentos_despacho_huevo_despachos_huevo_DespachoHuevoId",
                        column: x => x.DespachoHuevoId,
                        principalSchema: "gestion_avicola",
                        principalTable: "despachos_huevo",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "transiciones_despacho_huevo",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origen = table.Column<int>(type: "int", nullable: false),
                    Destino = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DespachoHuevoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transiciones_despacho_huevo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transiciones_despacho_huevo_despachos_huevo_DespachoHuevoId",
                        column: x => x.DespachoHuevoId,
                        principalSchema: "gestion_avicola",
                        principalTable: "despachos_huevo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_despachos_huevo_ClienteId_FechaDespacho",
                schema: "gestion_avicola",
                table: "despachos_huevo",
                columns: new[] { "ClienteId", "FechaDespacho" });

            migrationBuilder.CreateIndex(
                name: "IX_despachos_huevo_Estado_ClienteId_FechaDespacho",
                schema: "gestion_avicola",
                table: "despachos_huevo",
                columns: new[] { "Estado", "ClienteId", "FechaDespacho" });

            migrationBuilder.CreateIndex(
                name: "IX_detalles_despacho_huevo_DespachoHuevoId_Tamano",
                schema: "gestion_avicola",
                table: "detalles_despacho_huevo",
                columns: new[] { "DespachoHuevoId", "Tamano" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documentos_despacho_huevo_DespachoHuevoId",
                schema: "gestion_avicola",
                table: "documentos_despacho_huevo",
                column: "DespachoHuevoId",
                unique: true,
                filter: "[DespachoHuevoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_transiciones_despacho_huevo_DespachoHuevoId",
                schema: "gestion_avicola",
                table: "transiciones_despacho_huevo",
                column: "DespachoHuevoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detalles_despacho_huevo",
                schema: "gestion_avicola");

            migrationBuilder.DropTable(
                name: "documentos_despacho_huevo",
                schema: "gestion_avicola");

            migrationBuilder.DropTable(
                name: "transiciones_despacho_huevo",
                schema: "gestion_avicola");

            migrationBuilder.DropTable(
                name: "despachos_huevo",
                schema: "gestion_avicola");
        }
    }
}
