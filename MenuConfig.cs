using System.Text.Json;

namespace RaylibGameFramework.Menus;

public sealed class MenuConfig
{
    public string StartMenu { get; set; } = string.Empty;
    public Dictionary<string, MenuDefinition> Menus { get; set; } = [];
}

public sealed class MenuDefinition
{
    public string Title { get; set; } = string.Empty;
    public List<MenuItemDefinition> Items { get; set; } = [];
}

public sealed class MenuItemDefinition
{
    public string Type { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Function { get; set; } = string.Empty;
    public string? Target { get; set; }
    public string? Action { get; set; }
    public string? KeyboardMouseDescription { get; set; }
    public string? ControllerDescription { get; set; }
    public List<string> Options { get; set; } = [];
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Step { get; set; }
    public JsonElement Value { get; set; }
}
