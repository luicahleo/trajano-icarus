using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.GestionAvicola.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TrazabilidadGranjaAutorYFolio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF no genera las SEQUENCE solo: se crean antes de las columnas
            // que las usan como valor por defecto (spec 2026-09-14).
            migrationBuilder.CreateSequence<int>(
                name: "secuencia_pedidos_alimento", schema: "gestion_avicola", startValue: 1);
            migrationBuilder.CreateSequence<int>(
                name: "secuencia_despachos_huevo", schema: "gestion_avicola", startValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "CreadoPorTrabajadorId",
                schema: "gestion_avicola",
                table: "pedidos_alimentos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GranjaId",
                schema: "gestion_avicola",
                table: "pedidos_alimentos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<int>(
                name: "Numero",
                schema: "gestion_avicola",
                table: "pedidos_alimentos",
                type: "int",
                nullable: false,
                defaultValueSql: "NEXT VALUE FOR gestion_avicola.secuencia_pedidos_alimento");

            migrationBuilder.AddColumn<Guid>(
                name: "CreadoPorTrabajadorId",
                schema: "gestion_avicola",
                table: "despachos_huevo",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Numero",
                schema: "gestion_avicola",
                table: "despachos_huevo",
                type: "int",
                nullable: false,
                defaultValueSql: "NEXT VALUE FOR gestion_avicola.secuencia_despachos_huevo");

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_alimentos_ClienteId_GranjaId",
                schema: "gestion_avicola",
                table: "pedidos_alimentos",
                columns: new[] { "ClienteId", "GranjaId" });

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_alimentos_CreadoPorTrabajadorId",
                schema: "gestion_avicola",
                table: "pedidos_alimentos",
                column: "CreadoPorTrabajadorId");

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_alimentos_Numero",
                schema: "gestion_avicola",
                table: "pedidos_alimentos",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_despachos_huevo_ClienteId_GranjaId",
                schema: "gestion_avicola",
                table: "despachos_huevo",
                columns: new[] { "ClienteId", "GranjaId" });

            migrationBuilder.CreateIndex(
                name: "IX_despachos_huevo_CreadoPorTrabajadorId",
                schema: "gestion_avicola",
                table: "despachos_huevo",
                column: "CreadoPorTrabajadorId");

            migrationBuilder.CreateIndex(
                name: "IX_despachos_huevo_Numero",
                schema: "gestion_avicola",
                table: "despachos_huevo",
                column: "Numero",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pedidos_alimentos_ClienteId_GranjaId",
                schema: "gestion_avicola",
                table: "pedidos_alimentos");

            migrationBuilder.DropIndex(
                name: "IX_pedidos_alimentos_CreadoPorTrabajadorId",
                schema: "gestion_avicola",
                table: "pedidos_alimentos");

            migrationBuilder.DropIndex(
                name: "IX_pedidos_alimentos_Numero",
                schema: "gestion_avicola",
                table: "pedidos_alimentos");

            migrationBuilder.DropIndex(
                name: "IX_despachos_huevo_ClienteId_GranjaId",
                schema: "gestion_avicola",
                table: "despachos_huevo");

            migrationBuilder.DropIndex(
                name: "IX_despachos_huevo_CreadoPorTrabajadorId",
                schema: "gestion_avicola",
                table: "despachos_huevo");

            migrationBuilder.DropIndex(
                name: "IX_despachos_huevo_Numero",
                schema: "gestion_avicola",
                table: "despachos_huevo");

            migrationBuilder.DropColumn(
                name: "CreadoPorTrabajadorId",
                schema: "gestion_avicola",
                table: "pedidos_alimentos");

            migrationBuilder.DropColumn(
                name: "GranjaId",
                schema: "gestion_avicola",
                table: "pedidos_alimentos");

            migrationBuilder.DropColumn(
                name: "Numero",
                schema: "gestion_avicola",
                table: "pedidos_alimentos");

            migrationBuilder.DropColumn(
                name: "CreadoPorTrabajadorId",
                schema: "gestion_avicola",
                table: "despachos_huevo");

            migrationBuilder.DropColumn(
                name: "Numero",
                schema: "gestion_avicola",
                table: "despachos_huevo");

            migrationBuilder.DropSequence(
                name: "secuencia_pedidos_alimento", schema: "gestion_avicola");
            migrationBuilder.DropSequence(
                name: "secuencia_despachos_huevo", schema: "gestion_avicola");
        }
    }
}
