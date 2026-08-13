using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.Clientes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InicialClientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "clientes");

            migrationBuilder.CreateTable(
                name: "clientes",
                schema: "clientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RazonSocial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IdentificadorFiscal = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false),
                    ModulosHabilitados = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trabajadores",
                schema: "clientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DocumentoIdentidad = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Cargo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FechaIngreso = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaCese = table.Column<DateOnly>(type: "date", nullable: true),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trabajadores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_clientes_IdentificadorFiscal",
                schema: "clientes",
                table: "clientes",
                column: "IdentificadorFiscal",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trabajadores_ClienteId_DocumentoIdentidad",
                schema: "clientes",
                table: "trabajadores",
                columns: new[] { "ClienteId", "DocumentoIdentidad" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clientes",
                schema: "clientes");

            migrationBuilder.DropTable(
                name: "trabajadores",
                schema: "clientes");
        }
    }
}
