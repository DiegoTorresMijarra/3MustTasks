using System.Diagnostics;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace Spike.Common;

public sealed record ScenarioResult(
    string Phase, int TasksRead, string[] SampleTitles, double OpenMs, double WriteSmallMs,
    double WriteBulkMs, double ReadAllMs, double SingleWriteMs, int BulkCount, string Storage, string? Error);

/// <summary>Mismo escenario para ambas opciones, ejecutado a traves de los puertos de Core.</summary>
public static class Scenario
{
    public const int BulkCount = 1000;
    static readonly DateTimeOffset T0 = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);

    static Guid IdOf(int i) => new(i, 0, 0, new byte[8]);

    static TaskItem Make(string title, int i) => new()
    {
        Id = IdOf(i),
        Title = title,
        Notes = "notas de ejemplo " + i,
        LevelId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
        DueTime = new TimeOnly(9 + i % 8, 30),
        ReminderOffset = TimeSpan.FromMinutes(15),
        ScheduledDate = new DateOnly(2026, 10, 22),
        Status = TaskItemStatus.Active,
        CreatedAt = T0.AddMinutes(i),
        UpdatedAt = T0.AddMinutes(i),
    };

    /// <param name="open">Abre/inicializa el almacenamiento (esquema, carga del fichero...).</param>
    public static async Task<ScenarioResult> RunAsync(string phase, ITaskRepository repo, IUnitOfWork uow,
        Func<Task> open, Func<string> storage)
    {
        double openMs = 0, ws = 0, wb = 0, ra = 0, sw = 0; int n = 0; string[] titles = [];
        try
        {
            var t = Stopwatch.StartNew();
            await open();
            openMs = t.Elapsed.TotalMilliseconds;

            if (phase == "write")
            {
                t.Restart();
                await repo.UpsertAsync(Make("Terminar informe", 1));
                await repo.UpsertAsync(Make("Llamar al medico", 2));
                await repo.UpsertAsync(Make("Comprar pan", 3));
                await uow.SaveChangesAsync();
                ws = t.Elapsed.TotalMilliseconds;

                t.Restart();
                for (var i = 100; i < 100 + BulkCount; i++) await repo.UpsertAsync(Make("Tarea " + i, i));
                await uow.SaveChangesAsync();
                wb = t.Elapsed.TotalMilliseconds;

                t.Restart();
                await repo.UpsertAsync(Make("Tarea suelta", 5000));
                await uow.SaveChangesAsync();
                sw = t.Elapsed.TotalMilliseconds;
            }

            t.Restart();
            var all = await repo.ListAsync();
            ra = t.Elapsed.TotalMilliseconds;
            n = all.Count;
            titles = all.OrderBy(x => x.CreatedAt).Take(3).Select(x => x.Title).ToArray();
            var one = await repo.GetAsync(IdOf(1));
            if (n > 0 && (one is null || one.DueTime != new TimeOnly(10, 30) || one.ReminderOffset != TimeSpan.FromMinutes(15)
                || one.ScheduledDate != new DateOnly(2026, 10, 22) || one.CreatedAt != T0.AddMinutes(1)))
                throw new Exception("Roundtrip de campos incorrecto: " + one);
            return new(phase, n, titles, openMs, ws, wb, ra, sw, BulkCount, storage(), null);
        }
        catch (Exception e)
        {
            return new(phase, n, titles, openMs, ws, wb, ra, sw, BulkCount, storage(), e.ToString());
        }
    }
}
