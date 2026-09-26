# RaylibGameFramework.Menus

Version 0.1.3 adds an optional list/detail layout, read-only selection state, and
button payloads. Standard menus retain their existing layout and input behavior.

## List and detail menus

Set `"Layout": "ListWithDetail"` on a menu definition to place its list on the left
and reserve the right for consumer rendering. Omitted `Layout` defaults to
`Standard`; unsupported layouts are rejected by the loader.

```json
{
  "StartMenu": "Selection",
  "Menus": {
    "Selection": {
      "Title": "Choose an item",
      "Layout": "ListWithDetail",
      "Items": [
        { "Type": "Button", "Text": "First item", "Function": "Choose", "Value": "first" },
        { "Type": "Button", "Text": "Second item", "Function": "Choose", "Value": "second" },
        { "Type": "Button", "Text": "Back", "Function": "Back" }
      ]
    }
  }
}
```

New API:

- `MenuDefinition.Layout`: `MenuLayout.Standard` or `MenuLayout.ListWithDetail`.
- `MenuManager.CurrentMenuName`: current menu dictionary key.
- `MenuManager.SelectedIndex`: index in the original Items list, including labels/spacers.
- `MenuManager.SelectedItem`: immutable `MenuItemState` snapshot with the item's fields,
  copied read-only Options, and JSON Value. Read it after Update for the latest state.
- `MenuManager.DetailPanelBounds`: nullable Raylib `Rectangle` in screen coordinates;
  null in Standard. Query while a Raylib window is open. It updates with screen size
  and menu changes and does not scroll with the list.

The split uses 16-pixel outer margins, a 32-pixel gutter (including the scrollbar),
and 40% of the remaining width for the list. The detail area shares the list's
vertical viewport below the title. Margins/gutter shrink for very narrow windows.
Keyboard/controller navigation, selection-following scroll, and list controls use
the existing behavior. Mouse hover/clicks/wheel affect only the left list.
Long list text is clipped at the list boundary.

Call `Update()` as usual, then draw your own detail content after `Draw()`:

```csharp
menus.Draw();
if (menus.DetailPanelBounds is Rectangle bounds)
{
    MenuItemState selected = menus.SelectedItem;
    Raylib.BeginScissorMode((int)bounds.X, (int)bounds.Y,
        (int)bounds.Width, (int)bounds.Height);
    Raylib.DrawText(selected.Text, (int)bounds.X + 12, (int)bounds.Y + 12, 24, Color.Black);
    // Draw your preview, description, or other content here.
    Raylib.EndScissorMode();
}
```

The consumer owns detail drawing, clipping, and any detail input. The package adds
no game-specific models or rendering callbacks. Use `Raylib_cs` and
`RaylibGameFramework.Menus` namespaces for this example.

Ordinary Button actions now return configured Value as a cloned `JsonElement`
through `MenuAction.Value`, supporting strings, numbers, booleans, arrays, and objects.
For example, a Choose action above can be read with
`((JsonElement)action.Value!).GetString()` (using `System.Text.Json`).
Missing or JSON null values still return null. OpenMenu and Back retain their
navigation behavior and return no action; Toggle, Selector, and Slider retain
their existing CLR value types.

## Descriptive controls

Version 0.1.2 added optional descriptive rows to the existing Controls presentation.

```json
{
  "StartMenu": "Main",
  "Menus": {
    "Main": {
      "Title": "Main",
      "Items": [
        { "Type": "Button", "Text": "Controls", "Function": "OpenMenu", "Target": "Controls" }
      ]
    },
    "Controls": {
      "Title": "Controls",
      "Items": [
        { "Type": "KeyBind", "Text": "Jump", "Action": "Jump", "Function": "Rebind" },
        {
          "Type": "ControlDescription",
          "Text": "Look Around",
          "KeyboardMouseDescription": "Mouse Left / Right",
          "ControllerDescription": "Right Stick Left / Right"
        },
        { "Type": "Button", "Text": "Back", "Function": "Back" }
      ]
    }
  }
}
```

A `ControlDescription` requires a nonblank `Text` and both description strings.
Empty descriptions are allowed for an unsupported device. The strings are literal
display text: combinations and gestures are not parsed into bindings. These fields
are optional for all existing item types.

Descriptive rows can receive focus for reading and scrolling, highlighting the whole
row. Confirm, clicks, and left/right adjustment do nothing on them, even if an
`Action` or `Function` was supplied. They never look up bindings or start capture.
Keep an interactive item such as Back in each menu, as required by the existing loader.

`KeyBind` still uses `Action` to look up bindings and captures for the active input
device family. `Rebind` is the conventional Function value, not a separate item Type.
Existing definitions and package dependencies are unchanged.

Both row types share the existing three columns, sizing, font fitting, colors, and
viewport. Long lists (including 12 controls plus Back) use the existing wheel scrolling,
scrollbar, and selection-following MenuUp/MenuDown navigation. Bind those navigation
actions to keyboard and controller in the consuming game's input config. Back remains
in the navigation order; MenuBack returns through menu history. Smaller menus retain
their centered layout.

`MenuManager.GetBindingDisplayName(InputBinding?)` is now public for callers that need
the same friendly names. It covers both sticks, D-pad, face buttons, triggers (RT/LT),
shoulders, stick clicks, and mouse directions. Null bindings display Unbound; unknown
inputs retain the existing fallback. Descriptions are displayed as supplied.

Validation:
- `dotnet build`
- `dotnet test tests/MenuChecks/MenuChecks.csproj`

The headless regression check covers schema loading, description activation safety,
mixed-list navigation through Back, legacy button/toggle activation, and input names.
For an interactive smoke check, open Controls from Main, navigate a 12-row mixed list
with keyboard and controller, verify the selected row follows scrolling, click/confirm
a description, rebind a KeyBind with each device, cancel capture with MenuBack, and
return using Back. Rendering and physical input capture require this interactive check.

Detail-panel checks additionally cover legacy/default and invalid layouts, responsive
geometry, mouse-region isolation, selection snapshots, and JSON button payloads.
Before release, visually test ListWithDetail at 1280x720 and a smaller window with
a long list: keyboard/controller wrap and scroll, mouse hover/click/wheel, Back and
OpenMenu transitions, resizing, long text clipping, and a consumer-rendered preview
that follows selection. Verify scrolling over the detail panel does not move the list,
and compare a Standard menu with its previous appearance. These visual checks remain
manual; automated tests do not open a window.
