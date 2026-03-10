using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkVault.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalLikeCommentEmailPrefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailOnJournalCommented",
                table: "NotificationPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EmailOnJournalLiked",
                table: "NotificationPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailOnJournalCommented",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "EmailOnJournalLiked",
                table: "NotificationPreferences");
        }
    }
}
