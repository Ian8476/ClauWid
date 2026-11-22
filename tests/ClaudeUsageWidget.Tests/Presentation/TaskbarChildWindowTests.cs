using ClaudeUsageWidget.Presentation.Wpf.Taskbar;
using ClaudeUsageWidget.Tests.Support;

namespace ClaudeUsageWidget.Tests.Presentation;

public sealed class TaskbarChildWindowTests
{
    [Fact]
    public void Goes_above_the_taskbars_own_content_and_leaves_the_order_alone_afterwards() => Sta.Run(() =>
    {
        using var taskbar = new FakeTaskbar();

        // Explorer's surface is already there and covers the whole taskbar. A new child goes
        // to the bottom of the order, under it, which is why the widget has to climb.
        var content = taskbar.AddContentChild();
        using var child = NewChild(taskbar);
        var ours = Assert.Single(NativeWindows.WidgetWindows());
        Assert.Equal([content, ours], NativeWindows.ChildrenTopDown(taskbar.Handle));

        child.EnsureAboveTaskbarContent();

        Assert.Equal([ours, content], NativeWindows.ChildrenTopDown(taskbar.Handle));

        child.EnsureAboveTaskbarContent();

        Assert.Equal([ours, content], NativeWindows.ChildrenTopDown(taskbar.Handle));
    });

    [Fact]
    public void Is_a_layered_child_that_does_not_take_the_focus() => Sta.Run(() =>
    {
        using var taskbar = new FakeTaskbar();
        using var child = NewChild(taskbar);
        var ours = Assert.Single(NativeWindows.WidgetWindows());

        Assert.Equal(taskbar.Handle, NativeWindows.ParentOf(ours));
        Assert.True(NativeWindows.IsChild(ours));
        Assert.True(NativeWindows.HasExtendedStyle(ours, NativeWindows.LayeredStyle));
        Assert.True(NativeWindows.HasExtendedStyle(ours, NativeWindows.NoActivateStyle));
    });

    [Fact]
    public void Refuses_to_activate_the_taskbar_when_clicked() => Sta.Run(() =>
    {
        using var taskbar = new FakeTaskbar();
        using var child = NewChild(taskbar);
        var ours = Assert.Single(NativeWindows.WidgetWindows());

        var answer = NativeWindows.AskHowToHandleLeftClick(ours, taskbar.Handle);

        Assert.Equal(NativeWindows.MouseActivateNoActivate, answer);
    });

    [Fact]
    public void Keeps_the_request_for_a_context_menu_from_reaching_the_taskbar() => Sta.Run(() =>
    {
        using var taskbar = new FakeTaskbar();
        using var child = NewChild(taskbar);
        var ours = Assert.Single(NativeWindows.WidgetWindows());

        var answer = NativeWindows.SendContextMenu(ours);

        Assert.Equal(IntPtr.Zero, answer);
        Assert.False(taskbar.Received(NativeWindows.WmContextMenu));
    });

    [Fact]
    public void Places_itself_in_the_taskbars_client_area() => Sta.Run(() =>
    {
        using var taskbar = new FakeTaskbar();
        using var child = NewChild(taskbar);
        var ours = Assert.Single(NativeWindows.WidgetWindows());
        var slot = TaskbarSlot.For(taskbar.Geometry, child.NaturalSize, child.DpiScale);

        child.Place(slot);

        Assert.Equal(slot.Bounds, NativeWindows.ScreenBounds(ours));
    });

    [Fact]
    public void Shows_and_hides_itself_without_depending_on_the_taskbar() => Sta.Run(() =>
    {
        using var taskbar = new FakeTaskbar();
        using var child = NewChild(taskbar);
        var ours = Assert.Single(NativeWindows.WidgetWindows());
        Assert.False(child.IsVisible);

        child.Show();

        Assert.True(child.IsVisible);
        Assert.True(NativeWindows.IsShown(ours));

        child.Hide();

        Assert.False(child.IsVisible);
    });

    [Fact]
    public void Reports_being_destroyed_with_the_taskbar_but_not_when_disposed() => Sta.Run(() =>
    {
        var destroyedTaskbar = new FakeTaskbar();
        var orphan = NewChild(destroyedTaskbar);
        var disposedTaskbar = new FakeTaskbar();
        var disposed = NewChild(disposedTaskbar);
        var reports = new List<object?>();
        orphan.Destroyed += (sender, _) => reports.Add(sender);
        disposed.Destroyed += (sender, _) => reports.Add(sender);

        disposed.Dispose();
        Assert.Empty(reports);

        destroyedTaskbar.Dispose();

        Assert.Same(orphan, Assert.Single(reports));
        orphan.Dispose();
        disposedTaskbar.Dispose();
    });

    private static TaskbarChildWindow NewChild(FakeTaskbar taskbar) =>
        new(taskbar.Handle, TestData.NewViewModel());
}
