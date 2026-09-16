using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back.Migrations
{
    /// <inheritdoc />
    public partial class AddPreguntasCuestionarioEvaluacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdaptadaConIA",
                table: "Evaluaciones");

            migrationBuilder.DropColumn(
                name: "DescripcionIA",
                table: "Evaluaciones");

            migrationBuilder.AddColumn<string>(
                name: "PreguntasCuestionario",
                table: "Evaluaciones",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreguntasCuestionario",
                table: "Evaluaciones");

            migrationBuilder.AddColumn<bool>(
                name: "AdaptadaConIA",
                table: "Evaluaciones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DescripcionIA",
                table: "Evaluaciones",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
