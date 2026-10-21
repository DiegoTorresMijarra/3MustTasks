using ThreeMustTasks.Core.Domain;

namespace ThreeMustTasks.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_match_the_spec()
    {
        var s = new AppSettings();

        Assert.Equal(new TimeOnly(0, 0), s.DayStartsAt);
        Assert.False(s.StrictMode);
        Assert.True(s.AutoAddScheduled);
        Assert.Equal(ThemeMode.System, s.Theme);
        Assert.Equal(DayOfWeek.Monday, s.FirstDayOfWeek);
    }
}
