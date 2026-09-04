using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.GestionAvicola.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NotificacionesInternas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notificaciones_internas",
                schema: "gestion_avicola",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    PedidoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Meta = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Leida = table.Column<bool>(type: "bit", nullable: false),
                    LeidaPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FechaLeidaUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificaciones_internas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_internas_ClienteId_FechaUtc",
                schema: "gestion_avicola",
                table: "notificaciones_internas",
                columns: new[] { "ClienteId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_internas_PedidoId",
                schema: "gestion_avicola",
                table: "notificaciones_internas",
                column: "PedidoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notificaciones_internas",
                schema: "gestion_avicola");
        }
    }
}
