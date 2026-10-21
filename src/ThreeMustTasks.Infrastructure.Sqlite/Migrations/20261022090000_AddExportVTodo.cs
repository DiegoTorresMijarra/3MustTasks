using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeMustTasks.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddExportVTodo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExportVTodo",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExportVTodo",
                table: "Settings");
        }
    }
}
