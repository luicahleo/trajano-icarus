using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.GestionAvicola.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EstadoProgramaVacunacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Estado",
                schema: "gestion_avicola",
                table: "programas_vacunacion",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Datos existentes (decisión 9 del spec 2026-10-30): son datos de
            // prueba. Los programas con cronograma activo pasan a Publicado y
            // siguen asignables; los que no tienen ítems quedan en Borrador
            // (los inactivos equivalen a borradores eliminados). Así cada
            // publicado tiene cronograma y las tareas ya materializadas
            // siguen apuntando a programas publicados.
            migrationBuilder.Sql("""
                UPDATE p SET p.Estado = 1
                FROM gestion_avicola.programas_vacunacion AS p
                WHERE EXISTS (
                    SELECT 1 FROM gestion_avicola.programas_vacunacion_items AS i
                    WHERE i.ProgramaVacunacionId = p.Id AND i.EstaActivo = 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Estado",
                schema: "gestion_avicola",
                table: "programas_vacunacion");
        }
    }
}
