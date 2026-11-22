using System.Windows;
using ClaudeUsageWidget.Presentation.Wpf.Controls;
using ClaudeUsageWidget.Tests.Support;

namespace ClaudeUsageWidget.Tests.Presentation;

public sealed class TaskbarThemeTests
{
    /// <summary>
    /// The view swaps one dictionary for the other when the Windows mode changes. A key
    /// present in only one of them would leave something unpainted in the other mode.
    /// </summary>
    [Fact]
    public void Dark_and_light_define_the_same_neutrals() => Sta.Run(() =>
    {
        var dark = KeysOf("TaskbarDark");
        var light = KeysOf("TaskbarLight");

        Assert.NotEmpty(dark);
        Assert.Equal(dark, light);
    });

    private static List<string> KeysOf(string theme)
    {
        // A relative resource URI only resolves once WPF has registered the pack:// scheme, which
        // the Application type does when it is first touched. In the widget that is on startup; here
        // nothing may have touched it yet, which depends on which test the runner started with.
        _ = System.Windows.Application.Current;

        var assembly = typeof(TaskbarWidgetView).Assembly.GetName().Name;
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"/{assembly};component/Theme/{theme}.xaml", UriKind.Relative)
        };

        return dictionary.Keys.Cast<object>().Select(key => key.ToString()!).Order().ToList();
    }
}
