using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.GestionAvicola.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenombrarPrecioUnitarioCongelado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PrecioProductorCongelado",
                schema: "gestion_avicola",
                table: "detalles_despacho_huevo",
                newName: "PrecioUnitarioCongelado");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PrecioUnitarioCongelado",
                schema: "gestion_avicola",
                table: "detalles_despacho_huevo",
                newName: "PrecioProductorCongelado");
        }
    }
}
