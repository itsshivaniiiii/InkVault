using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkVault.Migrations
{
    /// <inheritdoc />
    public partial class AddWriteStreak : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentStreak",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastWriteDate",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LongestStreak",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentStreak",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastWriteDate",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LongestStreak",
                table: "AspNetUsers");
        }
    }
}
