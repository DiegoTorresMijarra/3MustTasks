using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Core.Tests;

public class OnboardingServiceTests
{
    private readonly IServiceProvider _sp;
    private readonly OnboardingService _onboarding;
    private readonly LevelService _levels;
    private readonly ISettingsStore _settings;

    public OnboardingServiceTests()
    {
        _sp = new ServiceCollection()
            .AddInMemoryPersistence()
            .AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero)))
            .AddSingleton<LevelService>()
            .AddSingleton<OnboardingService>()
            .BuildServiceProvider();
        _onboarding = _sp.GetRequiredService<OnboardingService>();
        _levels = _sp.GetRequiredService<LevelService>();
        _settings = _sp.GetRequiredService<ISettingsStore>();
    }

    private async Task<Level> Level(Guid id) => (await _levels.ListAsync()).Single(l => l.Id == id);

    [Fact]
    public void Is_pending_on_first_launch_with_planning_time_0730()
    {
        Assert.True(_onboarding.IsPending);
        Assert.Equal(new TimeOnly(7, 30), _onboarding.PlanningTime);
    }

    [Fact]
    public void Complete_marks_it_done_and_saves_the_planning_time_without_touching_other_settings()
    {
        _settings.Save(_settings.Load() with { StrictMode = true });

        _onboarding.Complete(new TimeOnly(6, 45));

        var s = _settings.Load();
        Assert.False(_onboarding.IsPending);
        Assert.Equal(new TimeOnly(6, 45), s.PlanningReminderTime);
        Assert.True(s.StrictMode);
    }

    [Fact]
    public void Complete_without_time_keeps_the_current_one()
    {
        _settings.Save(_settings.Load() with { PlanningReminderTime = new TimeOnly(9, 0) });

        _onboarding.Complete();

        Assert.False(_onboarding.IsPending);
        Assert.Equal(new TimeOnly(9, 0), _onboarding.PlanningTime);
    }

    [Theory]
    [InlineData(QuotaPreset.Balanced, 1, 2)]
    [InlineData(QuotaPreset.Demanding, 2, 1)]
    [InlineData(QuotaPreset.Beast, 3, 0)]
    public async Task Preset_sets_the_quotas_of_must_and_recommended(QuotaPreset preset, int must, int recommended)
    {
        await _onboarding.ApplyPresetAsync(preset);

        var m = await Level(DefaultLevels.MustId);
        var r = await Level(DefaultLevels.RecommendedId);
        Assert.Equal((must, must), (m.MinToAdd, m.MinToComplete));
        Assert.Equal((recommended, 0), (r.MinToAdd, r.MinToComplete));
        Assert.True(m.Enabled);
        Assert.True(r.Enabled);
        Assert.Equal(must + recommended, OnboardingService.Counts(preset)!.Value.Must + OnboardingService.Counts(preset)!.Value.Recommended);
    }

    [Fact]
    public async Task Balanced_matches_the_factory_defaults_for_must_and_adds_two_recommended()
    {
        await _onboarding.ApplyPresetAsync(QuotaPreset.Balanced);

        var m = await Level(DefaultLevels.MustId);
        var r = await Level(DefaultLevels.RecommendedId);
        Assert.Equal((1, 1), (m.MinToAdd, m.MinToComplete));
        Assert.Equal((2, 0), (r.MinToAdd, r.MinToComplete));
    }

    [Fact]
    public async Task Custom_changes_nothing()
    {
        var before = await _levels.ListAsync();

        await _onboarding.ApplyPresetAsync(QuotaPreset.Custom);

        Assert.Equal(before, await _levels.ListAsync());
        Assert.Null(OnboardingService.Counts(QuotaPreset.Custom));
    }

    [Fact]
    public async Task Preset_raises_a_max_that_is_lower_than_the_new_minimum()
    {
        var must = await Level(DefaultLevels.MustId);
        await _levels.SaveAsync(must.Id, new LevelInput(must.Name, must.Color, must.Icon, 1, 1, 1, true));

        await _onboarding.ApplyPresetAsync(QuotaPreset.Beast);

        var m = await Level(DefaultLevels.MustId);
        Assert.Equal(3, m.MinToAdd);
        Assert.Equal(3, m.Max);
    }

    [Fact]
    public async Task Preset_skips_a_default_level_that_no_longer_exists()
    {
        await _levels.ListAsync();
        await _levels.DeleteAsync(DefaultLevels.MaybeId);
        await _levels.DeleteAsync(DefaultLevels.RecommendedId);

        await _onboarding.ApplyPresetAsync(QuotaPreset.Demanding);

        Assert.Equal(2, (await Level(DefaultLevels.MustId)).MinToAdd);
        Assert.DoesNotContain(await _levels.ListAsync(), l => l.Id == DefaultLevels.RecommendedId);
    }
}
