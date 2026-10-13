using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvaluationProvider",
                table: "AiAnalysisLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EvaluationRunId",
                table: "AiAnalysisLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvaluationTask",
                table: "AiAnalysisLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InputTokens",
                table: "AiAnalysisLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OutputTokens",
                table: "AiAnalysisLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EvaluationRunId",
                table: "AiActions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EvaluationRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TaskSetVersion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Providers = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Repeats = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisLogs_EvaluationRunId",
                table: "AiAnalysisLogs",
                column: "EvaluationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AiActions_EvaluationRunId",
                table: "AiActions",
                column: "EvaluationRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvaluationRuns");

            migrationBuilder.DropIndex(
                name: "IX_AiAnalysisLogs_EvaluationRunId",
                table: "AiAnalysisLogs");

            migrationBuilder.DropIndex(
                name: "IX_AiActions_EvaluationRunId",
                table: "AiActions");

            migrationBuilder.DropColumn(
                name: "EvaluationProvider",
                table: "AiAnalysisLogs");

            migrationBuilder.DropColumn(
                name: "EvaluationRunId",
                table: "AiAnalysisLogs");

            migrationBuilder.DropColumn(
                name: "EvaluationTask",
                table: "AiAnalysisLogs");

            migrationBuilder.DropColumn(
                name: "InputTokens",
                table: "AiAnalysisLogs");

            migrationBuilder.DropColumn(
                name: "OutputTokens",
                table: "AiAnalysisLogs");

            migrationBuilder.DropColumn(
                name: "EvaluationRunId",
                table: "AiActions");
        }
    }
}
