using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookMyServiceBE.Migrations
{
    /// <inheritdoc />
    public partial class add_Chat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_UserId",
                table: "ChatMessages");

            migrationBuilder.AddColumn<int>(
                name: "RecipientUserId",
                table: "ChatMessages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_RecipientUserId",
                table: "ChatMessages",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_UserId_RecipientUserId_CreatedAt",
                table: "ChatMessages",
                columns: new[] { "UserId", "RecipientUserId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Users_RecipientUserId",
                table: "ChatMessages",
                column: "RecipientUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Users_RecipientUserId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_RecipientUserId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_UserId_RecipientUserId_CreatedAt",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                table: "ChatMessages");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_UserId",
                table: "ChatMessages",
                column: "UserId");
        }
    }
}
