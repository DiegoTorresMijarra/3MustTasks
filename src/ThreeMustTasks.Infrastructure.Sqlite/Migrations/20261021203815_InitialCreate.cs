using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeMustTasks.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DayAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LogicalDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DayAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DayLogs",
                columns: table => new
                {
                    LogicalDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PlanMinimumMetAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CompletionMinimumMetAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Fulfilled = table.Column<bool>(type: "INTEGER", nullable: true),
                    ClosedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DayLogs", x => x.LogicalDate);
                });

            migrationBuilder.CreateTable(
                name: "Favorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    LevelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DueTime = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    ReminderOffset = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    UseCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Favorites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Levels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: false),
                    Icon = table.Column<string>(type: "TEXT", nullable: false),
                    MinToAdd = table.Column<int>(type: "INTEGER", nullable: false),
                    MinToComplete = table.Column<int>(type: "INTEGER", nullable: false),
                    Max = table.Column<int>(type: "INTEGER", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Levels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Recurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TemplateTitle = table.Column<string>(type: "TEXT", nullable: false),
                    LevelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DueTime = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    Frequency = table.Column<string>(type: "TEXT", nullable: false),
                    Interval = table.Column<int>(type: "INTEGER", nullable: false),
                    DaysOfWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    DayOfMonth = table.Column<int>(type: "INTEGER", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    LastGeneratedDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recurrences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    DayStartsAt = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    StrictMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoAddScheduled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", nullable: false),
                    FirstDayOfWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    LevelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DueTime = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    ReminderOffset = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    ScheduledDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    IsPinned = table.Column<bool>(type: "INTEGER", nullable: false),
                    TimesPostponed = table.Column<int>(type: "INTEGER", nullable: false),
                    RecurrenceRuleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IcsUid = table.Column<string>(type: "TEXT", nullable: true),
                    IsFavoriteSource = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DayAssignments_LogicalDate",
                table: "DayAssignments",
                column: "LogicalDate");

            migrationBuilder.CreateIndex(
                name: "IX_DayAssignments_TaskId",
                table: "DayAssignments",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Levels_Order",
                table: "Levels",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_CreatedAt",
                table: "Tasks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ScheduledDate",
                table: "Tasks",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Status",
                table: "Tasks",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "DayAssignments");

            migrationBuilder.DropTable(
                name: "DayLogs");

            migrationBuilder.DropTable(
                name: "Favorites");

            migrationBuilder.DropTable(
                name: "Levels");

            migrationBuilder.DropTable(
                name: "Recurrences");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "Tasks");
        }
    }
}
