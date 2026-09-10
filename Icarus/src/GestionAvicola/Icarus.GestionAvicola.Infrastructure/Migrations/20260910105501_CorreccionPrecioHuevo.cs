using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.GestionAvicola.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CorreccionPrecioHuevo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Motivo",
                schema: "gestion_avicola",
                table: "publicaciones_precios_huevo",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublicacionCorrectivaId",
                schema: "gestion_avicola",
                table: "publicaciones_precios_huevo",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ajustes_credito_huevo",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DespachoHuevoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicacionErroneaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicacionCorrectivaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreadoEnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ajustes_credito_huevo", x => x.Id);
                    table.CheckConstraint("CK_ajustes_credito_huevo_monto", "[Monto] <> 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ajustes_credito_huevo_ClienteId",
                schema: "gestion_avicola",
                table: "ajustes_credito_huevo",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ajustes_credito_huevo_DespachoHuevoId",
                schema: "gestion_avicola",
                table: "ajustes_credito_huevo",
                column: "DespachoHuevoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ajustes_credito_huevo",
                schema: "gestion_avicola");

            migrationBuilder.DropColumn(
                name: "Motivo",
                schema: "gestion_avicola",
                table: "publicaciones_precios_huevo");

            migrationBuilder.DropColumn(
                name: "PublicacionCorrectivaId",
                schema: "gestion_avicola",
                table: "publicaciones_precios_huevo");
        }
    }
}
