using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Icarus.ControlAcceso.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InicialControlAcceso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "control_acceso");

            migrationBuilder.CreateTable(
                name: "configuracion_acceso_trabajador",
                schema: "control_acceso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabajadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Habilitado = table.Column<bool>(type: "bit", nullable: false),
                    Enrolamiento = table.Column<int>(type: "int", nullable: false),
                    VersionEnrolamiento = table.Column<int>(type: "int", nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracion_acceso_trabajador", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "jornadas_acceso",
                schema: "control_acceso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabajadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaBoliviana = table.Column<DateOnly>(type: "date", nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jornadas_acceso", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "plantillas_faciales",
                schema: "control_acceso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabajadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContenidoCifrado = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Nonce = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Tag = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    VersionClave = table.Column<int>(type: "int", nullable: false),
                    ModeloFormato = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VersionEnrolamiento = table.Column<int>(type: "int", nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacionUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FechaRevocacionUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plantillas_faciales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marcaciones",
                schema: "control_acceso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JornadaAccesoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    InstanteUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClaveIdempotencia = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origen = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marcaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_marcaciones_jornadas_acceso_JornadaAccesoId",
                        column: x => x.JornadaAccesoId,
                        principalSchema: "control_acceso",
                        principalTable: "jornadas_acceso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "revisiones_jornada",
                schema: "control_acceso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JornadaAccesoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstanteCorreccionUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HastaSecuenciaOriginal = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revisiones_jornada", x => x.Id);
                    table.ForeignKey(
                        name: "FK_revisiones_jornada_jornadas_acceso_JornadaAccesoId",
                        column: x => x.JornadaAccesoId,
                        principalSchema: "control_acceso",
                        principalTable: "jornadas_acceso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "revisiones_jornada_valores",
                schema: "control_acceso",
                columns: table => new
                {
                    RevisionJornadaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    InstanteUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revisiones_jornada_valores", x => new { x.RevisionJornadaId, x.Id });
                    table.ForeignKey(
                        name: "FK_revisiones_jornada_valores_revisiones_jornada_RevisionJornadaId",
                        column: x => x.RevisionJornadaId,
                        principalSchema: "control_acceso",
                        principalTable: "revisiones_jornada",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_configuracion_acceso_trabajador_ClienteId_TrabajadorId",
                schema: "control_acceso",
                table: "configuracion_acceso_trabajador",
                columns: new[] { "ClienteId", "TrabajadorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_jornadas_acceso_ClienteId_TrabajadorId_FechaBoliviana",
                schema: "control_acceso",
                table: "jornadas_acceso",
                columns: new[] { "ClienteId", "TrabajadorId", "FechaBoliviana" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_marcaciones_JornadaAccesoId_ClaveIdempotencia",
                schema: "control_acceso",
                table: "marcaciones",
                columns: new[] { "JornadaAccesoId", "ClaveIdempotencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plantillas_faciales_ClienteId_TrabajadorId_EstaActivo",
                schema: "control_acceso",
                table: "plantillas_faciales",
                columns: new[] { "ClienteId", "TrabajadorId", "EstaActivo" });

            migrationBuilder.CreateIndex(
                name: "IX_plantillas_faciales_ClienteId_TrabajadorId_VersionEnrolamiento",
                schema: "control_acceso",
                table: "plantillas_faciales",
                columns: new[] { "ClienteId", "TrabajadorId", "VersionEnrolamiento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_revisiones_jornada_JornadaAccesoId",
                schema: "control_acceso",
                table: "revisiones_jornada",
                column: "JornadaAccesoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracion_acceso_trabajador",
                schema: "control_acceso");

            migrationBuilder.DropTable(
                name: "marcaciones",
                schema: "control_acceso");

            migrationBuilder.DropTable(
                name: "plantillas_faciales",
                schema: "control_acceso");

            migrationBuilder.DropTable(
                name: "revisiones_jornada_valores",
                schema: "control_acceso");

            migrationBuilder.DropTable(
                name: "revisiones_jornada",
                schema: "control_acceso");

            migrationBuilder.DropTable(
                name: "jornadas_acceso",
                schema: "control_acceso");
        }
    }
}
