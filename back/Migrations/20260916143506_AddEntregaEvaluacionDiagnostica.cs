using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back.Migrations
{
    /// <inheritdoc />
    public partial class AddEntregaEvaluacionDiagnostica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La calificación puede ser NULL mientras el docente
            // todavía no haya revisado la entrega.
            migrationBuilder.AlterColumn<double>(
                name: "Calificacion",
                table: "ResultadosEvaluacionesDiagnosticas",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision");

            // Archivo enviado por el estudiante.
            migrationBuilder.AddColumn<string>(
                name: "ArchivoEntregaUrl",
                table: "ResultadosEvaluacionesDiagnosticas",
                type: "text",
                nullable: true);

            // Pendiente | Entregado | Calificado
            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "ResultadosEvaluacionesDiagnosticas",
                type: "text",
                nullable: false,
                defaultValue: "Pendiente");

            // Fecha en la que el estudiante realizó la entrega.
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEntrega",
                table: "ResultadosEvaluacionesDiagnosticas",
                type: "timestamp with time zone",
                nullable: true);

            // Para evaluaciones de tipo Cuestionario.
            migrationBuilder.AddColumn<string>(
                name: "RespuestasCuestionario",
                table: "ResultadosEvaluacionesDiagnosticas",
                type: "text",
                nullable: true);

            // Los resultados que ya existían antes de esta migración
            // ya tenían una calificación, por lo tanto se consideran
            // calificados.
            migrationBuilder.Sql(@"
                UPDATE ""ResultadosEvaluacionesDiagnosticas""
                SET ""Estado"" = 'Calificado'
                WHERE ""Calificacion"" IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivoEntregaUrl",
                table: "ResultadosEvaluacionesDiagnosticas");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "ResultadosEvaluacionesDiagnosticas");

            migrationBuilder.DropColumn(
                name: "FechaEntrega",
                table: "ResultadosEvaluacionesDiagnosticas");

            migrationBuilder.DropColumn(
                name: "RespuestasCuestionario",
                table: "ResultadosEvaluacionesDiagnosticas");

            // Si alguna entrega todavía no tenía nota,
            // se coloca 0 antes de volver a NOT NULL.
            migrationBuilder.Sql(@"
                UPDATE ""ResultadosEvaluacionesDiagnosticas""
                SET ""Calificacion"" = 0
                WHERE ""Calificacion"" IS NULL;
            ");

            migrationBuilder.AlterColumn<double>(
                name: "Calificacion",
                table: "ResultadosEvaluacionesDiagnosticas",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);
        }
    }
}