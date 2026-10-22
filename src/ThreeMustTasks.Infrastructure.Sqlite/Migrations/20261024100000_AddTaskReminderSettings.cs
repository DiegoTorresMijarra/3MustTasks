using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeMustTasks.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskReminderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultReminderOffsetMin",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "EveningSummaryEnabled",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "EveningSummaryTime",
                table: "Settings",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeOnly(21, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "FutureTaskAlertEnabled",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DefaultReminderOffsetMin", table: "Settings");
            migrationBuilder.DropColumn(name: "EveningSummaryEnabled", table: "Settings");
            migrationBuilder.DropColumn(name: "EveningSummaryTime", table: "Settings");
            migrationBuilder.DropColumn(name: "FutureTaskAlertEnabled", table: "Settings");
        }
    }
}
