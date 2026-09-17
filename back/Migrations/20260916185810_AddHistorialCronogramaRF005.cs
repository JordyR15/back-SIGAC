using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace back.Migrations
{
    /// <inheritdoc />
    public partial class AddHistorialCronogramaRF005 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistorialCronograma",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CronogramaActividadId = table.Column<int>(type: "integer", nullable: false),
                    FechaAnterior = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaNueva = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DescripcionAnterior = table.Column<string>(type: "text", nullable: false),
                    DescripcionNueva = table.Column<string>(type: "text", nullable: false),
                    ObservacionCambio = table.Column<string>(type: "text", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialCronograma", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialCronograma_Cronogramas_CronogramaActividadId",
                        column: x => x.CronogramaActividadId,
                        principalTable: "Cronogramas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialCronograma_CronogramaActividadId",
                table: "HistorialCronograma",
                column: "CronogramaActividadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistorialCronograma");
        }
    }
}
