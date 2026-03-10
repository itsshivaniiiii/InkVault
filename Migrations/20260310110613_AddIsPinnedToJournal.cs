using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkVault.Migrations
{
    /// <inheritdoc />
    public partial class AddIsPinnedToJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "Journals",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "Journals");
        }
    }
}
