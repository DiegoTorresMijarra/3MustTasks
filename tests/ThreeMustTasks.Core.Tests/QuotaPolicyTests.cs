using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Services;

namespace ThreeMustTasks.Core.Tests;

public class DefaultLevelsTests
{
    [Fact]
    public void Defaults_match_PRD_5_1()
    {
        var levels = DefaultLevels.Create();

        Assert.Equal(["Must", "Recommended", "Maybe"], levels.Select(l => l.Name));
        Assert.Equal([(1, 1), (2, 0), (0, 0)], levels.Select(l => (l.MinToAdd, l.MinToComplete)));
        Assert.All(levels, l => { Assert.Null(l.Max); Assert.True(l.Enabled); Assert.False(string.IsNullOrEmpty(l.Icon)); });
        Assert.Equal([0, 1, 2], levels.Select(l => l.Order));
        Assert.Equal(3, levels.Select(l => l.Id).Distinct().Count());
    }
}

public class QuotaPolicyTests
{
    private static Level L(string name, int add, int complete, int? max = null, bool enabled = true, int order = 0) =>
        new(Guid.NewGuid(), name, "#000", "flag", add, complete, max, order, enabled);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    public void CanAdd_respects_max(int planned, bool expected) =>
        Assert.Equal(expected, QuotaPolicy.CanAdd(L("Must", 1, 1, max: 2), planned));

    [Fact]
    public void CanAdd_without_max_is_unlimited() =>
        Assert.True(QuotaPolicy.CanAdd(L("Maybe", 0, 0), 500));

    [Fact]
    public void CanAdd_with_max_zero_never_admits() =>
        Assert.False(QuotaPolicy.CanAdd(L("Maybe", 0, 0, max: 0), 0));

    [Fact]
    public void CanAdd_ignores_disabled_levels() =>
        Assert.False(QuotaPolicy.CanAdd(L("Off", 0, 0, enabled: false), 0));

    [Fact]
    public void Evaluate_with_empty_plan_reports_everything_missing()
    {
        var must = L("Must", 1, 1, order: 0);
        var rec = L("Recommended", 2, 0, order: 1);
        var maybe = L("Maybe", 0, 0, order: 2);

        var status = QuotaPolicy.Evaluate([maybe, rec, must], []);

        Assert.False(status.IsPlanMinimumMet);
        Assert.Equal([("Must", 1), ("Recommended", 2)], status.MissingToAdd.Select(m => (m.Level.Name, m.Missing)));
        Assert.False(status.IsCompletionMinimumMet);
        Assert.Equal([("Must", 1)], status.MissingToComplete.Select(m => (m.Level.Name, m.Missing)));
    }

    [Fact]
    public void Evaluate_is_met_when_minimums_are_reached_and_extras_do_not_matter()
    {
        var must = L("Must", 1, 1);
        var rec = L("Recommended", 2, 0);
        var plan = new[]
        {
            new PlanEntry(must.Id, true),
            new PlanEntry(rec.Id, false),
            new PlanEntry(rec.Id, false),
            new PlanEntry(rec.Id, false),
        };

        var status = QuotaPolicy.Evaluate([must, rec], plan);

        Assert.True(status.IsPlanMinimumMet);
        Assert.Empty(status.MissingToAdd);
        Assert.True(status.IsCompletionMinimumMet);
        Assert.Empty(status.MissingToComplete);
    }

    [Fact]
    public void Evaluate_planned_but_not_completed_meets_add_but_not_complete()
    {
        var must = L("Must", 1, 1);

        var status = QuotaPolicy.Evaluate([must], [new PlanEntry(must.Id, false)]);

        Assert.True(status.IsPlanMinimumMet);
        Assert.False(status.IsCompletionMinimumMet);
    }

    [Fact]
    public void Evaluate_ignores_disabled_levels()
    {
        var must = L("Must", 1, 1);
        var off = L("Off", 3, 3, enabled: false);

        var status = QuotaPolicy.Evaluate([must, off], [new PlanEntry(must.Id, true)]);

        Assert.True(status.IsPlanMinimumMet);
        Assert.True(status.IsCompletionMinimumMet);
    }
}
