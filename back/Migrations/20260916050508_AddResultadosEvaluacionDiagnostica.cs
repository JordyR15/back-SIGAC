using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace back.Migrations
{
    /// <inheritdoc />
    public partial class AddResultadosEvaluacionDiagnostica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
             migrationBuilder.CreateTable(
                    name: "ResultadosEvaluacionesDiagnosticas",
                    columns: table => new
                    {
                        Id = table.Column<int>(
                                type: "integer",
                                nullable: false)
                            .Annotation(
                                "Npgsql:ValueGenerationStrategy",
                                Npgsql.EntityFrameworkCore.PostgreSQL.Metadata
                                    .NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),

                        EvaluacionId = table.Column<int>(
                            type: "integer",
                            nullable: false),

                        EstudianteId = table.Column<int>(
                            type: "integer",
                            nullable: false),

                        Calificacion = table.Column<double>(
                            type: "double precision",
                            nullable: false),

                        Observacion = table.Column<string>(
                            type: "text",
                            nullable: false),

                        FechaRegistro = table.Column<DateTime>(
                            type: "timestamp with time zone",
                            nullable: false)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey(
                            "PK_ResultadosEvaluacionesDiagnosticas",
                            x => x.Id);

                        table.ForeignKey(
                            name: "FK_ResultadosEvaluacionesDiagnosticas_Evaluaciones_EvaluacionId",
                            column: x => x.EvaluacionId,
                            principalTable: "Evaluaciones",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Cascade);

                        table.ForeignKey(
                            name: "FK_ResultadosEvaluacionesDiagnosticas_Users_EstudianteId",
                            column: x => x.EstudianteId,
                            principalTable: "Users",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Cascade);
                    });

                migrationBuilder.CreateIndex(
                    name: "IX_ResultadosEvaluacionesDiagnosticas_EstudianteId",
                    table: "ResultadosEvaluacionesDiagnosticas",
                    column: "EstudianteId");

                migrationBuilder.CreateIndex(
                    name: "IX_ResultadosEvaluacionesDiagnosticas_EvaluacionId_EstudianteId",
                    table: "ResultadosEvaluacionesDiagnosticas",
                    columns: new[]
                    {
                        "EvaluacionId",
                        "EstudianteId"
                    },
                    unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                   name: "ResultadosEvaluacionesDiagnosticas");
        }
    }
}
