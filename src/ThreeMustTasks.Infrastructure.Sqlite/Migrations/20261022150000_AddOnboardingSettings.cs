using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeMustTasks.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OnboardingCompleted",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "PlanningReminderTime",
                table: "Settings",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeOnly(7, 30, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OnboardingCompleted",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PlanningReminderTime",
                table: "Settings");
        }
    }
}
