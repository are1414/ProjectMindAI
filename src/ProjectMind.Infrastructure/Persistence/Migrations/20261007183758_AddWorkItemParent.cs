using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "WorkItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ParentId",
                table: "WorkItems",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_WorkItems_ParentId",
                table: "WorkItems",
                column: "ParentId",
                principalTable: "WorkItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_WorkItems_ParentId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_ParentId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "WorkItems");
        }
    }
}
