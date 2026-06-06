using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitFlow.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddQuitHabitFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "Habits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuitCategory",
                table: "Habits",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TriggerLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HabitId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimeOfDay = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmotionalState = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CravingLevel = table.Column<int>(type: "integer", nullable: false),
                    TriggerType = table.Column<int>(type: "integer", nullable: false),
                    DidRelapse = table.Column<bool>(type: "boolean", nullable: false),
                    Resisted = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriggerLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriggerLogs_Habits_HabitId",
                        column: x => x.HabitId,
                        principalTable: "Habits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TriggerLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TriggerLogs_HabitId_UserId_OccurredAt",
                table: "TriggerLogs",
                columns: new[] { "HabitId", "UserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TriggerLogs_UserId",
                table: "TriggerLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TriggerLogs");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "QuitCategory",
                table: "Habits");
        }
    }
}
