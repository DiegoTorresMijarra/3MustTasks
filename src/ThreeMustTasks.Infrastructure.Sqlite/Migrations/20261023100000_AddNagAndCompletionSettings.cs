using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeMustTasks.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddNagAndCompletionSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CompletionReminderEnabled",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "CompletionReminderTime",
                table: "Settings",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeOnly(18, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "NagEnabled",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "NagIntervalMin",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "NagMaxPerDay",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 4);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CompletionReminderEnabled", table: "Settings");
            migrationBuilder.DropColumn(name: "CompletionReminderTime", table: "Settings");
            migrationBuilder.DropColumn(name: "NagEnabled", table: "Settings");
            migrationBuilder.DropColumn(name: "NagIntervalMin", table: "Settings");
            migrationBuilder.DropColumn(name: "NagMaxPerDay", table: "Settings");
        }
    }
}
