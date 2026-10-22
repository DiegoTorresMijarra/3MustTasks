using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;
using ThreeMustTasks.UI;

namespace ThreeMustTasks.UI.Tests;

public sealed class OnboardingTests : BunitContext
{
    private sealed class FakePermission(bool onlyWhileOpen, NotificationPermissionState result) : INotificationPermission
    {
        public int Requests { get; private set; }
        public bool OnlyWorksWhileOpen => onlyWhileOpen;
        public Task<NotificationPermissionState> RequestAsync(CancellationToken ct = default)
        {
            Requests++;
            return Task.FromResult(result);
        }
    }

    private readonly FakePermission _permission;

    public OnboardingTests() : this(new FakePermission(onlyWhileOpen: true, NotificationPermissionState.Granted)) { }

    private OnboardingTests(FakePermission permission)
    {
        _permission = permission;
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero)));
        Services.AddInMemoryPersistence();
        Services.AddSingleton<INotificationPermission>(permission);
        Services.AddThreeMustTasksUI();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private ISettingsStore Store => Services.GetRequiredService<ISettingsStore>();
    private NavigationManager Nav => Services.GetRequiredService<NavigationManager>();
    private LevelService Levels => Services.GetRequiredService<LevelService>();

    private IRenderedComponent<AppRoot> Start(string url = "/")
    {
        Nav.NavigateTo(url);
        return Render<AppRoot>();
    }

    private static void Next(IRenderedComponent<AppRoot> cut) => cut.Find("[data-testid=onb-next]").Click();

    // --- Solo la primera vez / repetir desde Ajustes ---

    [Fact]
    public void First_launch_redirects_to_the_onboarding_and_hides_the_tab_bar()
    {
        var cut = Start();

        Assert.EndsWith("/onboarding", Nav.Uri);
        Assert.NotEmpty(cut.FindAll("[data-testid=onb-step1]"));
        Assert.Empty(cut.FindAll("nav.tmt-bottom-nav"));
    }

    [Fact]
    public void It_does_not_appear_again_once_completed()
    {
        Store.Save(Store.Load() with { OnboardingCompleted = true });

        var cut = Start();

        Assert.DoesNotContain("/onboarding", Nav.Uri);
        Assert.Empty(cut.FindAll("[data-testid=onboarding]"));
        Assert.NotEmpty(cut.FindAll("nav.tmt-bottom-nav"));
    }

    [Fact]
    public void Finishing_the_flow_marks_it_done_so_the_next_launch_goes_straight_to_today()
    {
        var cut = Start();
        Next(cut);
        Next(cut);
        cut.Find("[data-testid=onb-finish]").Click();

        Assert.True(Store.Load().OnboardingCompleted);
        Assert.DoesNotContain("onboarding", Nav.Uri);

        // Con el flag guardado, el siguiente arranque ya no redirige (ver It_does_not_appear_again_once_completed).
        Assert.False(Services.GetRequiredService<OnboardingService>().IsPending);
    }

    [Fact]
    public async Task Skip_marks_it_done_without_changing_levels_or_time()
    {
        var cut = Start();

        cut.Find("[data-testid=onb-skip]").Click();

        var s = Store.Load();
        Assert.True(s.OnboardingCompleted);
        Assert.Equal(new TimeOnly(7, 30), s.PlanningReminderTime);
        Assert.Equal(1, (await Levels.ListAsync()).Single(l => l.Id == DefaultLevels.MustId).MinToAdd);
        Assert.DoesNotContain("onboarding", Nav.Uri);
    }

    [Fact]
    public async Task Settings_offers_a_link_to_repeat_it_and_the_flow_works_when_already_completed()
    {
        Store.Save(Store.Load() with { OnboardingCompleted = true, PlanningReminderTime = new TimeOnly(9, 0) });
        var cut = Start("/ajustes");

        Assert.Equal("onboarding", cut.Find("[data-testid=onboarding-link]").GetAttribute("href"));

        Nav.NavigateTo("onboarding");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=onb-step1]")));
        Next(cut);
        cut.Find("[data-testid=onb-preset-input-Demanding]").Change(true);
        Next(cut);
        Assert.Equal("09:00", cut.Find("[data-testid=onb-time]").GetAttribute("value"));
        cut.Find("[data-testid=onb-finish]").Click();

        Assert.Equal(2, (await Levels.ListAsync()).Single(l => l.Id == DefaultLevels.MustId).MinToAdd);
    }

    // --- Pasos: concepto, cupos, hora ---

    [Fact]
    public void Three_steps_with_progress_indicator()
    {
        var cut = Start();

        Assert.Equal("Paso 1 de 3", cut.Find("[data-testid=onb-steps]").GetAttribute("aria-label"));
        Next(cut);
        Assert.NotEmpty(cut.FindAll("[data-testid=onb-step2]"));
        Assert.Equal("Paso 2 de 3", cut.Find("[data-testid=onb-steps]").GetAttribute("aria-label"));
        Next(cut);
        Assert.NotEmpty(cut.FindAll("[data-testid=onb-step3]"));
        Assert.Equal(3, cut.FindAll("[data-testid=onb-steps] span.on").Count);
        Assert.Empty(cut.FindAll("[data-testid=onb-next]"));
    }

    [Fact]
    public void Back_returns_to_the_previous_step()
    {
        var cut = Start();
        Next(cut);

        cut.Find("[data-testid=onb-back]").Click();

        Assert.NotEmpty(cut.FindAll("[data-testid=onb-step1]"));
    }

    [Fact]
    public void Quota_step_matches_the_mock_presets_with_balanced_recommended_and_preselected()
    {
        var cut = Start();
        Next(cut);

        var names = cut.FindAll(".tmt-onb-preset-name").Select(e => e.TextContent.Trim()).ToArray();
        Assert.StartsWith("Equilibrado", names[0]);
        Assert.Equal(["Exigente", "Modo bestia", "Personalizado"], names.Skip(1).ToArray());
        Assert.NotEmpty(cut.FindAll("[data-testid=onb-recommended]"));
        Assert.True(cut.Find("[data-testid=onb-preset-input-Balanced]").HasAttribute("checked"));
        Assert.Contains("estas 3 tareas", cut.Find("[data-testid=onb-summary]").TextContent);
        Assert.Equal(["1 Must", "2 Recommended"], cut.FindAll("[data-testid=onb-preset-Balanced] .tmt-onb-chip").Select(e => e.TextContent).ToArray());
    }

    [Theory]
    [InlineData("Balanced", 1, 2)]
    [InlineData("Demanding", 2, 1)]
    [InlineData("Beast", 3, 0)]
    public async Task Accepting_a_preset_applies_its_quotas(string preset, int must, int recommended)
    {
        var cut = Start();
        Next(cut);
        cut.Find($"[data-testid=onb-preset-input-{preset}]").Change(true);
        Next(cut);

        var levels = await Levels.ListAsync();
        Assert.Equal(must, levels.Single(l => l.Id == DefaultLevels.MustId).MinToAdd);
        Assert.Equal(recommended, levels.Single(l => l.Id == DefaultLevels.RecommendedId).MinToAdd);
    }

    [Fact]
    public async Task Skipping_from_the_quota_step_does_not_apply_the_selected_preset()
    {
        var cut = Start();
        Next(cut);
        cut.Find("[data-testid=onb-preset-input-Beast]").Change(true);

        cut.Find("[data-testid=onb-skip]").Click();

        Assert.Equal(1, (await Levels.ListAsync()).Single(l => l.Id == DefaultLevels.MustId).MinToAdd);
    }

    [Fact]
    public void Custom_preset_finishes_in_the_levels_settings()
    {
        var cut = Start();
        Next(cut);
        cut.Find("[data-testid=onb-preset-input-Custom]").Change(true);
        Next(cut);
        cut.Find("[data-testid=onb-finish]").Click();

        Assert.EndsWith("/ajustes/niveles", Nav.Uri);
        Assert.True(Store.Load().OnboardingCompleted);
    }

    [Fact]
    public void Planning_time_defaults_to_0730_and_is_saved()
    {
        var cut = Start();
        Next(cut);
        Next(cut);
        Assert.Equal("07:30", cut.Find("[data-testid=onb-time]").GetAttribute("value"));

        cut.Find("[data-testid=onb-time]").Change("06:15");
        cut.Find("[data-testid=onb-finish]").Click();

        Assert.Equal(new TimeOnly(6, 15), Store.Load().PlanningReminderTime);
    }

    [Fact]
    public void Invalid_planning_time_is_ignored()
    {
        var cut = Start();
        Next(cut);
        Next(cut);

        cut.Find("[data-testid=onb-time]").Change("");
        cut.Find("[data-testid=onb-finish]").Click();

        Assert.Equal(new TimeOnly(7, 30), Store.Load().PlanningReminderTime);
    }

    // --- Permiso de notificaciones y limitaciones de la PWA ---

    [Fact]
    public void Permission_button_asks_through_the_port_and_shows_the_result()
    {
        var cut = Start();
        Next(cut);
        Next(cut);

        cut.Find("[data-testid=onb-request-permission]").Click();

        Assert.Equal(1, _permission.Requests);
        Assert.NotEmpty(cut.FindAll("[data-testid=onb-permission-granted]"));
    }

    [Fact]
    public void Finishing_asks_for_permission_if_it_was_not_asked_yet_but_not_twice()
    {
        var cut = Start();
        Next(cut);
        Next(cut);
        cut.Find("[data-testid=onb-finish]").Click();
        Assert.Equal(1, _permission.Requests);

        using var asked = new OnboardingTestsHost(new FakePermission(true, NotificationPermissionState.Granted));
        var cut2 = asked.Start();
        Next(cut2);
        Next(cut2);
        cut2.Find("[data-testid=onb-request-permission]").Click();
        cut2.Find("[data-testid=onb-finish]").Click();
        Assert.Equal(1, asked.Permission.Requests);
    }

    [Fact]
    public void PWA_warns_that_notifications_only_work_while_the_app_is_open()
    {
        var cut = Start();
        Next(cut);
        Next(cut);

        var warning = cut.Find("[data-testid=onb-pwa-warning]");
        Assert.Contains("pestaña o la app abierta", warning.TextContent);
    }

    [Fact]
    public void Hosts_without_the_limitation_do_not_show_the_PWA_warning()
    {
        using var android = new OnboardingTestsHost(new FakePermission(false, NotificationPermissionState.Unsupported));
        var cut = android.Start();
        Next(cut);
        Next(cut);

        Assert.Empty(cut.FindAll("[data-testid=onb-pwa-warning]"));
        cut.Find("[data-testid=onb-request-permission]").Click();
        Assert.NotEmpty(cut.FindAll("[data-testid=onb-permission-unsupported]"));
    }

    [Fact]
    public void Denied_permission_explains_how_to_continue_and_does_not_block_finishing()
    {
        using var denied = new OnboardingTestsHost(new FakePermission(true, NotificationPermissionState.Denied));
        var cut = denied.Start();
        Next(cut);
        Next(cut);
        cut.Find("[data-testid=onb-request-permission]").Click();

        Assert.NotEmpty(cut.FindAll("[data-testid=onb-permission-denied]"));
        cut.Find("[data-testid=onb-finish]").Click();
        Assert.True(denied.Store.Load().OnboardingCompleted);
    }

    [Fact]
    public async Task Default_port_implementation_is_a_no_op_that_reports_unsupported()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddInMemoryPersistence();
        ctx.Services.AddThreeMustTasksUI();

        var port = ctx.Services.GetRequiredService<INotificationPermission>();

        Assert.False(port.OnlyWorksWhileOpen);
        Assert.Equal(NotificationPermissionState.Unsupported, await port.RequestAsync());
    }

    /// <summary>Contexto con otro permiso inyectado.</summary>
    private sealed class OnboardingTestsHost : BunitContext
    {
        private readonly NavigationManager _nav;
        public FakePermission Permission { get; }

        public OnboardingTestsHost(FakePermission permission)
        {
            Permission = permission;
            JSInterop.Mode = JSRuntimeMode.Loose;
            Services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero)));
            Services.AddInMemoryPersistence();
            Services.AddSingleton<INotificationPermission>(permission);
            Services.AddThreeMustTasksUI();
            _nav = Services.GetRequiredService<NavigationManager>();
        }

        public ISettingsStore Store => Services.GetRequiredService<ISettingsStore>();

        public IRenderedComponent<AppRoot> Start()
        {
            _nav.NavigateTo("/");
            return Render<AppRoot>();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
