using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeMustTasks.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurrenceIsPaused : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPaused",
                table: "Recurrences",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPaused",
                table: "Recurrences");
        }
    }
}
