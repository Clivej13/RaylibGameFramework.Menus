using System.Text.Json;

namespace RaylibGameFramework.Menus;

/// <summary>An immutable snapshot of the selected item. Read SelectedItem again after Update.</summary>
public sealed class MenuItemState
{
    public string Type { get; }
    public string Text { get; }
    public string Function { get; }
    public string? Target { get; }
    public string? Action { get; }
    public string? KeyboardMouseDescription { get; }
    public string? ControllerDescription { get; }
    public IReadOnlyList<string> Options { get; }
    public double? Min { get; }
    public double? Max { get; }
    public double? Step { get; }
    public JsonElement Value { get; }

    internal MenuItemState(MenuItemDefinition item)
    {
        Type = item.Type;
        Text = item.Text;
        Function = item.Function;
        Target = item.Target;
        Action = item.Action;
        KeyboardMouseDescription = item.KeyboardMouseDescription;
        ControllerDescription = item.ControllerDescription;
        Options = Array.AsReadOnly(item.Options.ToArray());
        Min = item.Min;
        Max = item.Max;
        Step = item.Step;
        Value = item.Value.ValueKind == JsonValueKind.Undefined ? default : item.Value.Clone();
    }
}
