using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitFlow.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleCalendarColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleCalendarAccessToken",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleCalendarRefreshToken",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GoogleCalendarTokenExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGoogleCalendarConnected",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleCalendarAccessToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GoogleCalendarRefreshToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GoogleCalendarTokenExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsGoogleCalendarConnected",
                table: "Users");
        }
    }
}
