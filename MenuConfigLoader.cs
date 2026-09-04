using System.Text.Json;

namespace RaylibGameFramework.Menus;

public static class MenuConfigLoader
{
    private static readonly HashSet<string> SupportedTypes =
        ["Button", "Toggle", "Selector", "Slider", "KeyBind", "Label", "Spacer"];

    public static MenuConfig Load(string path)
    {
        string resolvedPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);
        string json = File.ReadAllText(resolvedPath);
        MenuConfig config = JsonSerializer.Deserialize<MenuConfig>(json)
            ?? throw new InvalidDataException($"Could not load menu configuration from '{path}'.");

        if (string.IsNullOrWhiteSpace(config.StartMenu) || !config.Menus.ContainsKey(config.StartMenu))
        {
            throw new InvalidDataException("The menu configuration must specify an existing StartMenu.");
        }

        foreach ((string name, MenuDefinition menu) in config.Menus)
        {
            if (menu.Items.Count == 0)
            {
                throw new InvalidDataException($"Menu '{name}' must contain one or more items.");
            }

            if (!menu.Items.Any(item => item.Type is "Button" or "Toggle" or "Selector" or "Slider" or "KeyBind"))
            {
                throw new InvalidDataException($"Menu '{name}' must contain at least one interactive item.");
            }

            foreach (MenuItemDefinition item in menu.Items)
            {
                if (!SupportedTypes.Contains(item.Type))
                {
                    throw new InvalidDataException($"Menu '{name}' contains unsupported item type '{item.Type}'.");
                }

                ValidateItem(name, item);
            }
        }

        return config;
    }

    private static void ValidateItem(string menuName, MenuItemDefinition item)
    {
        switch (item.Type)
        {
            case "KeyBind" when string.IsNullOrWhiteSpace(item.Action):
                throw new InvalidDataException($"KeyBind '{item.Text}' in menu '{menuName}' requires an Action.");

            case "Toggle" when item.Value.ValueKind is not JsonValueKind.True and not JsonValueKind.False:
                throw new InvalidDataException($"Toggle '{item.Text}' in menu '{menuName}' requires a boolean Value.");

            case "Selector" when item.Options.Count == 0 ||
                                 item.Value.ValueKind != JsonValueKind.String ||
                                 !item.Options.Contains(item.Value.GetString()!):
                throw new InvalidDataException(
                    $"Selector '{item.Text}' in menu '{menuName}' requires Options and a Value from those options.");

            case "Slider" when item.Min is null || item.Max is null || item.Step is null ||
                               item.Min >= item.Max || item.Step <= 0 ||
                               item.Value.ValueKind != JsonValueKind.Number:
                throw new InvalidDataException(
                    $"Slider '{item.Text}' in menu '{menuName}' requires valid Min, Max, Step, and numeric Value fields.");

            case "Slider":
                double value = item.Value.GetDouble();
                if (value < item.Min || value > item.Max)
                {
                    throw new InvalidDataException(
                        $"Slider '{item.Text}' in menu '{menuName}' has a Value outside its Min/Max range.");
                }

                break;
        }
    }
}
