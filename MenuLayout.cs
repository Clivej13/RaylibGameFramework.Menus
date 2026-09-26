using System.Text.Json.Serialization;

namespace RaylibGameFramework.Menus;

[JsonConverter(typeof(JsonStringEnumConverter<MenuLayout>))]
public enum MenuLayout
{
    Standard,
    ListWithDetail
}
