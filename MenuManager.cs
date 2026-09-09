using System.Numerics;
using System.Text.Json;
using Raylib_cs;
using RaylibGameFramework.Input;

namespace RaylibGameFramework.Menus;

public sealed class MenuManager
{
    private const int MaxItemWidth = 900;
    private const int ControlHeight = 58;
    private const int LabelHeight = 36;
    private const int SpacerHeight = 28;
    private const int ItemGap = 14;
    private const int TitleFontSize = 48;
    private const int ItemFontSize = 24;
    private const int ContentTop = 145;
    private const int ContentBottomMargin = 28;
    private const float MouseWheelSpeed = 70f;

    private readonly MenuConfig _config;
    private readonly InputController _input;
    private readonly InputConfig _inputConfig;
    private readonly Stack<string> _history = new();
    private string _currentMenuName;
    private int _selectedIndex;
    private float _scrollOffset;
    private int _lastViewportHeight = -1;
    private MenuNavigationMode _navigationMode = MenuNavigationMode.Navigation;
    private Vector2 _lastMousePosition;
    private bool _hasMousePosition;
    private bool _suppressMenuInput;

    public MenuManager(MenuConfig config, InputController input, InputConfig inputConfig)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _inputConfig = inputConfig ?? throw new ArgumentNullException(nameof(inputConfig));
        _currentMenuName = config.StartMenu;
        _selectedIndex = FindFirstFocusableIndex(CurrentMenu);
    }

    public MenuAction? Update()
    {
        if (_input.CompletedRebind is InputRebindResult completedRebind)
        {
            if (!IsMenuBackBinding(completedRebind))
            {
                _input.ApplyRebind(completedRebind);
            }

            _input.CancelRebind();
            _suppressMenuInput = true;
            return null;
        }

        if (_input.IsRebinding)
        {
            if (_input.WasPressed("MenuBack"))
            {
                _input.CancelRebind();
                _suppressMenuInput = true;
            }

            return null;
        }

        if (_suppressMenuInput)
        {
            _suppressMenuInput = false;
            return null;
        }

        MenuDefinition menu = CurrentMenu;
        ClampScroll(menu);

        int viewportHeight = GetViewportHeight();
        if (viewportHeight != _lastViewportHeight)
        {
            _lastViewportHeight = viewportHeight;
            EnsureSelectedVisible();
        }

        Vector2 mousePosition = Raylib.GetMousePosition();
        bool mouseMoved = _hasMousePosition && mousePosition != _lastMousePosition;
        _lastMousePosition = mousePosition;
        _hasMousePosition = true;

        bool navigationInput =
            _input.WasPressed("MenuUp") ||
            _input.WasPressed("MenuDown") ||
            _input.WasPressed("MenuLeft") ||
            _input.WasPressed("MenuRight") ||
            _input.WasPressed("MenuConfirm") ||
            _input.WasPressed("MenuBack");
        bool mousePressed = Raylib.IsMouseButtonPressed(MouseButton.Left);

        if (navigationInput)
        {
            _navigationMode = MenuNavigationMode.Navigation;
        }
        else if (mouseMoved || mousePressed)
        {
            _navigationMode = MenuNavigationMode.Mouse;
        }

        if (IsPointInViewport(mousePosition))
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                _scrollOffset -= wheel * MouseWheelSpeed;
                ClampScroll(menu);
            }

            if (_navigationMode == MenuNavigationMode.Mouse)
            {
                for (int index = 0; index < menu.Items.Count; index++)
                {
                    MenuItemDefinition item = menu.Items[index];
                    if (!IsFocusable(item))
                    {
                        continue;
                    }

                    Rectangle bounds = GetItemBounds(menu, index);
                    if (!Raylib.CheckCollisionPointRec(mousePosition, bounds))
                    {
                        continue;
                    }

                    _selectedIndex = index;

                    if (item.Type == "Slider" && Raylib.IsMouseButtonDown(MouseButton.Left))
                    {
                        return SetSliderFromMouse(item, bounds, mousePosition.X);
                    }

                    if (mousePressed)
                    {
                        return ActivateItem(item, mousePosition.X < bounds.X + (bounds.Width / 2f) ? -1 : 1);
                    }
                }
            }
        }

        if (_input.WasPressed("MenuUp"))
        {
            MoveSelection(-1);
            EnsureSelectedVisible();
        }
        else if (_input.WasPressed("MenuDown"))
        {
            MoveSelection(1);
            EnsureSelectedVisible();
        }

        if (_input.WasPressed("MenuLeft"))
        {
            return AdjustSelected(-1);
        }

        if (_input.WasPressed("MenuRight"))
        {
            return AdjustSelected(1);
        }

        if (_input.WasPressed("MenuConfirm"))
        {
            MenuItemDefinition item = CurrentMenu.Items[_selectedIndex];
            return item.Type is "Button" or "Toggle" or "KeyBind" ? ActivateItem(item, 1) : null;
        }

        if (_input.WasPressed("MenuBack"))
        {
            GoBack();
        }

        return null;
    }

    public void Draw()
    {
        MenuDefinition menu = CurrentMenu;
        int titleX = (Raylib.GetScreenWidth() - Raylib.MeasureText(menu.Title, TitleFontSize)) / 2;
        Raylib.DrawText(menu.Title, titleX, 30, TitleFontSize, Color.DarkBlue);

        if (menu.Items.Any(item => item.Type is "KeyBind" or "ControlDescription"))
        {
            DrawKeyBindHeadings();
        }

        int viewportHeight = GetViewportHeight();
        Raylib.BeginScissorMode(0, ContentTop, Raylib.GetScreenWidth(), viewportHeight);

        for (int index = 0; index < menu.Items.Count; index++)
        {
            MenuItemDefinition item = menu.Items[index];
            Rectangle bounds = GetItemBounds(menu, index);

            if (bounds.Y + bounds.Height < ContentTop || bounds.Y > ContentTop + viewportHeight)
            {
                continue;
            }

            DrawItem(item, bounds, index == _selectedIndex);
        }

        Raylib.EndScissorMode();
        DrawScrollBar(menu);
    }

    public void ReturnToStartMenu()
    {
        _history.Clear();
        _currentMenuName = _config.StartMenu;
        ResetMenuPosition();
    }

    private MenuDefinition CurrentMenu => _config.Menus[_currentMenuName];

    private MenuAction? ActivateItem(MenuItemDefinition item, int direction)
    {
        switch (item.Type)
        {
            case "Button":
                return ActivateButton(item);
            case "Toggle":
                return SetToggle(item);
            case "Selector":
                return SetSelector(item, direction);
            case "Slider":
                return AdjustSlider(item, direction);
            case "KeyBind":
                _input.BeginRebind(item.Action!, _input.ActiveDeviceFamily);
                return null;
            default:
                return null;
        }
    }

    private MenuAction? ActivateButton(MenuItemDefinition item)
    {
        if (item.Function == "OpenMenu")
        {
            if (string.IsNullOrWhiteSpace(item.Target) || !_config.Menus.ContainsKey(item.Target))
            {
                throw new InvalidOperationException($"Menu item '{item.Text}' has an invalid target.");
            }

            _history.Push(_currentMenuName);
            _currentMenuName = item.Target;
            ResetMenuPosition();
            return null;
        }

        if (item.Function == "Back")
        {
            GoBack();
            return null;
        }

        return new MenuAction(item.Function);
    }

    private MenuAction? AdjustSelected(int direction)
    {
        MenuItemDefinition item = CurrentMenu.Items[_selectedIndex];
        return item.Type switch
        {
            "Toggle" => SetToggle(item),
            "Selector" => SetSelector(item, direction),
            "Slider" => AdjustSlider(item, direction),
            _ => null
        };
    }

    private static MenuAction SetToggle(MenuItemDefinition item)
    {
        bool value = !item.Value.GetBoolean();
        item.Value = JsonSerializer.SerializeToElement(value);
        return new MenuAction(item.Function, value);
    }

    private static MenuAction SetSelector(MenuItemDefinition item, int direction)
    {
        int current = item.Options.IndexOf(item.Value.GetString()!);
        int next = (current + direction + item.Options.Count) % item.Options.Count;
        string value = item.Options[next];
        item.Value = JsonSerializer.SerializeToElement(value);
        return new MenuAction(item.Function, value);
    }

    private static MenuAction? AdjustSlider(MenuItemDefinition item, int direction)
    {
        double current = item.Value.GetDouble();
        double value = Math.Clamp(current + (direction * item.Step!.Value), item.Min!.Value, item.Max!.Value);
        return SetSliderValue(item, value);
    }

    private static MenuAction? SetSliderFromMouse(MenuItemDefinition item, Rectangle bounds, float mouseX)
    {
        const float trackPadding = 22f;
        float trackStart = bounds.X + trackPadding;
        float trackWidth = bounds.Width - (trackPadding * 2f);
        double ratio = Math.Clamp((mouseX - trackStart) / trackWidth, 0f, 1f);
        double rawValue = item.Min!.Value + (ratio * (item.Max!.Value - item.Min.Value));
        double steps = Math.Round((rawValue - item.Min.Value) / item.Step!.Value);
        double value = Math.Clamp(item.Min.Value + (steps * item.Step.Value), item.Min.Value, item.Max.Value);
        return SetSliderValue(item, value);
    }

    private static MenuAction? SetSliderValue(MenuItemDefinition item, double value)
    {
        double current = item.Value.GetDouble();
        if (Math.Abs(current - value) < double.Epsilon)
        {
            return null;
        }

        item.Value = JsonSerializer.SerializeToElement(value);
        return new MenuAction(item.Function, value);
    }

    private void MoveSelection(int direction)
    {
        MenuDefinition menu = CurrentMenu;
        int index = _selectedIndex;

        do
        {
            index = (index + direction + menu.Items.Count) % menu.Items.Count;
        }
        while (!IsFocusable(menu.Items[index]));

        _selectedIndex = index;
    }

    private void GoBack()
    {
        if (_history.TryPop(out string? previousMenu))
        {
            _currentMenuName = previousMenu;
            ResetMenuPosition();
        }
    }

    private void ResetMenuPosition()
    {
        _selectedIndex = FindFirstFocusableIndex(CurrentMenu);
        _scrollOffset = 0;
        EnsureSelectedVisible();
    }

    private void EnsureSelectedVisible()
    {
        MenuDefinition menu = CurrentMenu;
        float itemTop = GetItemOffset(menu, _selectedIndex);
        float itemBottom = itemTop + GetItemHeight(menu.Items[_selectedIndex]);
        float viewportHeight = GetViewportHeight();

        if (itemTop < _scrollOffset)
        {
            _scrollOffset = itemTop;
        }
        else if (itemBottom > _scrollOffset + viewportHeight)
        {
            _scrollOffset = itemBottom - viewportHeight;
        }

        ClampScroll(menu);
    }

    private void ClampScroll(MenuDefinition menu)
    {
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, GetContentHeight(menu) - GetViewportHeight()));
    }

    private static bool IsFocusable(MenuItemDefinition item) =>
        item.Type is "Button" or "Toggle" or "Selector" or "Slider" or "KeyBind" or "ControlDescription";

    private static int FindFirstFocusableIndex(MenuDefinition menu)
    {
        int index = menu.Items.FindIndex(IsFocusable);
        if (index < 0)
        {
            throw new InvalidDataException("Every menu must contain at least one interactive item.");
        }

        return index;
    }

    private static int GetItemHeight(MenuItemDefinition item) => item.Type switch
    {
        "Label" => LabelHeight,
        "Spacer" => SpacerHeight,
        _ => ControlHeight
    };

    private static float GetItemOffset(MenuDefinition menu, int index)
    {
        float offset = 0;
        for (int itemIndex = 0; itemIndex < index; itemIndex++)
        {
            offset += GetItemHeight(menu.Items[itemIndex]) + ItemGap;
        }

        return offset;
    }

    private static float GetContentHeight(MenuDefinition menu)
    {
        if (menu.Items.Count == 0)
        {
            return 0;
        }

        return menu.Items.Sum(GetItemHeight) + ((menu.Items.Count - 1) * ItemGap);
    }

    private static int GetViewportHeight() => Math.Max(1, Raylib.GetScreenHeight() - ContentTop - ContentBottomMargin);

    private static float GetContentOriginY(MenuDefinition menu)
    {
        float contentHeight = GetContentHeight(menu);
        return contentHeight < GetViewportHeight()
            ? ContentTop + ((GetViewportHeight() - contentHeight) / 2f)
            : ContentTop;
    }

    private Rectangle GetItemBounds(MenuDefinition menu, int index)
    {
        int itemWidth = GetItemWidth();
        float x = (Raylib.GetScreenWidth() - itemWidth) / 2f;
        float y = GetContentOriginY(menu) + GetItemOffset(menu, index) - _scrollOffset;
        return new Rectangle(x, y, itemWidth, GetItemHeight(menu.Items[index]));
    }

    private static int GetItemWidth() =>
        Math.Min(MaxItemWidth, Math.Max(1, Raylib.GetScreenWidth() - 32));

    private static bool IsPointInViewport(Vector2 point) =>
        point.Y >= ContentTop && point.Y <= ContentTop + GetViewportHeight();

    private void DrawItem(MenuItemDefinition item, Rectangle bounds, bool selected)
    {
        if (item.Type == "Spacer")
        {
            return;
        }

        if (item.Type == "Label")
        {
            Raylib.DrawText(item.Text, (int)bounds.X, (int)bounds.Y + 4, 28, Color.DarkBlue);
            return;
        }

        bool highlightWholeRow = selected && item.Type != "KeyBind";
        Raylib.DrawRectangleRec(bounds, highlightWholeRow ? Color.SkyBlue : Color.LightGray);
        Raylib.DrawRectangleLinesEx(
            bounds,
            highlightWholeRow ? 4 : 2,
            highlightWholeRow ? Color.DarkBlue : Color.Gray);

        if (item.Type == "Slider")
        {
            DrawSlider(item, bounds);
            return;
        }

        if (item.Type is "KeyBind" or "ControlDescription")
        {
            DrawKeyBind(item, bounds, selected);
            return;
        }

        string text = item.Type switch
        {
            "Toggle" => $"{item.Text}: {(item.Value.GetBoolean() ? "On" : "Off")}",
            "Selector" => $"{item.Text}: < {item.Value.GetString()} >",
            _ => item.Text
        };

        int textX = (int)bounds.X + (((int)bounds.Width - Raylib.MeasureText(text, ItemFontSize)) / 2);
        int textY = (int)bounds.Y + ((ControlHeight - ItemFontSize) / 2);
        Raylib.DrawText(text, textX, textY, ItemFontSize, Color.Black);
    }

    private void DrawKeyBind(MenuItemDefinition item, Rectangle bounds, bool selected)
    {
        Rectangle keyboardBounds = GetBindingCellBounds(bounds, InputDeviceFamily.KeyboardMouse);
        Rectangle gamepadBounds = GetBindingCellBounds(bounds, InputDeviceFamily.Gamepad);
        DrawBindingCell(keyboardBounds, selected && (item.Type == "ControlDescription" || _input.ActiveDeviceFamily == InputDeviceFamily.KeyboardMouse));
        DrawBindingCell(gamepadBounds, selected && (item.Type == "ControlDescription" || _input.ActiveDeviceFamily == InputDeviceFamily.Gamepad));

        int textY = (int)bounds.Y + ((ControlHeight - ItemFontSize) / 2);
        int actionWidth = Math.Max(1, (int)(bounds.Width * 0.34f) - 24);
        int actionFontSize = FitFontSize(item.Text, ItemFontSize, actionWidth);
        int actionY = textY + ((ItemFontSize - actionFontSize) / 2);
        Raylib.DrawText(item.Text, (int)bounds.X + 12, actionY, actionFontSize, Color.Black);
        DrawBindingText(item, keyboardBounds, InputDeviceFamily.KeyboardMouse, textY);
        DrawBindingText(item, gamepadBounds, InputDeviceFamily.Gamepad, textY);
    }

    private static void DrawBindingCell(Rectangle bounds, bool selected)
    {
        Raylib.DrawRectangleRec(bounds, selected ? Color.SkyBlue : Color.LightGray);
        Raylib.DrawRectangleLinesEx(bounds, selected ? 4 : 1, selected ? Color.DarkBlue : Color.Gray);
    }

    private void DrawBindingText(
        MenuItemDefinition item,
        Rectangle bounds,
        InputDeviceFamily family,
        int textY)
    {
        if (item.Type == "ControlDescription")
        {
            DrawCellText(family == InputDeviceFamily.KeyboardMouse
                ? item.KeyboardMouseDescription ?? string.Empty
                : item.ControllerDescription ?? string.Empty, bounds, textY);
            return;
        }

        bool isCapturing = _input.IsRebinding &&
            string.Equals(_input.RebindingAction, item.Action, StringComparison.OrdinalIgnoreCase) &&
            _input.RebindingDeviceFamily == family;
        InputBinding? binding = _input.GetBinding(item.Action!, family);
        string text = isCapturing ? "Press an input..." : GetBindingDisplayName(binding);
        DrawCellText(text, bounds, textY);
    }

    private static void DrawCellText(string text, Rectangle bounds, int textY)
    {
        int availableWidth = Math.Max(1, (int)bounds.Width - 12);
        int fontSize = FitFontSize(text, ItemFontSize, availableWidth);
        int textX = (int)bounds.X + (((int)bounds.Width - Raylib.MeasureText(text, fontSize)) / 2);
        int adjustedY = textY + ((ItemFontSize - fontSize) / 2);
        Raylib.DrawText(text, textX, adjustedY, fontSize, Color.Black);
    }

    private static int FitFontSize(string text, int preferredSize, int availableWidth)
    {
        int fontSize = preferredSize;
        while (fontSize > 14 && Raylib.MeasureText(text, fontSize) > availableWidth)
        {
            fontSize--;
        }

        return fontSize;
    }

    public static string GetBindingDisplayName(InputBinding? binding)
    {
        if (binding is null)
        {
            return "Unbound";
        }

        return binding.Input switch
        {
            "RightFaceDown" => "A",
            "RightFaceRight" => "B",
            "RightFaceLeft" => "X",
            "RightFaceUp" => "Y",
            "LeftYNegative" => "Left Stick Up",
            "LeftYPositive" => "Left Stick Down",
            "LeftXNegative" => "Left Stick Left",
            "LeftXPositive" => "Left Stick Right",
            "RightYNegative" => "Right Stick Up",
            "RightYPositive" => "Right Stick Down",
            "RightXNegative" => "Right Stick Left",
            "RightXPositive" => "Right Stick Right",
            "LeftFaceLeft" => "D-Pad Left",
            "LeftFaceRight" => "D-Pad Right",
            "LeftFaceUp" => "D-Pad Up",
            "LeftFaceDown" => "D-Pad Down",
            "RightTrigger" or "RightTrigger2" => "RT",
            "LeftTrigger" or "LeftTrigger2" => "LT",
            "RightTrigger1" => "RB",
            "LeftTrigger1" => "LB",
            "LeftThumb" => "Left Stick Click",
            "RightThumb" => "Right Stick Click",
            "MouseXNegative" => "Mouse Left",
            "MouseXPositive" => "Mouse Right",
            "MouseYNegative" => "Mouse Up",
            "MouseYPositive" => "Mouse Down",
            _ when string.Equals(binding.Device, "Mouse", StringComparison.OrdinalIgnoreCase) =>
                $"Mouse {binding.Input}",
            _ => binding.Input
        };
    }

    private static Rectangle GetBindingCellBounds(Rectangle bounds, InputDeviceFamily family)
    {
        float actionWidth = bounds.Width * 0.34f;
        float bindingWidth = (bounds.Width - actionWidth) / 2f;
        float x = bounds.X + actionWidth +
            (family == InputDeviceFamily.Gamepad ? bindingWidth : 0);
        return new Rectangle(x, bounds.Y, bindingWidth, bounds.Height);
    }

    private static void DrawKeyBindHeadings()
    {
        int itemWidth = GetItemWidth();
        float x = (Raylib.GetScreenWidth() - itemWidth) / 2f;
        float actionWidth = itemWidth * 0.34f;
        float bindingWidth = (itemWidth - actionWidth) / 2f;
        const int fontSize = 20;
        const int y = ContentTop - 30;

        DrawCenteredHeading("Action", x, actionWidth, y, fontSize);
        DrawCenteredHeading("Keyboard / Mouse", x + actionWidth, bindingWidth, y, fontSize);
        DrawCenteredHeading("Controller", x + actionWidth + bindingWidth, bindingWidth, y, fontSize);
    }

    private static void DrawCenteredHeading(string text, float x, float width, int y, int fontSize)
    {
        int fittedFontSize = FitFontSize(text, fontSize, Math.Max(1, (int)width - 8));
        int textX = (int)(x + ((width - Raylib.MeasureText(text, fittedFontSize)) / 2f));
        Raylib.DrawText(text, textX, y, fittedFontSize, Color.DarkGray);
    }

    private bool IsMenuBackBinding(InputRebindResult result) =>
        _inputConfig.Bindings.Any(binding =>
            string.Equals(binding.Action, "MenuBack", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(binding.Device, result.Device, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(binding.Input, result.Input, StringComparison.OrdinalIgnoreCase));

    private static void DrawSlider(MenuItemDefinition item, Rectangle bounds)
    {
        string text = $"{item.Text}: {item.Value.GetDouble():0.##}";
        Raylib.DrawText(text, (int)bounds.X + 18, (int)bounds.Y + 8, 20, Color.Black);

        const float padding = 22f;
        float startX = bounds.X + padding;
        float endX = bounds.X + bounds.Width - padding;
        float lineY = bounds.Y + bounds.Height - 14;
        double ratio = (item.Value.GetDouble() - item.Min!.Value) / (item.Max!.Value - item.Min.Value);
        float knobX = startX + ((endX - startX) * (float)ratio);

        Raylib.DrawLineEx(new Vector2(startX, lineY), new Vector2(endX, lineY), 4, Color.Gray);
        Raylib.DrawCircleV(new Vector2(knobX, lineY), 8, Color.DarkBlue);
    }

    private void DrawScrollBar(MenuDefinition menu)
    {
        float contentHeight = GetContentHeight(menu);
        int viewportHeight = GetViewportHeight();
        if (contentHeight <= viewportHeight)
        {
            return;
        }

        float x = (Raylib.GetScreenWidth() + GetItemWidth()) / 2f + 14;
        float thumbHeight = Math.Max(28, viewportHeight * (viewportHeight / contentHeight));
        float travel = viewportHeight - thumbHeight;
        float maxScroll = contentHeight - viewportHeight;
        float thumbY = ContentTop + (travel * (_scrollOffset / maxScroll));

        Raylib.DrawRectangle((int)x, ContentTop, 6, viewportHeight, Color.LightGray);
        Raylib.DrawRectangle((int)x, (int)thumbY, 6, (int)thumbHeight, Color.DarkBlue);
    }
}

internal enum MenuNavigationMode
{
    Mouse,
    Navigation
}
