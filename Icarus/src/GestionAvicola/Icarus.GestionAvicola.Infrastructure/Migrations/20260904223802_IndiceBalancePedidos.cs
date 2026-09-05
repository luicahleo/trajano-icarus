using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.GestionAvicola.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IndiceBalancePedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_pedidos_alimentos_Estado_ClienteId_FechaPedido",
                schema: "gestion_avicola",
                table: "pedidos_alimentos",
                columns: new[] { "Estado", "ClienteId", "FechaPedido" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pedidos_alimentos_Estado_ClienteId_FechaPedido",
                schema: "gestion_avicola",
                table: "pedidos_alimentos");
        }
    }
}
