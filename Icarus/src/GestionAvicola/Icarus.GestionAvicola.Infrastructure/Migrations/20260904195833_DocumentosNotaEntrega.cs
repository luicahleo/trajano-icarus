using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.GestionAvicola.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DocumentosNotaEntrega : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documentos_nota_entrega",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaveOriginal = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaveVista = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mime = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    TamanoVistaBytes = table.Column<long>(type: "bigint", nullable: false),
                    HashSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NombreSeguro = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    ReemplazadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FechaDesactivacionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EntregaPedidoAlimentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documentos_nota_entrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_documentos_nota_entrega_documentos_nota_entrega_ReemplazadoPorId",
                        column: x => x.ReemplazadoPorId,
                        principalSchema: "gestion_avicola",
                        principalTable: "documentos_nota_entrega",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documentos_nota_entrega_entregas_pedidos_alimentos_EntregaPedidoAlimentoId",
                        column: x => x.EntregaPedidoAlimentoId,
                        principalSchema: "gestion_avicola",
                        principalTable: "entregas_pedidos_alimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documentos_nota_entrega_EntregaPedidoAlimentoId",
                schema: "gestion_avicola",
                table: "documentos_nota_entrega",
                column: "EntregaPedidoAlimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_nota_entrega_ReemplazadoPorId",
                schema: "gestion_avicola",
                table: "documentos_nota_entrega",
                column: "ReemplazadoPorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documentos_nota_entrega",
                schema: "gestion_avicola");
        }
    }
}
