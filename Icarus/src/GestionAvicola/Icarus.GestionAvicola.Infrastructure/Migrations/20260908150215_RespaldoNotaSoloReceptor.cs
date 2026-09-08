using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.GestionAvicola.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RespaldoNotaSoloReceptor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documentos_nota_entrega_documentos_nota_entrega_ReemplazadoPorId",
                schema: "gestion_avicola",
                table: "documentos_nota_entrega");

            migrationBuilder.DropIndex(
                name: "IX_documentos_nota_entrega_ReemplazadoPorId",
                schema: "gestion_avicola",
                table: "documentos_nota_entrega");

            migrationBuilder.DropColumn(
                name: "Activo",
                schema: "gestion_avicola",
                table: "documentos_nota_entrega");

            migrationBuilder.DropColumn(
                name: "FechaDesactivacionUtc",
                schema: "gestion_avicola",
                table: "documentos_nota_entrega");

            migrationBuilder.DropColumn(
                name: "ReemplazadoPorId",
                schema: "gestion_avicola",
                table: "documentos_nota_entrega");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                schema: "gestion_avicola",
                table: "documentos_nota_entrega",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDesactivacionUtc",
                schema: "gestion_avicola",
                table: "documentos_nota_entrega",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReemplazadoPorId",
                schema: "gestion_avicola",
                table: "documentos_nota_entrega",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_documentos_nota_entrega_ReemplazadoPorId",
                schema: "gestion_avicola",
                table: "documentos_nota_entrega",
                column: "ReemplazadoPorId");

            migrationBuilder.AddForeignKey(
                name: "FK_documentos_nota_entrega_documentos_nota_entrega_ReemplazadoPorId",
                schema: "gestion_avicola",
                table: "documentos_nota_entrega",
                column: "ReemplazadoPorId",
                principalSchema: "gestion_avicola",
                principalTable: "documentos_nota_entrega",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
