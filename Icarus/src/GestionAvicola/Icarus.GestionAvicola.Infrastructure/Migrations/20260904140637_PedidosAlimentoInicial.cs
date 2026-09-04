using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.GestionAvicola.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PedidosAlimentoInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pedidos_alimentos",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    FechaPedido = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaEntregaEstimada = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedidos_alimentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "detalles_pedidos_alimentos",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoAlimento = table.Column<int>(type: "int", nullable: false),
                    Presentacion = table.Column<int>(type: "int", nullable: false),
                    CantidadSolicitada = table.Column<int>(type: "int", nullable: false),
                    Equivalentes40Kg = table.Column<int>(type: "int", nullable: false),
                    PrecioFinalPor40Kg = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    NotificacionPreciosAlimentosId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubtotalSolicitado = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PedidoAlimentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalles_pedidos_alimentos", x => x.Id);
                    table.CheckConstraint("CK_detalles_pedidos_cantidad", "[CantidadSolicitada] > 0");
                    table.CheckConstraint("CK_detalles_pedidos_precio", "[PrecioFinalPor40Kg] IS NULL OR [PrecioFinalPor40Kg] > 0");
                    table.ForeignKey(
                        name: "FK_detalles_pedidos_alimentos_pedidos_alimentos_PedidoAlimentoId",
                        column: x => x.PedidoAlimentoId,
                        principalSchema: "gestion_avicola",
                        principalTable: "pedidos_alimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transiciones_pedidos_alimentos",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EstadoOrigen = table.Column<int>(type: "int", nullable: false),
                    EstadoDestino = table.Column<int>(type: "int", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaEntregaEstimada = table.Column<DateOnly>(type: "date", nullable: true),
                    PedidoAlimentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transiciones_pedidos_alimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transiciones_pedidos_alimentos_pedidos_alimentos_PedidoAlimentoId",
                        column: x => x.PedidoAlimentoId,
                        principalSchema: "gestion_avicola",
                        principalTable: "pedidos_alimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_detalles_pedidos_alimentos_PedidoAlimentoId_TipoAlimento",
                schema: "gestion_avicola",
                table: "detalles_pedidos_alimentos",
                columns: new[] { "PedidoAlimentoId", "TipoAlimento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_alimentos_ClienteId_FechaPedido",
                schema: "gestion_avicola",
                table: "pedidos_alimentos",
                columns: new[] { "ClienteId", "FechaPedido" });

            migrationBuilder.CreateIndex(
                name: "IX_transiciones_pedidos_alimentos_PedidoAlimentoId",
                schema: "gestion_avicola",
                table: "transiciones_pedidos_alimentos",
                column: "PedidoAlimentoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detalles_pedidos_alimentos",
                schema: "gestion_avicola");

            migrationBuilder.DropTable(
                name: "transiciones_pedidos_alimentos",
                schema: "gestion_avicola");

            migrationBuilder.DropTable(
                name: "pedidos_alimentos",
                schema: "gestion_avicola");
        }
    }
}
