using System.Windows;
using ClaudeUsageWidget.Presentation.Wpf.Windows;
using ClaudeUsageWidget.Tests.Support;

namespace ClaudeUsageWidget.Tests.Presentation;

public sealed class TaskbarThemeTests
{
    /// <summary>
    /// The window swaps one dictionary for the other when the Windows mode changes. A key
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
        var assembly = typeof(TaskbarWindow).Assembly.GetName().Name;
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"/{assembly};component/Theme/{theme}.xaml", UriKind.Relative)
        };

        return dictionary.Keys.Cast<object>().Select(key => key.ToString()!).Order().ToList();
    }
}
