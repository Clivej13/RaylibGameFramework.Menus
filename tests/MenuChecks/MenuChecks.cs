using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using RaylibGameFramework.Input;
using RaylibGameFramework.Menus;

public sealed class MenuChecks
{
    [Xunit.Fact]
    public void ControlsRegression()
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        static object? Call(object? instance, string name, params object[] args) =>
            typeof(MenuManager).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!
                .Invoke(instance, args);

        var description = new MenuItemDefinition
        {
            Type = "ControlDescription", Text = "Gesture",
            KeyboardMouseDescription = "Shift + Mouse", ControllerDescription = "RT + Right Stick",
            // Even accidental rebind metadata must not activate capture.
            Action = "Jump", Function = "Rebind"
        };
        var menu = new MenuDefinition { Title = "Controls" };
        menu.Items.Add(new() { Type = "KeyBind", Text = "Jump", Action = "Jump", Function = "Rebind" });
        for (int i = 0; i < 11; i++) menu.Items.Add(description);
        menu.Items.Add(new() { Type = "Button", Text = "Back", Function = "Back" });
        foreach (var item in menu.Items) item.Value = JsonSerializer.SerializeToElement<object?>(null);
        var config = new MenuConfig { StartMenu = "Controls", Menus = { ["Controls"] = menu } };
        string path = Path.Combine(Path.GetTempPath(), $"menu-check-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(config));
            var loaded = MenuConfigLoader.Load(path);
            Check(loaded.Menus["Controls"].Items[1].ControllerDescription == "RT + Right Stick", "Description round trip");
            description.ControllerDescription = null;
            File.WriteAllText(path, JsonSerializer.Serialize(config));
            try { MenuConfigLoader.Load(path); throw new Exception("Missing description accepted"); }
            catch (InvalidDataException) { }
            description.ControllerDescription = "RT + Right Stick";
        }
        finally { File.Delete(path); }

        // No input controller: any accidental access to capture in these paths fails.
        var manager = (MenuManager)RuntimeHelpers.GetUninitializedObject(typeof(MenuManager));
        typeof(MenuManager).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, config);
        typeof(MenuManager).GetField("_currentMenuName", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, "Controls");
        var selected = typeof(MenuManager).GetField("_selectedIndex", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Check(Call(manager, "ActivateItem", description, 1) is null, "Description activation");
        selected.SetValue(manager, 1);
        Check(Call(manager, "AdjustSelected", 1) is null, "Description adjustment");
        selected.SetValue(manager, 0);
        for (int i = 1; i <= 12; i++)
        {
            Call(manager, "MoveSelection", 1);
            Check((int)selected.GetValue(manager)! == i, "Every row and Back reachable");
        }
        Call(manager, "MoveSelection", 1);
        Check((int)selected.GetValue(manager)! == 0, "Navigation wraps");
        Call(manager, "MoveSelection", -1);
        Check((int)selected.GetValue(manager)! == 12, "Reverse navigation reaches Back");
        Check((float)Call(null, "GetContentHeight", menu)! == 922, "13 rows retain existing heights and spacing");
        foreach (string type in new[] { "Button", "Toggle", "Selector", "Slider", "KeyBind", "ControlDescription" })
            Check((bool)Call(null, "IsFocusable", new MenuItemDefinition { Type = type })!, type + " focus");
        Check(!(bool)Call(null, "IsFocusable", new MenuItemDefinition { Type = "Label" })!, "Labels skipped");
        var toggle = new MenuItemDefinition { Type = "Toggle", Function = "Sound", Value = JsonSerializer.SerializeToElement(false) };
        Check(((MenuAction)Call(manager, "ActivateItem", toggle, 1)!).Value is true, "Toggle still activates");
        Check(((MenuAction)Call(manager, "ActivateItem", new MenuItemDefinition { Type = "Button", Function = "Play" }, 1)!).Function == "Play", "Button still activates");
        var names = new Dictionary<string, string>
        {
            ["LeftXNegative"] = "Left Stick Left", ["LeftXPositive"] = "Left Stick Right",
            ["LeftYNegative"] = "Left Stick Up", ["LeftYPositive"] = "Left Stick Down",
            ["RightXNegative"] = "Right Stick Left", ["RightXPositive"] = "Right Stick Right",
            ["RightYNegative"] = "Right Stick Up", ["RightYPositive"] = "Right Stick Down",
            ["LeftFaceLeft"] = "D-Pad Left", ["LeftFaceRight"] = "D-Pad Right",
            ["LeftFaceUp"] = "D-Pad Up", ["LeftFaceDown"] = "D-Pad Down",
            ["RightTrigger2"] = "RT", ["LeftTrigger2"] = "LT",
            ["RightFaceDown"] = "A", ["RightFaceRight"] = "B",
            ["MouseXNegative"] = "Mouse Left", ["MouseXPositive"] = "Mouse Right",
            ["MouseYNegative"] = "Mouse Up", ["MouseYPositive"] = "Mouse Down"
        };
        foreach (var (input, expected) in names)
        {
            var binding = JsonSerializer.Deserialize<InputBinding>(JsonSerializer.Serialize(new
            {
                Action = "Example", Device = input.StartsWith("Mouse") ? "Mouse" : "Gamepad", Input = input
            }))!;
            Check(MenuManager.GetBindingDisplayName(binding) == expected, input);
        }
        Check(MenuManager.GetBindingDisplayName(null) == "Unbound", "Unbound fallback");
    }
}
