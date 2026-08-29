using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace back.Migrations
{
    /// <inheritdoc />
    public partial class AddPresentaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "MinimoNota",
                table: "Catedras",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Semestre",
                table: "Catedras",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Materia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    DocenteResponsableId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Materia_Users_DocenteResponsableId",
                        column: x => x.DocenteResponsableId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Presentaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AyudantiaId = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProfesoresAsignados = table.Column<string>(type: "text", nullable: false),
                    DecanoId = table.Column<int>(type: "integer", nullable: true),
                    CoordinadorCarreraId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Presentaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Presentaciones_Ayudantias_AyudantiaId",
                        column: x => x.AyudantiaId,
                        principalTable: "Ayudantias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Presentaciones_Users_CoordinadorCarreraId",
                        column: x => x.CoordinadorCarreraId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Presentaciones_Users_DecanoId",
                        column: x => x.DecanoId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Actividades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    FechaEntrega = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    MateriaId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Actividades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Actividades_Materia_MateriaId",
                        column: x => x.MateriaId,
                        principalTable: "Materia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Clases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    MateriaId = table.Column<int>(type: "integer", nullable: false),
                    DocenteId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clases_Materia_MateriaId",
                        column: x => x.MateriaId,
                        principalTable: "Materia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Clases_Users_DocenteId",
                        column: x => x.DocenteId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recursos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    EsEsencial = table.Column<bool>(type: "boolean", nullable: false),
                    MateriaId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recursos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recursos_Materia_MateriaId",
                        column: x => x.MateriaId,
                        principalTable: "Materia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PresentacionEvaluaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PresentacionId = table.Column<int>(type: "integer", nullable: false),
                    JuradoId = table.Column<int>(type: "integer", nullable: false),
                    Nota = table.Column<double>(type: "double precision", nullable: false),
                    Observaciones = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresentacionEvaluaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PresentacionEvaluaciones_Presentaciones_PresentacionId",
                        column: x => x.PresentacionId,
                        principalTable: "Presentaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PresentacionEvaluaciones_Users_JuradoId",
                        column: x => x.JuradoId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClaseEstudiante",
                columns: table => new
                {
                    ClasesEstudianteId = table.Column<int>(type: "integer", nullable: false),
                    EstudiantesId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaseEstudiante", x => new { x.ClasesEstudianteId, x.EstudiantesId });
                    table.ForeignKey(
                        name: "FK_ClaseEstudiante_Clases_ClasesEstudianteId",
                        column: x => x.ClasesEstudianteId,
                        principalTable: "Clases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClaseEstudiante_Users_EstudiantesId",
                        column: x => x.EstudiantesId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClasesSesiones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MateriaId = table.Column<int>(type: "integer", nullable: false),
                    ClaseId = table.Column<int>(type: "integer", nullable: true),
                    DocenteId = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HoraInicio = table.Column<TimeSpan>(type: "interval", nullable: false),
                    HoraFin = table.Column<TimeSpan>(type: "interval", nullable: false),
                    TipoClase = table.Column<string>(type: "text", nullable: false),
                    LinkVirtual = table.Column<string>(type: "text", nullable: false),
                    AplicacionVirtual = table.Column<string>(type: "text", nullable: false),
                    EdificioPresencial = table.Column<string>(type: "text", nullable: false),
                    AulaPresencial = table.Column<string>(type: "text", nullable: false),
                    PisoPresencial = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClasesSesiones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClasesSesiones_Clases_ClaseId",
                        column: x => x.ClaseId,
                        principalTable: "Clases",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClasesSesiones_Materia_MateriaId",
                        column: x => x.MateriaId,
                        principalTable: "Materia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClasesSesiones_Users_DocenteId",
                        column: x => x.DocenteId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecursosVistosPorEstudiante",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecursoId = table.Column<int>(type: "integer", nullable: false),
                    EstudianteId = table.Column<int>(type: "integer", nullable: false),
                    FechaVisto = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecursosVistosPorEstudiante", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecursosVistosPorEstudiante_Recursos_RecursoId",
                        column: x => x.RecursoId,
                        principalTable: "Recursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecursosVistosPorEstudiante_Users_EstudianteId",
                        column: x => x.EstudianteId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Asistencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClaseSesionId = table.Column<int>(type: "integer", nullable: false),
                    EstudianteId = table.Column<int>(type: "integer", nullable: false),
                    Presente = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asistencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Asistencias_ClasesSesiones_ClaseSesionId",
                        column: x => x.ClaseSesionId,
                        principalTable: "ClasesSesiones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Asistencias_Users_EstudianteId",
                        column: x => x.EstudianteId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Actividades_MateriaId",
                table: "Actividades",
                column: "MateriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_ClaseSesionId",
                table: "Asistencias",
                column: "ClaseSesionId");

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_EstudianteId",
                table: "Asistencias",
                column: "EstudianteId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaseEstudiante_EstudiantesId",
                table: "ClaseEstudiante",
                column: "EstudiantesId");

            migrationBuilder.CreateIndex(
                name: "IX_Clases_DocenteId",
                table: "Clases",
                column: "DocenteId");

            migrationBuilder.CreateIndex(
                name: "IX_Clases_MateriaId",
                table: "Clases",
                column: "MateriaId");

            migrationBuilder.CreateIndex(
                name: "IX_ClasesSesiones_ClaseId",
                table: "ClasesSesiones",
                column: "ClaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ClasesSesiones_DocenteId",
                table: "ClasesSesiones",
                column: "DocenteId");

            migrationBuilder.CreateIndex(
                name: "IX_ClasesSesiones_MateriaId",
                table: "ClasesSesiones",
                column: "MateriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Materia_DocenteResponsableId",
                table: "Materia",
                column: "DocenteResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_Presentaciones_AyudantiaId",
                table: "Presentaciones",
                column: "AyudantiaId");

            migrationBuilder.CreateIndex(
                name: "IX_Presentaciones_CoordinadorCarreraId",
                table: "Presentaciones",
                column: "CoordinadorCarreraId");

            migrationBuilder.CreateIndex(
                name: "IX_Presentaciones_DecanoId",
                table: "Presentaciones",
                column: "DecanoId");

            migrationBuilder.CreateIndex(
                name: "IX_PresentacionEvaluaciones_JuradoId",
                table: "PresentacionEvaluaciones",
                column: "JuradoId");

            migrationBuilder.CreateIndex(
                name: "IX_PresentacionEvaluaciones_PresentacionId",
                table: "PresentacionEvaluaciones",
                column: "PresentacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Recursos_MateriaId",
                table: "Recursos",
                column: "MateriaId");

            migrationBuilder.CreateIndex(
                name: "IX_RecursosVistosPorEstudiante_EstudianteId",
                table: "RecursosVistosPorEstudiante",
                column: "EstudianteId");

            migrationBuilder.CreateIndex(
                name: "IX_RecursosVistosPorEstudiante_RecursoId",
                table: "RecursosVistosPorEstudiante",
                column: "RecursoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Actividades");

            migrationBuilder.DropTable(
                name: "Asistencias");

            migrationBuilder.DropTable(
                name: "ClaseEstudiante");

            migrationBuilder.DropTable(
                name: "PresentacionEvaluaciones");

            migrationBuilder.DropTable(
                name: "RecursosVistosPorEstudiante");

            migrationBuilder.DropTable(
                name: "ClasesSesiones");

            migrationBuilder.DropTable(
                name: "Presentaciones");

            migrationBuilder.DropTable(
                name: "Recursos");

            migrationBuilder.DropTable(
                name: "Clases");

            migrationBuilder.DropTable(
                name: "Materia");

            migrationBuilder.DropColumn(
                name: "MinimoNota",
                table: "Catedras");

            migrationBuilder.DropColumn(
                name: "Semestre",
                table: "Catedras");
        }
    }
}
