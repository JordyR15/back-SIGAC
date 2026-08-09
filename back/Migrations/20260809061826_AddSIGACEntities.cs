using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace back.Migrations
{
    /// <inheritdoc />
    public partial class AddSIGACEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Catedras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    DocenteId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Catedras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Catedras_Users_DocenteId",
                        column: x => x.DocenteId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ayudantias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CatedraId = table.Column<int>(type: "integer", nullable: false),
                    EstudianteId = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ayudantias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ayudantias_Catedras_CatedraId",
                        column: x => x.CatedraId,
                        principalTable: "Catedras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Ayudantias_Users_EstudianteId",
                        column: x => x.EstudianteId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cronogramas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CatedraId = table.Column<int>(type: "integer", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    FechaPrevista = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaReal = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cronogramas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cronogramas_Catedras_CatedraId",
                        column: x => x.CatedraId,
                        principalTable: "Catedras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Evaluaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    CatedraId = table.Column<int>(type: "integer", nullable: false),
                    EsDiagnostica = table.Column<bool>(type: "boolean", nullable: false),
                    AdaptadaConIA = table.Column<bool>(type: "boolean", nullable: false),
                    DescripcionIA = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evaluaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Evaluaciones_Catedras_CatedraId",
                        column: x => x.CatedraId,
                        principalTable: "Catedras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IndicadoresCualitativos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EstudianteId = table.Column<int>(type: "integer", nullable: false),
                    CatedraId = table.Column<int>(type: "integer", nullable: false),
                    Indicador = table.Column<string>(type: "text", nullable: false),
                    Observacion = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndicadoresCualitativos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndicadoresCualitativos_Catedras_CatedraId",
                        column: x => x.CatedraId,
                        principalTable: "Catedras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IndicadoresCualitativos_Users_EstudianteId",
                        column: x => x.EstudianteId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inscripciones",
                columns: table => new
                {
                    EstudianteId = table.Column<int>(type: "integer", nullable: false),
                    CatedraId = table.Column<int>(type: "integer", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    PromedioActual = table.Column<double>(type: "double precision", nullable: false),
                    AlertaRendimiento = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inscripciones", x => new { x.EstudianteId, x.CatedraId });
                    table.ForeignKey(
                        name: "FK_Inscripciones_Catedras_CatedraId",
                        column: x => x.CatedraId,
                        principalTable: "Catedras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inscripciones_Users_EstudianteId",
                        column: x => x.EstudianteId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActividadesAyudantia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AyudantiaId = table.Column<int>(type: "integer", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    FechaPlanificada = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Completada = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActividadesAyudantia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActividadesAyudantia_Ayudantias_AyudantiaId",
                        column: x => x.AyudantiaId,
                        principalTable: "Ayudantias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bitacoras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AyudantiaId = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActividadesRealizadas = table.Column<string>(type: "text", nullable: false),
                    EvidenciaUrl = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bitacoras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bitacoras_Ayudantias_AyudantiaId",
                        column: x => x.AyudantiaId,
                        principalTable: "Ayudantias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActividadesAyudantia_AyudantiaId",
                table: "ActividadesAyudantia",
                column: "AyudantiaId");

            migrationBuilder.CreateIndex(
                name: "IX_Ayudantias_CatedraId",
                table: "Ayudantias",
                column: "CatedraId");

            migrationBuilder.CreateIndex(
                name: "IX_Ayudantias_EstudianteId",
                table: "Ayudantias",
                column: "EstudianteId");

            migrationBuilder.CreateIndex(
                name: "IX_Bitacoras_AyudantiaId",
                table: "Bitacoras",
                column: "AyudantiaId");

            migrationBuilder.CreateIndex(
                name: "IX_Catedras_DocenteId",
                table: "Catedras",
                column: "DocenteId");

            migrationBuilder.CreateIndex(
                name: "IX_Cronogramas_CatedraId",
                table: "Cronogramas",
                column: "CatedraId");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluaciones_CatedraId",
                table: "Evaluaciones",
                column: "CatedraId");

            migrationBuilder.CreateIndex(
                name: "IX_IndicadoresCualitativos_CatedraId",
                table: "IndicadoresCualitativos",
                column: "CatedraId");

            migrationBuilder.CreateIndex(
                name: "IX_IndicadoresCualitativos_EstudianteId",
                table: "IndicadoresCualitativos",
                column: "EstudianteId");

            migrationBuilder.CreateIndex(
                name: "IX_Inscripciones_CatedraId",
                table: "Inscripciones",
                column: "CatedraId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActividadesAyudantia");

            migrationBuilder.DropTable(
                name: "Bitacoras");

            migrationBuilder.DropTable(
                name: "Cronogramas");

            migrationBuilder.DropTable(
                name: "Evaluaciones");

            migrationBuilder.DropTable(
                name: "IndicadoresCualitativos");

            migrationBuilder.DropTable(
                name: "Inscripciones");

            migrationBuilder.DropTable(
                name: "Ayudantias");

            migrationBuilder.DropTable(
                name: "Catedras");
        }
    }
}
