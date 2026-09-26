using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Raylib_cs;
using RaylibGameFramework.Menus;
using Xunit;

public sealed class DetailPanelChecks
{
    private static object? Call(object? instance, string name, params object[] args) =>
        typeof(MenuManager).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!
            .Invoke(instance, args);

    private static MenuConfig Load(string layout)
    {
        string path = Path.Combine(Path.GetTempPath(), $"menu-detail-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """
                {"StartMenu":"Main","Menus":{"Main":{
                """ + layout + """
                "Items":[{"Type":"Button","Text":"Choose","Function":"Choose","Value":{"id":"one"}}]}}}
                """);
            return MenuConfigLoader.Load(path);
        }
        finally { File.Delete(path); }
    }

    private static MenuManager Manager(MenuConfig config)
    {
        // Exercise state and activation without opening a native window or polling input.
        var manager = (MenuManager)RuntimeHelpers.GetUninitializedObject(typeof(MenuManager));
        typeof(MenuManager).GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(manager, config);
        typeof(MenuManager).GetField("_currentMenuName", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(manager, config.StartMenu);
        return manager;
    }

    [Theory]
    [InlineData("", MenuLayout.Standard)]
    [InlineData("\"Layout\":\"Standard\",", MenuLayout.Standard)]
    [InlineData("\"Layout\":\"ListWithDetail\",", MenuLayout.ListWithDetail)]
    public void LayoutLoadsWithLegacyDefault(string json, MenuLayout expected) =>
        Assert.Equal(expected, Load(json).Menus["Main"].Layout);

    [Theory]
    [InlineData("\"Layout\":\"Unknown\",")]
    [InlineData("\"Layout\":99,")]
    public void UnsupportedLayoutFails(string json)
    {
        Exception? error = Record.Exception(() => Load(json));
        Assert.True(error is JsonException or InvalidDataException);
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(640, 480)]
    [InlineData(100, 200)]
    [InlineData(0, 0)]
    public void SplitBoundsStaySeparatedAndTrackScreenSize(int width, int height)
    {
        var bounds = ((Rectangle List, Rectangle? Detail))Call(null, "CalculateLayoutBounds",
            MenuLayout.ListWithDetail, width, height)!;
        Rectangle detail = Assert.IsType<Rectangle>(bounds.Detail);
        Assert.True(bounds.List.X >= 0 && bounds.List.Width >= 0);
        Assert.True(detail.X >= bounds.List.X + bounds.List.Width);
        Assert.True(detail.Width >= 0 && detail.X + detail.Width <= width);
        Assert.Equal(bounds.List.Y, detail.Y);
        Assert.Equal(bounds.List.Height, detail.Height);
        Assert.Equal(Math.Max(1, height - 145 - 28), detail.Height);
    }

    [Theory]
    [InlineData(1280, 190, 900)]
    [InlineData(640, 16, 608)]
    public void StandardGeometryIsUnchanged(int width, float x, float itemWidth)
    {
        var bounds = ((Rectangle List, Rectangle? Detail))Call(null, "CalculateLayoutBounds",
            MenuLayout.Standard, width, 720)!;
        Assert.Null(bounds.Detail);
        Assert.Equal(x, bounds.List.X);
        Assert.Equal(itemWidth, bounds.List.Width);
    }

    [Fact]
    public void DetailAndGutterDoNotReceiveListMouseInput()
    {
        var bounds = ((Rectangle List, Rectangle? Detail))Call(null, "CalculateLayoutBounds",
            MenuLayout.ListWithDetail, 1280, 720)!;
        bool Contains(MenuLayout layout, float x, float y) =>
            (bool)Call(null, "ContainsListPoint", layout, bounds.List, new System.Numerics.Vector2(x, y))!;
        Assert.True(Contains(MenuLayout.ListWithDetail, bounds.List.X + 10, 200));
        Assert.False(Contains(MenuLayout.ListWithDetail, bounds.List.X + bounds.List.Width + 1, 200));
        Assert.False(Contains(MenuLayout.ListWithDetail, bounds.Detail!.Value.X + 10, 200));
        Assert.False(Contains(MenuLayout.ListWithDetail, bounds.List.X + 10, 100));
        Assert.False(Contains(MenuLayout.ListWithDetail, bounds.List.X + 10, 710));
        // Legacy wheel handling spans the viewport horizontally.
        Assert.True(Contains(MenuLayout.Standard, 0, 200));
        Assert.True(Contains(MenuLayout.Standard, 1279, 200));
    }

    [Fact]
    public void SelectionSnapshotIsReadOnlyAndNavigationUsesOriginalIndices()
    {
        MenuConfig config = Load("\"Layout\":\"ListWithDetail\",");
        var first = config.Menus["Main"].Items[0];
        first.Options.Add("original");
        config.Menus["Main"].Items.Add(new() { Type = "Label", Text = "Heading" });
        config.Menus["Main"].Items.Add(new() { Type = "Button", Text = "Second", Function = "Choose" });
        MenuManager manager = Manager(config);
        Assert.Equal("Main", manager.CurrentMenuName);
        Assert.Equal(0, manager.SelectedIndex);
        var snapshot = manager.SelectedItem;
        first.Text = "Changed";
        first.Options[0] = "changed";
        first.Value = JsonSerializer.SerializeToElement("changed");
        Assert.Equal("Choose", snapshot.Text);
        Assert.Equal("original", snapshot.Options[0]);
        Assert.Equal("one", snapshot.Value.GetProperty("id").GetString());
        Assert.All(typeof(MenuItemState).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)snapshot.Options)[0] = "mutation");
        Call(manager, "MoveSelection", 1);
        Assert.Equal(2, manager.SelectedIndex);
        Assert.Equal("Second", manager.SelectedItem.Text);
        Call(manager, "MoveSelection", 1);
        Assert.Equal(0, manager.SelectedIndex);
        Call(manager, "MoveSelection", -1);
        Assert.Equal(2, manager.SelectedIndex);
    }

    [Theory]
    [InlineData("\"one\"")]
    [InlineData("42")]
    [InlineData("1.25")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("{\"id\":\"one\"}")]
    [InlineData("[1,2]")]
    public void ButtonsReturnJsonPayloads(string json)
    {
        MenuManager manager = Manager(Load(""));
        var item = new MenuItemDefinition { Type = "Button", Function = "Choose" };
        using (JsonDocument document = JsonDocument.Parse(json))
        {
            item.Value = document.RootElement;
            var action = Assert.IsType<MenuAction>(Call(manager, "ActivateItem", item, 1));
            Assert.Equal("Choose", action.Function);
            item.Value = default;
            document.Dispose();
            Assert.Equal(json, Assert.IsType<JsonElement>(action.Value).GetRawText());
        }
    }

    [Fact]
    public void MissingAndNullButtonValuesRemainNull()
    {
        MenuManager manager = Manager(Load(""));
        var item = new MenuItemDefinition { Type = "Button", Function = "Play" };
        Assert.Null(Assert.IsType<MenuAction>(Call(manager, "ActivateItem", item, 1)).Value);
        item.Value = JsonSerializer.SerializeToElement<object?>(null);
        Assert.Null(Assert.IsType<MenuAction>(Call(manager, "ActivateItem", item, 1)).Value);
    }
}
