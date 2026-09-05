using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UsuariosCaisy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FuncionalidadesCaisy",
                schema: "identity",
                table: "usuarios",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FuncionalidadesCaisy",
                schema: "identity",
                table: "usuarios");
        }
    }
}
