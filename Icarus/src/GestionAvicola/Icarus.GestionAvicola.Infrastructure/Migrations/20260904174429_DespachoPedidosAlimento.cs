using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.GestionAvicola.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DespachoPedidosAlimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "entregas_pedidos_alimentos",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroNota = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FechaNota = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaDespacho = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalNetoInformado = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PedidoAlimentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entregas_pedidos_alimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_entregas_pedidos_alimentos_pedidos_alimentos_PedidoAlimentoId",
                        column: x => x.PedidoAlimentoId,
                        principalSchema: "gestion_avicola",
                        principalTable: "pedidos_alimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "detalles_entregas_pedidos_alimentos",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoAlimento = table.Column<int>(type: "int", nullable: false),
                    Presentacion = table.Column<int>(type: "int", nullable: false),
                    CantidadEntregada = table.Column<int>(type: "int", nullable: false),
                    Equivalentes40Kg = table.Column<int>(type: "int", nullable: false),
                    EntregaPedidoAlimentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalles_entregas_pedidos_alimentos", x => x.Id);
                    table.CheckConstraint("CK_detalles_entregas_cantidad", "[CantidadEntregada] >= 0");
                    table.ForeignKey(
                        name: "FK_detalles_entregas_pedidos_alimentos_entregas_pedidos_alimentos_EntregaPedidoAlimentoId",
                        column: x => x.EntregaPedidoAlimentoId,
                        principalSchema: "gestion_avicola",
                        principalTable: "entregas_pedidos_alimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_detalles_entregas_pedidos_alimentos_EntregaPedidoAlimentoId_TipoAlimento",
                schema: "gestion_avicola",
                table: "detalles_entregas_pedidos_alimentos",
                columns: new[] { "EntregaPedidoAlimentoId", "TipoAlimento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entregas_pedidos_alimentos_PedidoAlimentoId",
                schema: "gestion_avicola",
                table: "entregas_pedidos_alimentos",
                column: "PedidoAlimentoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detalles_entregas_pedidos_alimentos",
                schema: "gestion_avicola");

            migrationBuilder.DropTable(
                name: "entregas_pedidos_alimentos",
                schema: "gestion_avicola");
        }
    }
}
