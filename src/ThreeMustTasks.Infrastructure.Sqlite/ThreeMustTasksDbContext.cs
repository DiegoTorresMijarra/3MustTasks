using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ThreeMustTasks.Core.Domain;

namespace ThreeMustTasks.Infrastructure.Sqlite;

/// <summary>
/// Contexto de EF Core con el modelo de datos de la spec §3. Las entidades de Core se mapean directamente
/// (son records inmutables); no hay claves foráneas porque los puertos no exigen integridad referencial
/// entre agregados (la suite de contrato usa ids sin fila asociada).
/// </summary>
public sealed class ThreeMustTasksDbContext(DbContextOptions<ThreeMustTasksDbContext> options) : DbContext(options)
{
    public const string SettingsIdProperty = "Id";
    public const int SettingsRowId = 1;

    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<DayAssignment> DayAssignments => Set<DayAssignment>();
    public DbSet<DayLog> DayLogs => Set<DayLog>();
    public DbSet<Level> Levels => Set<Level>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<FavoriteTemplate> Favorites => Set<FavoriteTemplate>();
    public DbSet<RecurrenceRule> Recurrences => Set<RecurrenceRule>();
    public DbSet<AppSettings> Settings => Set<AppSettings>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite no puede ordenar DateTimeOffset en TEXT: se guarda como ticks UTC (long).
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
        configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<UtcTicksConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Level>(e =>
        {
            e.ToTable("Levels");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.Color).IsRequired();
            e.Property(x => x.Icon).IsRequired();
            e.HasIndex(x => x.Order);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.Color).IsRequired();
        });

        modelBuilder.Entity<TaskItem>(e =>
        {
            e.ToTable("Tasks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Title).IsRequired();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.ScheduledDate);
        });

        modelBuilder.Entity<DayAssignment>(e =>
        {
            e.ToTable("DayAssignments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Outcome).HasConversion<string>();
            e.HasIndex(x => x.LogicalDate);
            e.HasIndex(x => x.TaskId);
        });

        modelBuilder.Entity<DayLog>(e =>
        {
            e.ToTable("DayLogs");
            e.HasKey(x => x.LogicalDate);
            e.Property(x => x.LogicalDate).ValueGeneratedNever();
        });

        modelBuilder.Entity<FavoriteTemplate>(e =>
        {
            e.ToTable("Favorites");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Title).IsRequired();
        });

        modelBuilder.Entity<RecurrenceRule>(e =>
        {
            e.ToTable("Recurrences");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.TemplateTitle).IsRequired();
            e.Property(x => x.Frequency).HasConversion<string>();
            // Spec §3: DaysOfWeek se guarda como flags (bit n = (DayOfWeek)n).
            e.Property(x => x.DaysOfWeek)
                .HasConversion(
                    v => DaysOfWeekFlags.ToFlags(v),
                    v => DaysOfWeekFlags.FromFlags(v),
                    new ValueComparer<DayOfWeek[]>(
                        (a, b) => a != null && b != null && a.SequenceEqual(b),
                        v => v.Aggregate(0, (h, d) => HashCode.Combine(h, (int)d)),
                        v => v.ToArray()));
        });

        // AppSettings es un documento único: una sola fila con clave sombra Id = 1.
        modelBuilder.Entity<AppSettings>(e =>
        {
            e.ToTable("Settings");
            e.Property<int>(SettingsIdProperty).ValueGeneratedNever();
            e.HasKey(SettingsIdProperty);
            e.Property(x => x.Theme).HasConversion<string>();
        });
    }

    private sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
        v => v.UtcTicks,
        v => new DateTimeOffset(v, TimeSpan.Zero));
}

internal static class DaysOfWeekFlags
{
    public static int ToFlags(DayOfWeek[] days) => days.Aggregate(0, (acc, d) => acc | (1 << (int)d));

    public static DayOfWeek[] FromFlags(int flags) =>
        Enum.GetValues<DayOfWeek>().Where(d => (flags & (1 << (int)d)) != 0).OrderBy(d => (int)d).ToArray();
}
