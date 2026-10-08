using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreserveAiActionsAndBulkDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiActions_ChatSessions_ChatSessionId",
                table: "AiActions");

            migrationBuilder.AlterColumn<int>(
                name: "ChatSessionId",
                table: "AiActions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<bool>(
                name: "DecidedInBulk",
                table: "AiActions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "FK_AiActions_ChatSessions_ChatSessionId",
                table: "AiActions",
                column: "ChatSessionId",
                principalTable: "ChatSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiActions_ChatSessions_ChatSessionId",
                table: "AiActions");

            migrationBuilder.DropColumn(
                name: "DecidedInBulk",
                table: "AiActions");

            migrationBuilder.AlterColumn<int>(
                name: "ChatSessionId",
                table: "AiActions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AiActions_ChatSessions_ChatSessionId",
                table: "AiActions",
                column: "ChatSessionId",
                principalTable: "ChatSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
