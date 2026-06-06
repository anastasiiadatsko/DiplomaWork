using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitFlow.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddHabitGoogleCalendarReminderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleCalendarEventId",
                table: "Habits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGoogleCalendarReminderEnabled",
                table: "Habits",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ReminderTime",
                table: "Habits",
                type: "time without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleCalendarEventId",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "IsGoogleCalendarReminderEnabled",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "ReminderTime",
                table: "Habits");
        }
    }
}
