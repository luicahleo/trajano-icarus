using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.GestionAvicola.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecepcionPedidosAlimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recepciones_pedidos_alimentos",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaRecepcion = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalRecibido = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiferenciasJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PedidoAlimentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recepciones_pedidos_alimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recepciones_pedidos_alimentos_pedidos_alimentos_PedidoAlimentoId",
                        column: x => x.PedidoAlimentoId,
                        principalSchema: "gestion_avicola",
                        principalTable: "pedidos_alimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "detalles_recepciones_pedidos_alimentos",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoAlimento = table.Column<int>(type: "int", nullable: false),
                    Presentacion = table.Column<int>(type: "int", nullable: false),
                    CantidadRecibida = table.Column<int>(type: "int", nullable: false),
                    Equivalentes40Kg = table.Column<int>(type: "int", nullable: false),
                    RecepcionPedidoAlimentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalles_recepciones_pedidos_alimentos", x => x.Id);
                    table.CheckConstraint("CK_detalles_recepciones_cantidad", "[CantidadRecibida] >= 0");
                    table.ForeignKey(
                        name: "FK_detalles_recepciones_pedidos_alimentos_recepciones_pedidos_alimentos_RecepcionPedidoAlimentoId",
                        column: x => x.RecepcionPedidoAlimentoId,
                        principalSchema: "gestion_avicola",
                        principalTable: "recepciones_pedidos_alimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_detalles_recepciones_pedidos_alimentos_RecepcionPedidoAlimentoId_TipoAlimento",
                schema: "gestion_avicola",
                table: "detalles_recepciones_pedidos_alimentos",
                columns: new[] { "RecepcionPedidoAlimentoId", "TipoAlimento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recepciones_pedidos_alimentos_PedidoAlimentoId",
                schema: "gestion_avicola",
                table: "recepciones_pedidos_alimentos",
                column: "PedidoAlimentoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detalles_recepciones_pedidos_alimentos",
                schema: "gestion_avicola");

            migrationBuilder.DropTable(
                name: "recepciones_pedidos_alimentos",
                schema: "gestion_avicola");
        }
    }
}
