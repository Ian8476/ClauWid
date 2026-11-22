using System.Windows;
using System.Windows.Interop;
using ClaudeUsageWidget.Presentation.Wpf.Taskbar;
using ClaudeUsageWidget.Tests.Support;

namespace ClaudeUsageWidget.Tests.Presentation;

/// <summary>The menu itself is modal and is not opened here: only what it owns is checked.</summary>
public sealed class TaskbarMenuTests
{
    [Fact]
    public void Owns_a_hidden_window_for_as_long_as_it_lives() => Sta.Run(() =>
    {
        var before = OwnWindowCount();

        var menu = new TaskbarMenu();

        Assert.Equal(before + 1, OwnWindowCount());

        menu.Dispose();
        menu.Dispose();

        Assert.Equal(before, OwnWindowCount());
    });

    [Fact]
    public void Refuses_to_open_once_disposed() => Sta.Run(() =>
    {
        var menu = new TaskbarMenu();
        menu.Dispose();

        Assert.Throws<ObjectDisposedException>(() => menu.ShowAt(new Point(0, 0)));
    });

    [Fact]
    public void Applying_the_menu_theme_never_fails_wherever_it_is_not_supported()
    {
        MenuThemeSwitch.Apply(light: false);
        MenuThemeSwitch.Apply(light: true);
    }

    private static int OwnWindowCount() => PresentationSource.CurrentSources.OfType<HwndSource>().Count();
}
