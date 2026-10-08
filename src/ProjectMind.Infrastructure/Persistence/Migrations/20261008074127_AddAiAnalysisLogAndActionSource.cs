using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAnalysisLogAndActionSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "AiActions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unknown");   // mevcut öneriler: kaynak bilinmiyor

            migrationBuilder.CreateTable(
                name: "AiAnalysisLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    ChatSessionId = table.Column<int>(type: "int", nullable: true),
                    ChatMessageId = table.Column<int>(type: "int", nullable: true),
                    PromptVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ToolsCalled = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ContextLength = table.Column<int>(type: "int", nullable: false),
                    UnverifiedNumberCount = table.Column<int>(type: "int", nullable: false),
                    UnverifiedNumbers = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SchemaValid = table.Column<bool>(type: "bit", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    InputHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAnalysisLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisLogs_ProjectId_Kind_InputHash",
                table: "AiAnalysisLogs",
                columns: new[] { "ProjectId", "Kind", "InputHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAnalysisLogs");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "AiActions");
        }
    }
}
