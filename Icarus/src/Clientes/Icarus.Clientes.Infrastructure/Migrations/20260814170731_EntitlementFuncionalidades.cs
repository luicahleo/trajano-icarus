using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.Clientes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EntitlementFuncionalidades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Funcionalidades",
                schema: "clientes",
                table: "trabajadores",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Funcionalidades",
                schema: "clientes",
                table: "trabajadores");
        }
    }
}
