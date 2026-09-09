# RaylibGameFramework.Menus

Version 0.1.2 adds optional descriptive rows to the existing Controls presentation.

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
