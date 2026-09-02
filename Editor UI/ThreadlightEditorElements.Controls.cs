namespace Threadlight.EditorUI {
using System;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
public static partial class ThreadlightEditorElements {
    private const string ObjectPickerIconGuid = "4e207d25354c4ae2bdfb4b1f45f9a8c7";
    private const string ObjectPickerIconPath =
        "Packages/com.wolfyvr.threadlight.authoring/Editor UI/Object Picker.png";
    private static Texture2D objectPickerIcon;
    private static readonly ConditionalWeakTable<Button, ToggleRefreshBinding>
        toggleRefreshBindings = new ConditionalWeakTable<Button, ToggleRefreshBinding>();
    public const string FieldClassName = "threadlight-field";
    public const string FieldStackedClassName = "threadlight-field--stacked";
    public const string ToggleRowClassName = "threadlight-toggle-row";
    public const string ToggleRowWrappedClassName = "threadlight-toggle-row--wrapped";
    public const string ToggleControlClassName = "threadlight-toggle-control";
    public static void StyleField<T>(BaseField<T> field,
        Func<Color> hoverBorder = null, Func<Color> focusBorder = null) {
        if (field == null || field.ClassListContains(FieldClassName)) return;
        ApplySharedStyles(field);
        field.AddToClassList(FieldClassName);
        BindWidthClass(field, FieldStackedClassName, ThreadlightEditorTheme.FieldStackBreakpoint);
        bool labeled = !string.IsNullOrWhiteSpace(field.label);
        field.labelElement.style.display = labeled ? DisplayStyle.Flex : DisplayStyle.None;
        field.labelElement.style.color = ThreadlightEditorTheme.TextMuted;
        VisualElement input = DirectInput(field);
        if (input == null) return;
        if (field is Vector3Field) {
            input.AddToClassList("threadlight-vector-input");
            input.style.backgroundColor = Color.clear;
            input.style.borderLeftWidth = input.style.borderTopWidth = 0f;
            input.style.borderRightWidth = input.style.borderBottomWidth = 0f;
            return;
        }
        StyleInput(input, false);
        RegisterFieldStates(field, input, hoverBorder, focusBorder);
        RegisterPointerLight(input, () => ResolveColor(hoverBorder, ThreadlightEditorTheme.FieldBorderHover), 86f, .30f);
        if (field is UnityEditor.UIElements.ObjectField objectField)
            StyleObjectPicker(objectField, hoverBorder, focusBorder);
        NormalizeSliderLayout(field, hoverBorder);
    }
    public static void StyleCompositeAxis(FloatField axis,
        Func<Color> hoverBorder = null, Func<Color> focusBorder = null) {
        if (axis == null || axis.ClassListContains("threadlight-composite-axis")) return;
        ApplySharedStyles(axis);
        axis.AddToClassList("threadlight-composite-axis");
        axis.style.width = StyleKeyword.Auto;
        axis.style.minWidth = 0f;
        axis.style.flexBasis = 0f;
        axis.style.flexGrow = 1f;
        axis.style.flexShrink = 1f;
        axis.style.marginLeft = 1f;
        axis.style.marginRight = 1f;
        axis.style.minHeight = ThreadlightEditorTheme.ControlHeight;
        axis.style.height = ThreadlightEditorTheme.ControlHeight;
        Label label = axis.labelElement;
        label.style.width = label.style.minWidth = label.style.maxWidth = 11f;
        label.style.height = ThreadlightEditorTheme.ControlHeight;
        label.style.paddingLeft = 0f;
        label.style.paddingRight = 2f;
        label.style.flexShrink = 0f;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        VisualElement input = DirectInput(axis);
        if (input != null) {
            StyleInput(input, true);
            input.style.height = ThreadlightEditorTheme.ControlHeight;
            input.style.alignItems = Align.Center;
            RegisterFieldStates(axis, input, hoverBorder, focusBorder);
            RegisterPointerLight(input,
                () => ResolveColor(hoverBorder, ThreadlightEditorTheme.FieldBorderHover),
                72f, .28f);
        }
    }
    public static void StyleVector3Field(Vector3Field field,
        Func<Color> hoverBorder = null, Func<Color> focusBorder = null) {
        if (field == null) return;
        StyleField(field, hoverBorder, focusBorder);
        VisualElement input = DirectInput(field);
        if (input != null) {
            input.style.width = StyleKeyword.Auto;
            input.style.minWidth = 0f;
            input.style.flexBasis = 0f;
            input.style.flexGrow = 1f;
            input.style.flexShrink = 1f;
            input.style.flexDirection = FlexDirection.Row;
            input.style.alignItems = Align.Center;
            input.style.paddingLeft = 0f;
            input.style.paddingRight = 0f;
        }
        field.Query<FloatField>().ForEach(axis =>
            StyleCompositeAxis(axis, hoverBorder, focusBorder));
    }
    public static VisualElement CreateToggleRow(string label, Func<bool> currentValue, Action<bool> changed,
        Func<Color> interactionAccent = null) {
        VisualElement row = new VisualElement();
        StyleToggleRowSurface(row);
        row.AddToClassList(ToggleRowClassName);
        BindWidthClass(row, ToggleRowWrappedClassName, ThreadlightEditorTheme.ToggleWrapBreakpoint);
        Label title = new Label(label ?? string.Empty);
        title.AddToClassList("threadlight-toggle-row-title");
        title.style.color = ThreadlightEditorTheme.TextMuted;
        row.Add(title);
        Button toggle = CreateToggleControl(currentValue, changed, interactionAccent);
        toggle.style.marginLeft = StyleKeyword.Auto;
        row.Add(toggle);
        return row;
    }
    public static void StyleToggleRowSurface(VisualElement row) {
        if (row == null) return;
        ApplySharedStyles(row);
        row.AddToClassList("threadlight-bordered-surface");
        row.style.backgroundColor = ThreadlightEditorTheme.ItemBody;
        SetBorderColor(row, ThreadlightEditorTheme.FieldBorder);
        SetRadius(row, 6f);
    }
    public static Button CreateToggleControl(bool initialValue, Action<bool> changed,
        Func<Color> interactionAccent = null) {
        bool value = initialValue;
        return CreateToggleControl(() => value, next => { value = next; changed?.Invoke(next); },
            interactionAccent);
    }
    public static Button CreateToggleControl(Func<bool> value, Action<bool> changed,
        Func<Color> interactionAccent = null) {
        if (value == null) throw new ArgumentNullException(nameof(value));
        Button toggle = null;
        Action interactionRefresh = null;
        Action<bool, bool> apply = (hovered, focused) => {
            bool enabled = value();
            StyleToggle(toggle, enabled, interactionAccent);
            if (hovered)
                toggle.style.backgroundColor = enabled ? ThreadlightEditorTheme.ToggleOnHover : ThreadlightEditorTheme.ToggleOffHover;
            if (focused)
                SetBorderColor(toggle, ResolveColor(interactionAccent,
                    ThreadlightEditorTheme.FieldBorderFocus));
        };
        toggle = new Button(() => { changed?.Invoke(!value()); interactionRefresh?.Invoke(); });
        ApplySharedStyles(toggle);
        toggle.AddToClassList(ToggleControlClassName);
        toggle.focusable = true;
        toggle.tabIndex = 0;
        ClearDefaultToolkitButtonBackground(toggle);
        RegisterInteractionState(toggle, apply, out interactionRefresh);
        toggleRefreshBindings.Add(toggle, new ToggleRefreshBinding(interactionRefresh));
        return toggle;
    }
    /// <summary>
    /// Reapplies a provider-backed toggle's visual state after its source changes
    /// outside the control, such as through Undo or another inspector.
    /// </summary>
    public static void RefreshToggleControl(Button toggle) {
        if (toggle != null && toggleRefreshBindings.TryGetValue(
                toggle, out ToggleRefreshBinding binding))
            binding.Refresh();
    }
    private sealed class ToggleRefreshBinding {
        private readonly Action refresh;
        internal ToggleRefreshBinding(Action refresh) { this.refresh = refresh; }
        internal void Refresh() => refresh();
    }
    private static void RegisterFieldStates(VisualElement field, VisualElement input,
        Func<Color> hoverBorder, Func<Color> focusBorder) {
        Action<Color, Color> apply = (background, border) =>
        { input.style.backgroundColor = background; SetBorderColor(input, border); };
        RegisterInteractionState(field, (hovered, focused) => apply(
            focused ? ThreadlightEditorTheme.FieldFocus : hovered ? ThreadlightEditorTheme.FieldHover : ThreadlightEditorTheme.Field,
            focused ? ResolveColor(focusBorder, ThreadlightEditorTheme.FieldBorderFocus) :
            hovered ? ResolveColor(hoverBorder, ThreadlightEditorTheme.FieldBorderHover) :
            ThreadlightEditorTheme.FieldBorder), true);
    }
    private static Color ResolveColor(Func<Color> provider, Color fallback) {
        return provider != null ? provider() : fallback;
    }
    public static void NormalizeSliderLayout(VisualElement field, Func<Color> interactionAccent = null) {
        VisualElement slider = field != null && field.ClassListContains("unity-base-slider")
            ? field
            : field?.Q<VisualElement>(className: "unity-base-slider");
        if (slider == null) return;
        slider.AddToClassList("threadlight-slider");
        StretchSliderElement(slider);
        slider.style.alignSelf = Align.Stretch;
        VisualElement input = slider.Q<VisualElement>(className: "unity-base-slider__input");
        if (input != null) { StretchSliderElement(input); input.style.alignItems = Align.Center; }
        VisualElement dragContainer = slider.Q<VisualElement>(className: "unity-base-slider__drag-container");
        if (dragContainer != null) {
            StretchSliderElement(dragContainer);
            dragContainer.style.alignSelf = Align.Center;
            RegisterPointerLight(dragContainer,
                () => ResolveColor(interactionAccent, ThreadlightEditorTheme.FieldBorderHover), 124f, .46f);
        }
        VisualElement valueField = slider.Q<VisualElement>(className: "unity-base-slider__text-field");
        if (valueField == null) return;
        valueField.style.width = 54f;
        valueField.style.minWidth = 46f;
        valueField.style.maxWidth = 64f;
        valueField.style.flexGrow = 0f;
        valueField.style.flexShrink = 0f;
        valueField.style.alignSelf = Align.Center;
    }
    private static void StretchSliderElement(VisualElement element) {
        element.style.width = StyleKeyword.Auto;
        element.style.minWidth = 0f;
        element.style.maxWidth = Length.Percent(100f);
        element.style.flexBasis = 0f;
        element.style.flexGrow = 1f;
        element.style.flexShrink = 1f;
    }
    private static void StyleInput(VisualElement input, bool axis) {
        input.AddToClassList("threadlight-field-input");
        input.AddToClassList(axis ? "threadlight-field-input-axis" : "threadlight-field-input-standard");
        input.style.backgroundColor = ThreadlightEditorTheme.Field;
        SetBorderColor(input, ThreadlightEditorTheme.FieldBorder);
    }
    private static void StyleObjectPicker(UnityEditor.UIElements.ObjectField field,
        Func<Color> hoverAccent,
        Func<Color> focusAccent) {
        VisualElement selector = field?.Q<VisualElement>(
            className: "unity-object-field__selector");
        Texture2D texture = LoadObjectPickerIcon();
        if (selector == null || texture == null) return;
        selector.style.backgroundImage = new StyleBackground();
        selector.style.alignItems = Align.Center;
        selector.style.justifyContent = Justify.Center;
        Image icon = new Image {
            image = texture,
            scaleMode = ScaleMode.ScaleToFit,
            pickingMode = PickingMode.Ignore
        };
        icon.AddToClassList("threadlight-object-picker-icon");
        StyleOpticalIcon(icon, 20f, 20f);
        icon.style.position = Position.Absolute;
        icon.style.left = 0f;
        icon.style.top = 0f;
        icon.style.translate = new Translate(0f, 0f, 0f);
        selector.Add(icon);
        RegisterInteractionState(field, (hovered, focused) => {
            Color tint = focused
                ? ResolveColor(focusAccent, ThreadlightEditorTheme.FieldBorderFocus)
                : hovered
                    ? ResolveColor(hoverAccent, ThreadlightEditorTheme.FieldBorderHover)
                    : ThreadlightEditorTheme.TextMuted;
            selector.style.backgroundColor = focused
                ? ThreadlightEditorTheme.FieldFocus
                : hovered
                    ? ThreadlightEditorTheme.FieldHover
                    : ThreadlightEditorTheme.Field;
            SetBorderColor(selector, focused || hovered
                ? tint
                : ThreadlightEditorTheme.FieldBorder);
            icon.tintColor = tint;
        }, true);
    }
    private static Texture2D LoadObjectPickerIcon() {
        if (objectPickerIcon != null) return objectPickerIcon;
        string path = AssetDatabase.GUIDToAssetPath(ObjectPickerIconGuid);
        if (string.IsNullOrWhiteSpace(path)) path = ObjectPickerIconPath;
        return objectPickerIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    private static VisualElement DirectInput(VisualElement field) {
        foreach (VisualElement child in field.Children())
            if (child.ClassListContains("unity-base-field__input")) return child;
        return null;
    }
    private static void StyleToggle(Button toggle, bool enabled, Func<Color> interactionAccent) {
        if (toggle == null) return;
        toggle.text = "●";
        toggle.style.color = enabled ? ThreadlightEditorTheme.ToggleThumbOn : ThreadlightEditorTheme.ToggleThumbOff;
        toggle.style.unityTextAlign = enabled ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        toggle.style.backgroundColor = enabled ? ThreadlightEditorTheme.ToggleOn : ThreadlightEditorTheme.ToggleOff;
        Color accent = ResolveColor(interactionAccent,
            ThreadlightEditorTheme.Palette(ThreadlightEditorTone.Standard).Accent);
        SetBorderColor(toggle, enabled ? ThreadlightEditorTheme.WithAlpha(accent, .78f)
            : ThreadlightEditorTheme.FieldBorder);
    }
}
}
