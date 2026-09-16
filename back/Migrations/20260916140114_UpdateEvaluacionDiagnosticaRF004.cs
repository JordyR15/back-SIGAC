using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEvaluacionDiagnosticaRF004 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchivoDocenteUrl",
                table: "Evaluaciones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaFin",
                table: "Evaluaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaInicio",
                table: "Evaluaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Instrucciones",
                table: "Evaluaciones",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TipoEvaluacion",
                table: "Evaluaciones",
                type: "text",
                nullable: false,
                defaultValue: "Archivo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivoDocenteUrl",
                table: "Evaluaciones");

            migrationBuilder.DropColumn(
                name: "FechaFin",
                table: "Evaluaciones");

            migrationBuilder.DropColumn(
                name: "FechaInicio",
                table: "Evaluaciones");

            migrationBuilder.DropColumn(
                name: "Instrucciones",
                table: "Evaluaciones");

            migrationBuilder.DropColumn(
                name: "TipoEvaluacion",
                table: "Evaluaciones");
        }
    }
}