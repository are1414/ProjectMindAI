using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectMind.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bir işe birden çok kişi atama (D32): WorkItems.AssigneeId yerine WorkItemAssignments ara tablosu.
    /// Mevcut tek kişilik atamalar yeni tabloya kopyalanır, sonra kolon silinir (veri kaybı yok).
    /// </summary>
    public partial class AddWorkItemAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkItemAssignments",
                columns: table => new
                {
                    WorkItemId = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemAssignments", x => new { x.WorkItemId, x.PersonId });
                    table.ForeignKey(
                        name: "FK_WorkItemAssignments_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkItemAssignments_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemAssignments_PersonId",
                table: "WorkItemAssignments",
                column: "PersonId");

            // Mevcut atamalar korunur.
            migrationBuilder.Sql(
                "INSERT INTO WorkItemAssignments (WorkItemId, PersonId) " +
                "SELECT Id, AssigneeId FROM WorkItems WHERE AssigneeId IS NOT NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_People_AssigneeId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_AssigneeId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "AssigneeId",
                table: "WorkItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssigneeId",
                table: "WorkItems",
                type: "int",
                nullable: true);

            // Tek kişilik şemaya dönüşte her işin en küçük id'li atananı kalır.
            migrationBuilder.Sql(
                "UPDATE w SET AssigneeId = a.PersonId FROM WorkItems w " +
                "JOIN (SELECT WorkItemId, MIN(PersonId) AS PersonId FROM WorkItemAssignments GROUP BY WorkItemId) a " +
                "ON a.WorkItemId = w.Id;");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_AssigneeId",
                table: "WorkItems",
                column: "AssigneeId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_People_AssigneeId",
                table: "WorkItems",
                column: "AssigneeId",
                principalTable: "People",
                principalColumn: "Id");

            migrationBuilder.DropTable(
                name: "WorkItemAssignments");
        }
    }
}
