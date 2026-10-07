using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusHistoryAndSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    BaselineId = table.Column<int>(type: "int", nullable: true),
                    PlannedValue = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    EarnedValue = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ActualCost = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    BudgetAtCompletion = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Spi = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: true),
                    Cpi = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: true),
                    SpiTime = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: true),
                    EstimateAtCompletion = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    ForecastFinish = table.Column<DateOnly>(type: "date", nullable: true),
                    PercentComplete = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: false),
                    HealthScore = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: true),
                    ScopeHours = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    BlockedItems = table.Column<int>(type: "int", nullable: false),
                    TeamSize = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSnapshots_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StatusUpdates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkItemId = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PercentComplete = table.Column<int>(type: "int", nullable: false),
                    ActualHours = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatusUpdates_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSnapshots_ProjectId_Date",
                table: "ProjectSnapshots",
                columns: new[] { "ProjectId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatusUpdates_ProjectId_Date",
                table: "StatusUpdates",
                columns: new[] { "ProjectId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_StatusUpdates_WorkItemId",
                table: "StatusUpdates",
                column: "WorkItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectSnapshots");

            migrationBuilder.DropTable(
                name: "StatusUpdates");
        }
    }
}
