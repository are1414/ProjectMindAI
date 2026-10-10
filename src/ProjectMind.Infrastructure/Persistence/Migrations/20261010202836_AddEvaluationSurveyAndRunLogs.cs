using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationSurveyAndRunLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClarityRating",
                table: "AiAnalysisLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Helpful",
                table: "AiAnalysisLogs",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RatedAt",
                table: "AiAnalysisLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SurveyResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParticipantCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    QuestionnaireVersion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ConsentGiven = table.Column<bool>(type: "bit", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SusScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyResponses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatIfRunLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScenarioCount = table.Column<int>(type: "int", nullable: false),
                    Iterations = table.Column<int>(type: "int", nullable: false),
                    Seed = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    ResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatIfRunLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SurveyAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SurveyResponseId = table.Column<int>(type: "int", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Value = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyAnswers_SurveyResponses_SurveyResponseId",
                        column: x => x.SurveyResponseId,
                        principalTable: "SurveyResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyAnswers_SurveyResponseId_ItemCode",
                table: "SurveyAnswers",
                columns: new[] { "SurveyResponseId", "ItemCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurveyResponses_ParticipantCode",
                table: "SurveyResponses",
                column: "ParticipantCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatIfRunLogs_ProjectId",
                table: "WhatIfRunLogs",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SurveyAnswers");

            migrationBuilder.DropTable(
                name: "WhatIfRunLogs");

            migrationBuilder.DropTable(
                name: "SurveyResponses");

            migrationBuilder.DropColumn(
                name: "ClarityRating",
                table: "AiAnalysisLogs");

            migrationBuilder.DropColumn(
                name: "Helpful",
                table: "AiAnalysisLogs");

            migrationBuilder.DropColumn(
                name: "RatedAt",
                table: "AiAnalysisLogs");
        }
    }
}
