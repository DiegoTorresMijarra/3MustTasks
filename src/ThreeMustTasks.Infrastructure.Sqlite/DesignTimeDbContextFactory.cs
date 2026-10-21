using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ThreeMustTasks.Infrastructure.Sqlite;

/// <summary>Solo para <c>dotnet ef migrations</c>.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ThreeMustTasksDbContext>
{
    public ThreeMustTasksDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ThreeMustTasksDbContext>().UseSqlite("Data Source=design.db").Options);
}
