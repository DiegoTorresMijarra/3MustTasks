using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeMustTasks.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanReminderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlanReminderDaysMask",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 127);

            migrationBuilder.AddColumn<bool>(
                name: "PlanReminderEnabled",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlanReminderDaysMask",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PlanReminderEnabled",
                table: "Settings");
        }
    }
}
